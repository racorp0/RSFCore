using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares food-production and journey-stock checks with existing AI and market systems.</summary>
public static class FoodProductionRules
{
    /// <summary>Counts affordable hungry customers not covered by their own food, local fresh sale stock, or active cooking.</summary>
    /// <param name="world">The existing simulation world.</param>
    /// <param name="cook">The cook observing its own village.</param>
    /// <param name="owners">Indexed inventory owners, shared by the calling AI update.</param>
    /// <returns>The number of additional fresh meals currently needed, bounded by the cook's stock limit.</returns>
    public static int FreshMealDemand(World world, Entity cook, IEnumerable<Entity> owners)
    {
        if (!world.TryGet<CookingSettingsComponent>(cook, out var cooking) || cooking is null ||
            !world.TryGet<VillageResidentComponent>(cook, out var resident) || resident is null) return 0;
        List<int> budgets = new();
        List<(long Price, int Servings)> stock = new();
        int pending = 0;
        int quote = MarketPricing.Quote(world, cook, "category:food", cooking.MealPrice);
        foreach (Entity other in owners)
        {
            if (world.Has<DeadComponent>(other) || !world.TryGet<VillageResidentComponent>(other, out var local) || local is null || local.Village != resident.Village ||
                !world.TryGet<HealthComponent>(other, out var health) || health.Current <= 0 ||
                !world.TryGet<InventoryComponent>(other, out var bag) || bag is null) continue;
            if (world.TryGet<CookingAttemptComponent>(other, out var batch) && batch is not null && !batch.Recipe.PreservesFood)
                pending += batch.Recipe.Outputs.Sum(group => group.Options.Max(output => output.MaximumQuantity));
            foreach (Entity item in bag.Items)
            {
                if (!AISurvivalRules.CanUse(world, item, false) || IsPreserved(world, item)) continue;
                if (world.TryGet<TradePriceComponent>(item, out var price) && price.GoldPerUnit > 0 &&
                    world.TryGet<SaleLocationComponent>(item, out var sale) && sale.PlaceId == VillageRules.SalePlace(world, other, item))
                    stock.Add(((long)price.GoldPerUnit * RawFoodRules.Units(world, item), RawFoodRules.Servings(world, item)));
                else if (other == cook)
                    // Count finished meals before their next offer action, otherwise cooking would outrank publishing them forever.
                    stock.Add((quote, RawFoodRules.Servings(world, item)));
            }
            if (other == cook || !NeedsFood(world, other) ||
                (world.TryGet<LifeStageComponent>(other, out var life) && life.Stage is LifeStage.Baby or LifeStage.Toddler) ||
                (world.TryGet<MerchantVisitComponent>(other, out var visit) && visit is not null && visit.Departing) ||
                !world.TryGet<GoldComponent>(other, out var money) || money.Amount < quote ||
                bag.Items.Any(item => AISurvivalRules.CanUse(world, item, false) &&
                    (!world.Has<MerchantVisitComponent>(other) || !IsPreserved(world, item)))) continue;
            budgets.Add(money.Amount);
        }
        // Allocate sale servings once, starting with poorer buyers, so one tomato stack does not create duplicate demand.
        budgets.Sort();
        stock.Sort((left, right) => left.Price.CompareTo(right.Price));
        int unmet = 0;
        foreach (int budget in budgets)
        {
            int index = stock.FindIndex(offer => offer.Servings > 0 && offer.Price <= budget);
            if (index < 0) unmet++;
            else stock[index] = (stock[index].Price, stock[index].Servings - 1);
        }
        return Math.Min(cooking.MealStockTarget, Math.Max(0, unmet - pending));
    }

    /// <summary>Includes ordinary hunger, preparing for work, and scheduled meal breaks in fresh-food demand.</summary>
    /// <param name="world">The NPC world.</param>
    /// <param name="npc">The potential customer.</param>
    /// <returns>True when the existing need and schedule settings call for eating.</returns>
    public static bool NeedsFood(World world, Entity npc)
    {
        if (!world.TryGet<NeedsStateComponent>(npc, out var needs)) return false;
        if (needs.SeekFood) return true;
        return world.TryGet<AIWorkSettingsComponent>(npc, out var work) && work is not null &&
            world.TryGet<ScheduleStateComponent>(npc, out var schedule) && schedule is not null &&
            ((schedule.Activity == ScheduledActivity.Work && (schedule.PreparingFood || !schedule.WorkStarted) && needs.HungerPercent < work.BeforeWorkHungerPercent) ||
             (schedule.Activity == ScheduledActivity.MealBreak && needs.HungerPercent < work.BreakHungerPercent));
    }

    /// <summary>Reads recipe preservation metadata rather than matching a food name.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="item">The proposed meal.</param>
    /// <returns>True for a meal produced by a preservation recipe.</returns>
    public static bool IsPreserved(World world, Entity item) => world.TryGet<MealComponent>(item, out var meal) && meal.IsPreserved;

    /// <summary>Requires journey food to remain edible for the configured minimum journey duration.</summary>
    /// <param name="world">The visitor world.</param>
    /// <param name="item">A proposed preserved meal.</param>
    /// <returns>True for a usable preserved meal with enough remaining lifetime.</returns>
    public static bool JourneyFood(World world, Entity item) => IsPreserved(world, item) && AISurvivalRules.CanUse(world, item, false) &&
        world.TryGet<FoodExpirationComponent>(item, out var expiry) &&
        expiry.ExpiresAt.Value - FoodRules.Now(world).Value >= (GameSettings.Find(world)?.Food.MinimumJourneyShelfLifeMinutes ?? 43200);

    /// <summary>Counts usable journey meals without counting expired or nearly expired smoked food.</summary>
    /// <param name="world">The visitor world.</param>
    /// <param name="npc">The traveler carrying supplies.</param>
    /// <returns>The number of preserved journey servings.</returns>
    public static int JourneyCount(World world, Entity npc) => world.TryGet<InventoryComponent>(npc, out var bag) && bag is not null
        ? bag.Items.Where(item => JourneyFood(world, item)).Sum(item => RawFoodRules.Servings(world, item)) : 0;

    /// <summary>Reserves a local-price estimate for journey food before optional commodity shopping spends the visitor's gold.</summary>
    /// <param name="world">The trading world.</param>
    /// <param name="npc">The visiting buyer.</param>
    /// <returns>Estimated gold needed for missing rations, bounded by the gold component's integer range.</returns>
    public static int JourneyGoldReserve(World world, Entity npc)
    {
        var game = GameSettings.Find(world);
        if (game is null) return 0;
        int missing = Math.Max(0, game.Food.MerchantJourneyFoodTarget - JourneyCount(world, npc));
        int price = game.Cooking.Recipes.Where(recipe => recipe.PreservesFood).SelectMany(recipe => recipe.Outputs)
            .SelectMany(group => group.Options).Where(output => output.ChancePercent > 0 && output.ShelfLifeMinutes >= game.Food.MinimumJourneyShelfLifeMinutes)
            .Select(output => MarketPricing.Quote(world, npc, "item:" + output.ItemId, game.Cooking.MealPrice))
            .Where(quote => quote > 0).DefaultIfEmpty(0).Min();
        return (int)Math.Min(int.MaxValue, (long)missing * price);
    }
}
