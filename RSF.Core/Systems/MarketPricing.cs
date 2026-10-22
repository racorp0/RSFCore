using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares commodity identities, category signals, and local quotes across tracking, trading, and NPC offers.</summary>
public static class MarketPricing
{
    /// <summary>Identifies existing inventory goods without creating a competing item registry.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The existing inventory item.</param>
    /// <param name="settings">Already loaded settings, or null for a manual world.</param>
    /// <returns>The commodity reference, physical quantity, and unit; unknown goods have a null reference.</returns>
    public static (string? Key, long Quantity, string Unit) Identify(World world, Entity item, GameSettings? settings)
    {
        if (world.TryGet<PlantProduceComponent>(item, out var produce))
            return ("item:" + produce.ItemId, produce.Quantity, settings?.Fishing.FishTypes.Any(f => f.MeatItemId == produce.ItemId) == true ? "grams" : "count");
        if (world.TryGet<PlantSeedComponent>(item, out var seed)) return ("seed:" + seed.PlantId, seed.Quantity, "count");
        if (world.TryGet<BaitComponent>(item, out var bait)) return ("bait:" + bait.BaitId, bait.Quantity, "count");
        if (world.TryGet<SpiceComponent>(item, out var spice)) return ("item:spice", spice.Quantity, "count");
        if (world.TryGet<FishComponent>(item, out var fish))
        {
            string species = settings?.Fishing.FishTypes.FirstOrDefault(f => string.Equals(f.Species, fish.Species, StringComparison.OrdinalIgnoreCase))?.Species ?? fish.Species;
            return ("fish:" + species, fish.WeightGrams, "grams");
        }
        if (world.TryGet<MealComponent>(item, out var meal) && !string.IsNullOrWhiteSpace(meal.ItemId)) return ("item:" + meal.ItemId, 1, "count");
        if (world.TryGet<ItemComponent>(item, out var name) && name.Name == "Bread") return ("item:bread", 1, "count");
        return (null, 0, "count");
    }

    /// <summary>Uses one shared pressure signal for substitutable food, bait, and whole fish.</summary>
    /// <param name="commodity">An existing commodity or category reference.</param>
    /// <param name="settings">Loaded recipe definitions used to recognize meal IDs.</param>
    /// <returns>The single signal controlling this commodity's market adjustment.</returns>
    public static string Signal(string commodity, GameSettings? settings)
    {
        if (commodity.StartsWith("bait:", StringComparison.Ordinal)) return "category:bait";
        if (commodity.StartsWith("fish:", StringComparison.Ordinal)) return "category:whole_fish";
        if (commodity == "item:bread" || (commodity.StartsWith("item:", StringComparison.Ordinal) &&
            settings?.Cooking.Recipes.Any(r => r.Outputs.Any(g => g.Options.Any(o => "item:" + o.ItemId == commodity))) == true)) return "category:food";
        return commodity;
    }

    /// <summary>Recognizes the actual item's effects, including meals not listed in the shared recipe defaults.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The existing inventory item.</param>
    /// <param name="commodity">Its specific reference.</param>
    /// <param name="settings">Loaded default definitions.</param>
    /// <returns>One price signal; food takes precedence when a meal also restores health.</returns>
    public static string ItemSignal(World world, Entity item, string commodity, GameSettings? settings)
    {
        if (AISurvivalRules.CanUse(world, item, false)) return "category:food";
        if (AISurvivalRules.CanUse(world, item, true)) return "category:healing";
        return Signal(commodity, settings);
    }

    /// <summary>Combines a commodity's starting offset with its category signal, bounded by the local market limits.</summary>
    /// <param name="world">The buyer or seller world.</param>
    /// <param name="npc">The resident whose market supplies the quote.</param>
    /// <param name="commodity">The existing commodity reference.</param>
    /// <param name="signalOverride">Optional category recognized directly from an actual item.</param>
    /// <returns>A local multiplier, or one in a world without market pricing.</returns>
    public static double Multiplier(World world, Entity npc, string commodity, string? signalOverride = null)
    {
        if (!world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null ||
            !world.TryGetEntity(resident.Village.MarketId, out var market) || market is null ||
            !world.TryGet<MarketPriceComponent>(market, out var prices) || prices is null) return 1;
        world.TryGet<MarketActivityComponent>(market, out var activity);
        string signal = signalOverride ?? activity?.PriceSignals.GetValueOrDefault(commodity) ?? Signal(commodity, GameSettings.Find(world));
        double multiplier = prices.PriceMultipliers.GetValueOrDefault(commodity, 1);
        if (signal != commodity) multiplier *= prices.PriceMultipliers.GetValueOrDefault(signal, 1);
        return Math.Clamp(multiplier, prices.MinimumPriceMultiplier, prices.MaximumPriceMultiplier);
    }

    /// <summary>Rounds a local quote once to positive whole gold, retaining RSF's existing integer money model.</summary>
    /// <param name="world">The trading world.</param>
    /// <param name="npc">The local resident.</param>
    /// <param name="commodity">The commodity to quote.</param>
    /// <param name="basePrice">Unadjusted gold per trade unit, or exact whole-fish weight price.</param>
    /// <param name="signalOverride">Optional category identified from an actual item.</param>
    /// <returns>The quoted gold price, or zero for invalid or overflowing prices.</returns>
    public static int Quote(World world, Entity npc, string commodity, double basePrice, string? signalOverride = null)
    {
        if (!double.IsFinite(basePrice) || basePrice <= 0) return 0;
        double multiplier = Multiplier(world, npc, commodity, signalOverride);
        double estimate = basePrice * multiplier;
        if (!double.IsFinite(estimate) || estimate > int.MaxValue || basePrice > (double)decimal.MaxValue ||
            multiplier > (double)decimal.MaxValue) return 0;
        // Decimal avoids a binary rounding tail such as 110.00000000000001 becoming an extra gold.
        decimal result = decimal.Ceiling((decimal)basePrice * (decimal)multiplier);
        return result > int.MaxValue ? 0 : Math.Max(1, (int)result);
    }

    /// <summary>Quotes an NPC offer by its identity or survival effects, rounding the supplied base price only once.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="npc">The seller.</param>
    /// <param name="item">The offered inventory item.</param>
    /// <param name="basePrice">The original price before market adjustment.</param>
    /// <returns>The local quote, using a survival category when goods have no specific identity.</returns>
    public static int QuoteItem(World world, Entity npc, Entity item, double basePrice)
    {
        if (FoodRules.IsExpired(world, item)) return 0;
        var settings = GameSettings.Find(world);
        var identity = Identify(world, item, settings);
        string key = identity.Key ?? (AISurvivalRules.CanUse(world, item, false) ? "category:food" : "category:healing");
        return Quote(world, npc, key, basePrice, ItemSignal(world, item, key, settings));
    }

    /// <summary>Marks only a successfully published NPC offer for later automatic refreshes.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="seller">The NPC that published the offer.</param>
    /// <param name="item">The already offered item.</param>
    /// <param name="basePrice">Its unadjusted price, retained independently of the latest quote.</param>
    public static void MarkAutomatic(World world, Entity seller, Entity item, double basePrice)
    {
        if (world.TryGet<VillageResidentComponent>(seller, out var resident) && resident is not null &&
            world.TryGetEntity(resident.Village.MarketId, out var market) && market is not null && world.Has<MarketPriceComponent>(market) &&
            world.TryGet<TradePriceComponent>(item, out var price))
            world.Add(item, new AutomaticMarketPriceComponent(market.Id, seller.Id, basePrice, price.GoldPerUnit));
    }
}
