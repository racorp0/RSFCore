using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Builds feasible job options for the existing AI decision system; it never executes work.</summary>
internal static class AIWorkDecision
{
    /// <summary>Adds existing job choices and bounded smoker ingredient purchases from shared indexed candidates.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="clock">The game-minute clock.</param>
    /// <param name="npc">The worker making a decision.</param>
    /// <param name="needs">Current survival urgency.</param>
    /// <param name="schedule">The resolved daily preference.</param>
    /// <param name="work">Per-worker scores, prices, and stock targets.</param>
    /// <param name="settings">General AI stability settings.</param>
    /// <param name="state">Current target and failed actions.</param>
    /// <param name="snapshot">World-ordered owners, waters, and stock shared by due NPCs.</param>
    /// <param name="choices">The list receiving scored options.</param>
    public static void AddCandidates(World world, WorldClock clock, Entity npc, NeedsStateComponent needs,
        ScheduleStateComponent schedule, AIWorkSettingsComponent work, AISettingsComponent settings,
        AIStateComponent state, AIDecisionSnapshot snapshot, List<AIActionCandidate> choices)
    {
        if (world.TryGet<MerchantVisitComponent>(npc, out var visitor) && visitor is not null && visitor.Departing) return;
        if (world.Has<SleepingComponent>(npc) || world.Has<CookingAttemptComponent>(npc)) return;
        bool working = schedule.Activity == ScheduledActivity.Work && schedule.Stage == LifeStage.Adult && schedule.Job != Job.None;
        if (world.Has<FishingAttemptComponent>(npc))
        {
            // An active attempt is kept intact until completion unless needs or schedules interrupt it.
            bool continueFishing = working && schedule.Job == Job.Fisher && !needs.HealthCritical && !needs.HungerUrgent;
            Add(continueFishing ? AIActivity.WaitForFishing : AIActivity.CancelFishing, npc, work.FishingPoints);
            return;
        }
        if (!working || needs.HealthCritical || needs.HungerUrgent || schedule.PreparingFood ||
            (!schedule.WorkStarted && needs.HungerPercent < work.BeforeWorkHungerPercent) ||
            !world.TryGet<InventoryComponent>(npc, out var inventory) || inventory is null) return;

        // Publishing sale offers is a normal job action, not an invisible transfer of items or money.
        foreach (Entity item in inventory.Items)
        {
            if (FoodRules.IsExpired(world, item) || world.Has<TradePriceComponent>(item)) continue;
            if (ProcessingAI.WantsToProcess(world, npc, item)) continue;
            if (schedule.Job == Job.Farmer && (world.Has<BaitComponent>(item) || world.Has<PlantProduceComponent>(item))) Add(AIActivity.OfferGoods, item, work.OfferPoints);
            if (schedule.Job == Job.Fisher && world.Has<FishComponent>(item)) Add(AIActivity.OfferGoods, item, work.OfferPoints);
            if (schedule.Job == Job.Cook && world.TryGet<ItemComponent>(item, out var food) && food.Name == "Bread")
                Add(AIActivity.OfferGoods, item, work.OfferPoints);
        }

        if (schedule.Job == Job.Farmer)
        {
            var farming = GameSettings.Find(world)?.Farming ?? new FarmingSettingsComponent();
            bool anyPlantSeeds = inventory.Items.Any(item => world.TryGet<PlantSeedComponent>(item, out var seed) && !world.Has<SaleLocationComponent>(item) &&
                farming.Plants.Any(p => p.Id == seed.PlantId && seed.Quantity >= p.SeedsPerPlanting));
            // Count each farmer's crop mix once per decision, rather than scanning the world for every empty plot.
            Dictionary<string, int> plantCounts = new();
            foreach (Entity entity in snapshot.OwnedEntities)
            {
                if (!world.TryGet<OwnershipComponent>(entity, out var owner) || owner is null || owner.OwnerId != npc.Id) continue;
                if (world.TryGet<PlantCropComponent>(entity, out var growing) && !growing.IsDry)
                {
                    plantCounts.TryGetValue(growing.Definition.Id, out int count);
                    plantCounts[growing.Definition.Id] = count + 1;
                }
            }
            bool can = inventory.Items.Any(item => world.Has<WateringCanComponent>(item));
            foreach (Entity plot in snapshot.OwnedEntities)
            {
                // Explicit plot ownership prevents one farmer from harvesting another farmer's crops.
                if (!world.TryGet<OwnershipComponent>(plot, out var owner) || owner is null || owner.OwnerId != npc.Id ||
                    !world.TryGet<MapTileComponent>(plot, out var tile) || tile is null || tile.Zone != MapZone.Farmland) continue;
                if (!world.Has<FarmPlotComponent>(plot))
                {
                    if (anyPlantSeeds) Add(AIActivity.Plow, plot, work.PlowPoints);
                }
                else if (world.TryGet<PlantCropComponent>(plot, out var plant))
                {
                    if (plant.IsDry) Add(AIActivity.ClearCrop, plot, work.HarvestPoints);
                    else if (plant.IsMature) Add(AIActivity.HarvestCrop, plot, work.HarvestPoints);
                    else if (can && !plant.WasWatered && world.TryGet<FarmPlotComponent>(plot, out var soil) &&
                        (soil.LastWateredAt is null || clock.CurrentMinute.ElapsedSince(soil.LastWateredAt.Value).Minutes >= plant.Definition.WateringIntervalMinutes)) Add(AIActivity.Water, plot, work.WaterPoints);
                }
                else
                {
                    // A deficit bonus balances configured plot preferences without naming any species in code.
                    for (int index = 0; index < farming.Plants.Count; index++)
                    {
                        var definition = farming.Plants[index];
                        if (!inventory.Items.Any(e => world.TryGet<PlantSeedComponent>(e, out var seed) && seed.PlantId == definition.Id &&
                            seed.Quantity >= definition.SeedsPerPlanting && !world.Has<SaleLocationComponent>(e))) continue;
                        plantCounts.TryGetValue(definition.Id, out int active);
                        Add(AIActivity.PlantCrop, plot, work.PlantPoints + (active < definition.PreferredPlotsPerFarmer ? work.CropPreferenceBonusPoints : 0), quantity: index);
                    }
                }
            }
        }
        else if (schedule.Job == Job.Fisher)
        {
            if (!world.TryGet<FishingSkillComponent>(npc, out var skill) || skill.Level < 1 ||
                !inventory.Items.Any(item => world.Has<FishingRodComponent>(item)) ||
                !inventory.Items.Any(item => world.TryGet<BaitComponent>(item, out var bait) && bait.Quantity > 0)) return;
            foreach (Entity river in snapshot.Waters)
                if (world.TryGet<RiverComponent>(river, out var water) && water is not null && inventory.Items.Any(item =>
                    world.TryGet<BaitComponent>(item, out var bait) && bait.Quantity > 0 && !world.Has<SaleLocationComponent>(item) &&
                    BaitRules.CanUse(world, water, bait.BaitId))) Add(AIActivity.StartFishing, river, work.FishingPoints);
        }
        else if (schedule.Job is Job.Cook or Job.Smoker && world.TryGet<GoldComponent>(npc, out var wallet))
        {
            world.TryGet<CookingSettingsComponent>(npc, out var kitchen);
            var food = GameSettings.Find(world)?.Food ?? new FoodSettings();
            if (schedule.Job == Job.Smoker && FoodProductionRules.JourneyCount(world, npc) >= food.PreservedFoodStockTarget) return;
            // Keep the cook's existing small ingredient buffer. Cooking waits for customers, but buying every input only after hunger strikes would block meal preparation.
            // Each profession buys only inputs for its own recipes, not every known personal fallback recipe.
            var recipes = kitchen?.Recipes.Where(recipe => recipe.PreservesFood == (schedule.Job == Job.Smoker) &&
                ProcessingRules.Requirements(world, npc, recipe) && CookingRules.FindStation(world, npc, recipe, snapshot.ProcessingStations) is not null).ToList() ?? new();
            long meatGrams = inventory.Items.Where(item => !world.Has<SaleLocationComponent>(item))
                .Sum(item => (long)FishMeat.Grams(world, item));
            foreach (Entity seller in snapshot.InventoryOwners)
            {
                if (seller == npc || world.Has<DeadComponent>(seller) ||
                    !world.TryGet<HealthComponent>(seller, out var health) || health.Current <= 0 ||
                    !world.TryGet<GoldComponent>(seller, out var gold) || gold.Amount < 0 ||
                    !world.TryGet<InventoryComponent>(seller, out var stock) || stock is null) continue;
                foreach (Entity item in stock.Items)
                {
                    if (FoodRules.IsExpired(world, item) || !world.Has<SaleLocationComponent>(item) || !world.TryGet<TradePriceComponent>(item, out var price) || price.GoldPerUnit <= 0) continue;
                    int quantity = 0;
                    if (world.TryGet<PlantProduceComponent>(item, out var product) && GameSettings.Find(world) is GameSettings configured &&
                        configured.Products.TryGetValue(product.ItemId, out var output) && kitchen is not null &&
                        recipes.Any(recipe => recipe.Inputs.Any(g => g.Options.Any(o => o.Kind == ProcessingItemKind.Item && o.ItemId == product.ItemId))))
                    {
                        long held = InventoryStacks.CountItem(world, inventory.Items, product.ItemId);
                        // Prepared fish meat is measured in grams; other ingredients retain their existing product targets.
                        long target = FishMeat.Grams(world, item) > 0 ? Math.Max(0, work.FishMeatStockTargetGrams - meatGrams + held) : output.CookStockTarget;
                        quantity = (int)Math.Min(product.Quantity, Math.Max(0, target - held));
                    }
                    else if (world.TryGet<FishComponent>(item, out var fish) && meatGrams < work.FishMeatStockTargetGrams &&
                        FishMeat.Definition(world, fish.Species) is var meat && meat is not null && FishMeat.Grams(world, item) > 0 && kitchen is not null &&
                        recipes.Any(recipe => recipe.Inputs.Any(group => group.Options.Any(option => option.Kind == ProcessingItemKind.Item && option.ItemId == meat.MeatItemId)))) quantity = 1;
                    quantity = Math.Min(quantity, Math.Max(0, wallet.Amount / price.GoldPerUnit));
                    if (world.TryGet<PlantProduceComponent>(item, out var ingredient) && FishMeat.Grams(world, item) > 0)
                    {
                        int batchGrams = recipes.SelectMany(recipe => recipe.Inputs).SelectMany(group => group.Options)
                            .Where(option => option.Kind == ProcessingItemKind.Item && option.ItemId == ingredient.ItemId)
                            .Select(option => option.Quantity).DefaultIfEmpty(int.MaxValue).Min();
                        long held = InventoryStacks.CountItem(world, inventory.Items, ingredient.ItemId);
                        // Avoid spending the last gold on a few grams that cannot complete even one recipe.
                        if (held + quantity < batchGrams) continue;
                    }
                    if (quantity <= 0 || (long)gold.Amount + (long)quantity * price.GoldPerUnit > int.MaxValue) continue;
                    Add(AIActivity.BuyIngredients, item,
                        work.IngredientBuyingPoints - (double)quantity * price.GoldPerUnit * settings.PurchaseCostWeight,
                        seller.Id, quantity);
                }
            }
        }

        /// <summary>Scores one candidate while respecting previous failures and target stability.</summary>
        /// <param name="activity">The proposed action.</param>
        /// <param name="target">The target entity.</param>
        /// <param name="points">The configured base points.</param>
        /// <param name="seller">The optional seller for purchases.</param>
        /// <param name="quantity">The number of grains or items.</param>
        void Add(AIActivity activity, Entity target, double points, EntityId seller = default, int quantity = 1)
        {
            if (state.FailedTargets.ContainsKey(target.Id)) return;
            double score = points + work.ScheduleBonus;
            if (state.Activity == activity && state.TargetId == target.Id && state.SellerId == seller)
                score += settings.ContinuationBonus;
            choices.Add(new AIActionCandidate(activity, target, seller, score, 0, quantity));
        }
    }
}
