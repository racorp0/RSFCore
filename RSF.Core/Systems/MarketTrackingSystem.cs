using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Observes local sale stock and affordable purchase needs without executing trades or setting prices.</summary>
public sealed class MarketTrackingSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly GameSettings? _settings;

    /// <summary>Reuses the world, clock, and already loaded JSON definitions.</summary>
    /// <param name="world">The world containing markets and NPC inventories.</param>
    /// <param name="clock">The simulation's existing game-minute clock.</param>
    public MarketTrackingSystem(World world, WorldClock clock)
    {
        _world = world;
        _clock = clock;
        _settings = GameSettings.Find(world);
    }

    /// <summary>Replaces due market snapshots and expires old sales, removing fulfilled, abandoned, and deceased buyers' old requests.</summary>
    /// <param name="deltaTime">Elapsed simulation time; refreshes use the shared game clock.</param>
    public void Update(float deltaTime)
    {
        // Only markets are checked each minute; terrain is never scanned by this system.
        var due = _world.GetEntitiesWith<MarketActivityComponent>().Where(market =>
            _world.TryGet<MarketPriceComponent>(market, out var policy) && policy is not null &&
            _world.TryGet<MarketActivityComponent>(market, out var activity) && activity is not null &&
            (activity.LastTrackedAt is null || _clock.CurrentMinute.Value - activity.LastTrackedAt.Value.Value >= policy.TrackingIntervalMinutes)).ToList();
        if (due.Count == 0) return;
        var residents = _world.GetEntitiesWith<VillageResidentComponent>().Where(IsTrader).ToList();
        foreach (Entity market in due)
        {
            _world.TryGet<MarketActivityComponent>(market, out var activity);
            var locals = residents.Where(npc => _world.TryGet<VillageResidentComponent>(npc, out var resident) &&
                resident is not null && resident.Village.MarketId == market.Id).ToList();
            _world.TryGet<MarketPriceComponent>(market, out var policy);
            Refresh(activity!, locals, policy!);
        }
    }

    /// <summary>Builds stock and food-serving snapshots without erasing the separate rolling purchase history.</summary>
    /// <param name="activity">The local market data to replace.</param>
    /// <param name="locals">Buyers and sellers belonging to this market.</param>
    /// <param name="policy">This market's rolling history limits.</param>
    private void Refresh(MarketActivityComponent activity, List<Entity> locals, MarketPriceComponent policy)
    {
        activity.HistoryStartedAt ??= _clock.CurrentMinute;
        // Keep sales separate from requests: snapshots must never erase completed purchases.
        MarketSales.Prune(activity, _clock.CurrentMinute, policy.SalesWindowMinutes);
        activity.Supply.Clear();
        activity.Demand.Clear();
        activity.OutstandingRequests.Clear();
        Dictionary<string, List<(Entity Seller, Entity Item, long Price)>> offers = new(StringComparer.Ordinal);
        HashSet<EntityId> seen = new();
        foreach (Entity seller in locals)
        {
            _world.TryGet<InventoryComponent>(seller, out var bag);
            foreach (Entity item in bag!.Items)
            {
                if (!_world.TryGetEntity(item.Id, out var registered) || registered != item || !_world.Has<ItemComponent>(item) ||
                    !_world.TryGet<TradePriceComponent>(item, out var price) || price.GoldPerUnit <= 0 ||
                    !_world.TryGet<SaleLocationComponent>(item, out var location) ||
                    location.PlaceId != VillageRules.SalePlace(_world, seller, item) ||
                    !_world.TryGetEntity(location.PlaceId, out _) ||
                    (_world.TryGet<EquipmentComponent>(seller, out var equipment) && equipment?.Weapon == item) ||
                    !_world.TryGet<GoldComponent>(seller, out var gold) || (long)gold.Amount + price.GoldPerUnit > int.MaxValue ||
                    InventoryStacks.Count(_world, item) <= 0 || !seen.Add(item.Id)) continue;
                var commodity = Identify(item);
                if (commodity.Key is not null)
                {
                    activity.PriceSignals[commodity.Key] = MarketPricing.ItemSignal(_world, item, commodity.Key, _settings);
                    AddSupply(commodity.Key, commodity.Quantity, commodity.Unit, price.GoldPerUnit);
                }
                // Categories are substitute pools, not extra physical items to sum with specific commodities.
                if (AISurvivalRules.CanUse(_world, item, false)) AddSupply("category:food", RawFoodRules.Servings(_world, item), "count", (long)price.GoldPerUnit * RawFoodRules.Units(_world, item));
                if (AISurvivalRules.CanUse(_world, item, true)) AddSupply("category:healing", RawFoodRules.Servings(_world, item), "count", (long)price.GoldPerUnit * RawFoodRules.Units(_world, item));
                if (_world.Has<BaitComponent>(item) && _world.TryGet<BaitComponent>(item, out var bait) && BaitRules.Find(_world, bait.BaitId) is not null)
                    AddSupply("category:bait", bait.Quantity, "count", price.GoldPerUnit);
                if (_world.Has<FishComponent>(item)) AddSupply("category:whole_fish", 1, "count", price.GoldPerUnit);

                /// <summary>Adds an offer to both the quantity snapshot and the affordability lookup.</summary>
                /// <param name="key">The commodity or substitute category.</param>
                /// <param name="quantity">The listed amount.</param>
                /// <param name="unit">Count or grams.</param>
                /// <param name="unitPrice">Gold per lookup unit; food categories use a complete serving and whole fish use one fish.</param>
                void AddSupply(string key, long quantity, string unit, long unitPrice)
                {
                    if (quantity <= 0) return;
                    AddTotal(activity.Supply, key, quantity, unit);
                    if (!offers.TryGetValue(key, out var list)) offers[key] = list = new();
                    list.Add((seller, item, unitPrice));
                }
            }
        }
        foreach (Entity npc in locals) TrackBuyer(activity, offers, npc);
        activity.LastTrackedAt = _clock.CurrentMinute;
    }

    /// <summary>Records affordable survival, demand-led ingredient, and final journey-food requests against one wallet.</summary>
    /// <param name="activity">The snapshot receiving deduplicated requests.</param>
    /// <param name="offers">Actual posted prices grouped by commodity.</param>
    /// <param name="npc">The buyer being observed.</param>
    private void TrackBuyer(MarketActivityComponent activity, Dictionary<string, List<(Entity Seller, Entity Item, long Price)>> offers, Entity npc)
    {
        if (!_world.Has<AISettingsComponent>(npc) || !_world.TryGet<NeedsStateComponent>(npc, out var needs) ||
            !_world.TryGet<ScheduleStateComponent>(npc, out var schedule) || schedule is null ||
            schedule.Stage is LifeStage.Baby or LifeStage.Toddler ||
            (_world.TryGet<MerchantVisitComponent>(npc, out var visitor) && visitor is not null && visitor.Departing)) return;
        _world.TryGet<GoldComponent>(npc, out var wallet);
        _world.TryGet<InventoryComponent>(npc, out var inventory);
        _world.TryGet<AIWorkSettingsComponent>(npc, out var work);
        long budget = wallet.Amount;
        var held = inventory!.Items.Distinct().Where(item => _world.TryGetEntity(item.Id, out var registered) && registered == item).ToList();
        bool foodNeeded = needs.SeekFood || (work is not null &&
            ((schedule.Activity == ScheduledActivity.Work && (schedule.PreparingFood || !schedule.WorkStarted) && needs.HungerPercent < work.BeforeWorkHungerPercent) ||
             (schedule.Activity == ScheduledActivity.MealBreak && needs.HungerPercent < work.BreakHungerPercent)));
        if (needs.HealthCritical && needs.SeekHealing && !held.Any(item => AISurvivalRules.CanUse(_world, item, true)))
            Request("category:healing", 1, "count", 0);
        if (foodNeeded && !held.Any(item => AISurvivalRules.CanUse(_world, item, false) &&
            (visitor is null || !FoodProductionRules.IsPreserved(_world, item))))
        {
            int reference = _settings is null ? 0 : _settings.Cooking.Recipes.SelectMany(recipe => recipe.Outputs)
                .SelectMany(group => group.Options).Where(output => output.ChancePercent > 0)
                .Select(output => MarketPricing.Quote(_world, npc, "item:" + output.ItemId, _settings.Cooking.MealPrice))
                .Prepend(MarketPricing.Quote(_world, npc, "item:bread", _settings.AI.Work.BreadPrice))
                .Where(price => price > 0).DefaultIfEmpty(0).Min();
            Request("category:food", 1, "count", reference, quotedReference: true);
        }
        if (!needs.HealthCritical && needs.SeekHealing && !held.Any(item => AISurvivalRules.CanUse(_world, item, true)))
            Request("category:healing", 1, "count", 0);
        if (visitor is not null && visitor.PreparingDeparture && _settings is not null)
        {
            int wanted = _settings.Food.MerchantJourneyFoodTarget - FoodProductionRules.JourneyCount(_world, npc);
            // Choose one affordable preserved identity, never count every alternative recipe as separate demand.
            var options = _settings.Cooking.Recipes.Where(recipe => recipe.PreservesFood).SelectMany(recipe => recipe.Outputs)
                .SelectMany(group => group.Options).Where(output => output.ChancePercent > 0 &&
                    output.ShelfLifeMinutes >= _settings.Food.MinimumJourneyShelfLifeMinutes)
                .Select(output => new { Key = "item:" + output.ItemId,
                    Price = MarketPricing.Quote(_world, npc, "item:" + output.ItemId, _settings.Cooking.MealPrice) })
                .Where(option => option.Price > 0).OrderBy(option => option.Price).ToList();
            foreach (var option in options)
            {
                if (option.Price > budget) continue;
                Request(option.Key, wanted, "count", option.Price, item => FoodProductionRules.JourneyFood(_world, item), quotedReference: true);
                break;
            }
            return;
        }
        // A survival interruption must not simultaneously create a speculative work shopping list.
        if (_settings is null || work is null || schedule.Stage != LifeStage.Adult || schedule.Activity != ScheduledActivity.Work ||
            needs.HealthCritical || needs.HungerUrgent || schedule.PreparingFood ||
            (!schedule.WorkStarted && needs.HungerPercent < work.BeforeWorkHungerPercent) ||
            _world.Has<SleepingComponent>(npc) || _world.Has<CookingAttemptComponent>(npc)) return;
        var available = held.Where(item => !_world.Has<SaleLocationComponent>(item)).ToList();
        if (schedule.Job == Job.Fisher)
        {
            long count = available.Where(item => _world.TryGet<BaitComponent>(item, out var bait) && BaitRules.Find(_world, bait.BaitId) is not null)
                .Sum(item => (long)InventoryStacks.Count(_world, item));
            int reference = _settings.Fishing.Baits.Select(bait => MarketPricing.Quote(_world, npc, "bait:" + bait.Id, bait.Price))
                .Where(price => price > 0).DefaultIfEmpty(0).Min();
            Request("category:bait", work.BaitStockTarget - count, "count", reference, quotedReference: true);
        }
        if (schedule.Job == Job.Farmer)
            foreach (var plant in _settings.Farming.Plants)
                Request("seed:" + plant.Id, plant.SeedStockTarget - available.Where(item => _world.TryGet<PlantSeedComponent>(item, out var seed) && seed.PlantId == plant.Id)
                    .Sum(item => (long)InventoryStacks.Count(_world, item)), "count", plant.SeedPrice);
        _world.TryGet<CookingSettingsComponent>(npc, out var cooking);
        if (schedule.Job is Job.Cook or Job.Smoker && cooking is not null &&
            (schedule.Job == Job.Cook || FoodProductionRules.JourneyCount(_world, npc) < _settings.Food.PreservedFoodStockTarget))
        {
            cooking = cooking with { Recipes = cooking.Recipes.Where(recipe => recipe.PreservesFood == (schedule.Job == Job.Smoker) &&
                ProcessingRules.Requirements(_world, npc, recipe) && CookingRules.FindStation(_world, npc, recipe) is not null).ToList() };
            if (schedule.Job == Job.Cook) Request("item:spice", cooking.SpiceStockTarget - available.Where(item => _world.Has<SpiceComponent>(item))
                .Sum(item => (long)InventoryStacks.Count(_world, item)), "count", _settings.Economy.SpicePrice);
            bool meatRequested = false;
            foreach (var product in _settings.Products.Values)
            {
                if (!Uses(cooking, product.ItemId)) continue;
                bool meat = _settings.Fishing.FishTypes.Any(fish => fish.MeatItemId == product.ItemId);
                // Fresh meat and whole fish are alternatives. Missing meat offers must not reserve money needed for fish.
                if (meat && !offers.ContainsKey("item:" + product.ItemId)) continue;
                int minimum = meat ? cooking.Recipes.SelectMany(recipe => recipe.Inputs).SelectMany(group => group.Options)
                    .Where(option => option.Kind == ProcessingItemKind.Item && option.ItemId == product.ItemId)
                    .Select(option => option.Quantity).DefaultIfEmpty(1).Min() : 1;
                if (meat) minimum = (int)Math.Max(1, minimum - InventoryStacks.CountItem(_world, available, product.ItemId));
                Request("item:" + product.ItemId, meat
                    ? Math.Max(0, work.FishMeatStockTargetGrams - available.Sum(item => (long)FishMeat.Grams(_world, item)))
                    : product.CookStockTarget - InventoryStacks.CountItem(_world, held, product.ItemId),
                    Unit(product.ItemId), product.SellPrice, minimumQuantity: minimum);
                if (meat && activity.OutstandingRequests.ContainsKey(new(npc.Id, "item:" + product.ItemId))) meatRequested = true;
            }
            if (!meatRequested && available.Sum(item => (long)FishMeat.Grams(_world, item)) < work.FishMeatStockTargetGrams)
            {
                int reference = _settings.Fishing.FishTypes.Where(fish => Uses(cooking, fish.MeatItemId))
                    .Select(fish => MarketPricing.Quote(_world, npc, "fish:" + fish.Species, (double)fish.MinWeightGrams * fish.PricePer500Grams / 500))
                    .Where(price => price > 0).DefaultIfEmpty(0).Min();
                Request("category:whole_fish", 1, "count", reference, item =>
                    _world.TryGet<FishComponent>(item, out var fish) && FishMeat.Definition(_world, fish.Species) is var definition &&
                    definition is not null && Uses(cooking, definition.MeatItemId), quotedReference: true);
            }
        }
        if (visitor is not null)
        {
            budget = Math.Max(0, budget - FoodProductionRules.JourneyGoldReserve(_world, npc));
            int reference = _settings.Fishing.FishTypes.Select(fish => MarketPricing.Quote(_world, npc, "fish:" + fish.Species,
                (double)fish.MinWeightGrams * fish.PricePer500Grams / 500)).Where(price => price > 0).DefaultIfEmpty(0).Min();
            Request("category:whole_fish", visitor.FishTarget - available.Count(item => _world.Has<FishComponent>(item)), "count", reference, quotedReference: true);
            foreach (var product in _settings.Products.Values)
                Request("item:" + product.ItemId, product.MerchantStockTarget - InventoryStacks.CountItem(_world, held, product.ItemId), Unit(product.ItemId), product.SellPrice);
        }

        /// <summary>Uses posted prices when present, otherwise a locally adjusted configured reference; reserves funds once per request.</summary>
        /// <param name="key">The commodity or general category.</param>
        /// <param name="wanted">The outstanding stock deficit.</param>
        /// <param name="unit">Count or grams.</param>
        /// <param name="fallbackPrice">Configured reference price, or zero when none is known.</param>
        /// <param name="accepts">Optional ingredient eligibility check for actual offers.</param>
        /// <param name="quotedReference">True when substitute definitions have already been quoted individually.</param>
        /// <param name="minimumQuantity">Smallest usable purchase, so meat demand never reserves gold for an incomplete recipe.</param>
        void Request(string key, long wanted, string unit, int fallbackPrice, Func<Entity, bool>? accepts = null, bool quotedReference = false, int minimumQuantity = 1)
        {
            var requestKey = new MarketRequestKey(npc.Id, key);
            if (wanted <= 0 || activity.OutstandingRequests.ContainsKey(requestKey)) return;
            var prices = offers.TryGetValue(key, out var list)
                ? list.Where(offer => offer.Seller != npc && (accepts is null || accepts(offer.Item))).Select(offer => offer.Price)
                : Enumerable.Empty<long>();
            // When stock is absent, affordability still follows the current local quote.
            int reference = quotedReference ? fallbackPrice : fallbackPrice > 0 ? MarketPricing.Quote(_world, npc, key, fallbackPrice) : 0;
            long price = prices.DefaultIfEmpty(reference).Min();
            if (price <= 0) return;
            long quantity = Math.Min(wanted, budget / price);
            if (quantity < minimumQuantity) return;
            budget -= quantity * price;
            activity.OutstandingRequests[requestKey] = new MarketQuantity(quantity, unit);
            AddTotal(activity.Demand, key, quantity, unit);
        }
    }

    /// <summary>Checks the same basic living-owner requirements used by trading.</summary>
    /// <param name="npc">The proposed market participant.</param>
    /// <returns>True for a living registered inventory owner with nonnegative gold.</returns>
    private bool IsTrader(Entity npc) => !_world.Has<DeadComponent>(npc) &&
        _world.TryGet<HealthComponent>(npc, out var health) && health.Current > 0 &&
        _world.TryGet<InventoryComponent>(npc, out var bag) && bag is not null &&
        _world.TryGet<GoldComponent>(npc, out var wallet) && wallet.Amount >= 0;

    /// <summary>Maps existing component identities to commodity references and preserves physical units.</summary>
    /// <param name="item">The listed inventory item.</param>
    /// <returns>The identity and quantity, or a null identity for unsupported goods.</returns>
    private (string? Key, long Quantity, string Unit) Identify(Entity item) => MarketPricing.Identify(_world, item, _settings);

    /// <summary>Recognizes configured meat quantities as grams instead of inventing portion counts.</summary>
    /// <param name="itemId">The existing product identity.</param>
    /// <returns>The product's physical quantity unit.</returns>
    private string Unit(string itemId) => _settings?.Fishing.FishTypes.Any(fish => fish.MeatItemId == itemId) == true ? "grams" : "count";

    /// <summary>Checks whether a cook's known recipes accept this ingredient.</summary>
    /// <param name="cooking">The cook's existing recipe settings.</param>
    /// <param name="itemId">The ordinary ingredient identity.</param>
    /// <returns>True when at least one recipe accepts this item.</returns>
    private static bool Uses(CookingSettingsComponent cooking, string itemId) => cooking.Recipes.Any(recipe =>
        recipe.Inputs.Any(group => group.Options.Any(option => option.Kind == ProcessingItemKind.Item && option.ItemId == itemId)));

    /// <summary>Adds quantities within one commodity; category and specific totals remain separate views.</summary>
    /// <param name="totals">The market's supply or demand dictionary.</param>
    /// <param name="key">The commodity identity.</param>
    /// <param name="quantity">The amount to add.</param>
    /// <param name="unit">Its physical unit.</param>
    private static void AddTotal(Dictionary<string, MarketQuantity> totals, string key, long quantity, string unit)
    {
        totals.TryGetValue(key, out var previous);
        totals[key] = new MarketQuantity(previous.Quantity + quantity, unit);
    }
}
