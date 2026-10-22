using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Adds village cooking, sleep, visitor, and supply choices to the existing score comparison.</summary>
internal static class VillageAIDecision
{
    /// <summary>Plans demand-led cooking, smoker offers, departure shopping, and village needs through existing scored actions.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="clock">The shared clock.</param>
    /// <param name="npc">The NPC choosing an activity.</param>
    /// <param name="needs">Its current survival needs.</param>
    /// <param name="state">Its current action and failed targets.</param>
    /// <param name="settings">Its stability settings.</param>
    /// <param name="snapshot">World-ordered inventory owners, inn guests, and processing hosts for this update.</param>
    /// <param name="choices">The candidate list receiving village actions.</param>
    public static void Add(World world, WorldClock clock, Entity npc, NeedsStateComponent needs, AIStateComponent state,
        AISettingsComponent settings, AIDecisionSnapshot snapshot, List<AIActionCandidate> choices)
    {
        if (!world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null ||
            !world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) return;
        VillageComponent village = resident.Village;
        world.TryGet<MerchantVisitComponent>(npc, out var visit);
        if (visit is not null && visit.Departing)
        { AddChoice(AIActivity.LeaveVillage, village.EntranceId, 10000); return; }

        if (world.TryGet<SleepSettingsComponent>(npc, out var sleep) && sleep is not null && world.TryGet<FatigueComponent>(npc, out var fatigue))
        {
            int minute = (int)(clock.CurrentMinute.Value % GameDate.MinutesPerDay);
            bool night = sleep.BedtimeMinute >= sleep.WakeTimeMinute
                ? minute >= sleep.BedtimeMinute || minute < sleep.WakeTimeMinute
                : minute >= sleep.BedtimeMinute && minute < sleep.WakeTimeMinute;
            bool sleeping = world.Has<SleepingComponent>(npc);
            if (sleeping || fatigue.Value >= sleep.UrgentFatigue || (night && fatigue.Value >= sleep.PreferredFatigue))
            {
                bool ownedHome = world.TryGet<HomeComponent>(npc, out var home) &&
                    world.TryGetEntity(home.HouseId, out var house) && house is not null &&
                    world.TryGet<OwnershipComponent>(house, out var owner) && owner is not null && owner.OwnerId == npc.Id;
                EntityId place = ownedHome ? home.HouseId : village.InnId;
                bool entitled = ownedHome ||
                    (world.TryGet<InnStayComponent>(npc, out var stay) && stay.InnId == place && stay.PaidUntil.Value > clock.CurrentMinute.Value);
                double score = sleep.SleepPoints + Math.Max(0, fatigue.Value - sleep.WakeFatigue) * sleep.SeverityWeight;
                if (!entitled)
                {
                    bool canRent = world.TryGet<GoldComponent>(npc, out var money) && money.Amount >= village.RentPerDay &&
                        world.TryGetEntity(village.InnId, out var inn) && inn is not null && world.Has<OwnershipComponent>(inn) &&
                        world.TryGet<BuildingComponent>(inn, out var beds) && beds is not null &&
                        snapshot.InnGuests.Count(e => world.TryGet<InnStayComponent>(e, out var rental) && rental.InnId == inn.Id && rental.PaidUntil.Value > clock.CurrentMinute.Value) < beds.Capacity;
                    if (canRent) AddChoice(AIActivity.RentBed, place, score, 1);
                    else if (visit is not null) { visit.Departing = true; AddChoice(AIActivity.LeaveVillage, village.EntranceId, score); }
                }
                else AddChoice(AIActivity.Sleep, place, score, 1);
            }
        }
        if (world.Has<SleepingComponent>(npc)) return;
        if (world.Has<CookingAttemptComponent>(npc))
        {
            double points = world.TryGet<CookingSettingsComponent>(npc, out var activeSettings) && activeSettings is not null
                ? activeSettings.CookingPoints : 0;
            world.TryGet<CookingAttemptComponent>(npc, out var batch);
            AddChoice(AIActivity.WaitForCooking, batch is not null && !batch.StationId.IsEmpty ? batch.StationId : village.InnId, points);
            return;
        }
        // Final journey shopping is independent of work hours. Survival and sleep retain their higher urgency.
        if (visit is not null && visit.PreparingDeparture)
        {
            var food = GameSettings.Find(world)?.Food ?? new FoodSettings();
            if (FoodProductionRules.JourneyCount(world, npc) < food.MerchantJourneyFoodTarget &&
                world.TryGet<GoldComponent>(npc, out var journeyWallet))
                foreach (Entity seller in snapshot.InventoryOwners)
                {
                    if (seller == npc || !world.TryGet<HealthComponent>(seller, out var sellerHealth) || sellerHealth.Current <= 0 ||
                        !world.TryGet<InventoryComponent>(seller, out var stock) || stock is null) continue;
                    foreach (Entity item in stock.Items)
                        if (FoodProductionRules.JourneyFood(world, item) && world.Has<SaleLocationComponent>(item) &&
                            world.TryGet<TradePriceComponent>(item, out var price) && price.GoldPerUnit > 0 && price.GoldPerUnit <= journeyWallet.Amount)
                            AddChoice(AIActivity.BuySupplies, item.Id, food.DepartureShoppingPoints - price.GoldPerUnit * settings.PurchaseCostWeight,
                                seller: seller.Id, quantity: 1);
                }
            return;
        }
        bool canProcess = world.TryGet<AIWorkSettingsComponent>(npc, out var processingWork) && processingWork is not null &&
            world.TryGet<ScheduleStateComponent>(npc, out var processingSchedule) && processingSchedule is not null &&
            processingSchedule.Stage == LifeStage.Adult && processingSchedule.Activity == ScheduledActivity.Work &&
            !needs.HealthCritical && !needs.HungerUrgent && !processingSchedule.PreparingFood &&
            (processingSchedule.WorkStarted || needs.HungerPercent >= processingWork.BeforeWorkHungerPercent);
        ProcessingAI.Add(world, npc, state, snapshot, choices, canProcess);
        if (!world.TryGet<AIWorkSettingsComponent>(npc, out var work) || work is null ||
            !world.TryGet<ScheduleStateComponent>(npc, out var schedule) || schedule is null ||
            schedule.Stage != LifeStage.Adult || schedule.Activity != ScheduledActivity.Work || needs.HealthCritical || needs.HungerUrgent ||
            schedule.PreparingFood || (!schedule.WorkStarted && needs.HungerPercent < work.BeforeWorkHungerPercent)) return;

        world.TryGet<CookingSettingsComponent>(npc, out var cooking);
        if (schedule.Job is Job.Cook or Job.Smoker && cooking is not null)
        {
            cooking.Validate();
            // Fresh meals follow actual unmet customers; the smoker's separate goal maintains long-lived stock.
            if (schedule.Job == Job.Cook && FoodProductionRules.FreshMealDemand(world, npc, snapshot.InventoryOwners) > 0)
            {
                for (int index = 0; index < cooking.Recipes.Count; index++)
                {
                    Recipe recipe = cooking.Recipes[index];
                    if (recipe.PreservesFood) continue; // Smoker production uses the existing preservation goal.
                    Entity? station = CookingRules.FindStation(world, npc, recipe, snapshot.ProcessingStations);
                    if (station is not null && CookingRules.CanStart(world, npc, recipe, requireNear: false, station: station))
                        AddChoice(AIActivity.CookMeal, station.Id, cooking.CookingPoints + work.ScheduleBonus, quantity: index);
                }
            }
            foreach (Entity item in bag.Items)
                if (!FoodRules.IsExpired(world, item) && world.Has<MealComponent>(item) && !world.Has<TradePriceComponent>(item)) AddChoice(AIActivity.OfferGoods, item.Id, work.OfferPoints + work.ScheduleBonus);
        }
        if (schedule.Job == Job.Merchant)
        {
            foreach (Entity item in bag.Items)
                if ((world.Has<PlantSeedComponent>(item) || world.Has<BaitComponent>(item) || world.Has<SpiceComponent>(item)) && !world.Has<TradePriceComponent>(item))
                    AddChoice(AIActivity.OfferGoods, item.Id, work.OfferPoints + work.ScheduleBonus);
            AddChoice(AIActivity.GoToMarket, village.MarketId, 1);
        }
        if (!world.TryGet<GoldComponent>(npc, out var wallet) || wallet.Amount < 0) return;
        int shoppingGold = visit is null ? wallet.Amount : Math.Max(0, wallet.Amount - FoodProductionRules.JourneyGoldReserve(world, npc));
        foreach (Entity seller in snapshot.InventoryOwners)
        {
            if (seller == npc || !world.TryGet<HealthComponent>(seller, out var health) || health.Current <= 0 ||
                !world.TryGet<InventoryComponent>(seller, out var stock) || stock is null) continue;
            foreach (Entity item in stock.Items)
            {
                if (!world.TryGet<TradePriceComponent>(item, out var price) || price.GoldPerUnit <= 0 || !world.Has<SaleLocationComponent>(item)) continue;
                int wanted = 0;
                if (schedule.Job == Job.Fisher && world.TryGet<BaitComponent>(item, out var bait) && BaitRules.Find(world, bait.BaitId) is not null)
                    wanted = work.BaitStockTarget - bag.Items.Where(e => world.TryGet<BaitComponent>(e, out var held) &&
                        BaitRules.Find(world, held.BaitId) is not null && !world.Has<SaleLocationComponent>(e)).Sum(e => InventoryStacks.Count(world, e));
                if (schedule.Job == Job.Cook && cooking is not null && world.Has<SpiceComponent>(item)) wanted = cooking.SpiceStockTarget - Count<SpiceComponent>(bag.Items);
                if (visit is not null && world.Has<FishComponent>(item)) wanted = visit.FishTarget - Count<FishComponent>(bag.Items);
                if (world.TryGet<PlantSeedComponent>(item, out var plantSeed) && schedule.Job == Job.Farmer)
                {
                    var definition = GameSettings.Find(world)?.Farming.Plants.FirstOrDefault(p => p.Id == plantSeed.PlantId);
                    if (definition is not null) wanted = definition.SeedStockTarget - bag.Items.Where(e => world.TryGet<PlantSeedComponent>(e, out var held) && held.PlantId == plantSeed.PlantId && !world.Has<SaleLocationComponent>(e)).Sum(e => InventoryStacks.Count(world, e));
                }
                if (world.TryGet<PlantProduceComponent>(item, out var produce) && visit is not null)
                {
                    if (GameSettings.Find(world) is GameSettings configured && configured.Products.TryGetValue(produce.ItemId, out var definition))
                        wanted = (int)Math.Max(0, definition.MerchantStockTarget - InventoryStacks.CountItem(world, bag.Items, produce.ItemId));
                }
                int quantity = Math.Min(Math.Min(wanted, InventoryStacks.Count(world, item)), shoppingGold / price.GoldPerUnit);
                if (quantity > 0) AddChoice(AIActivity.BuySupplies, item.Id,
                    work.IngredientBuyingPoints + work.ScheduleBonus - (double)quantity * price.GoldPerUnit * settings.PurchaseCostWeight,
                    seller: seller.Id, quantity: quantity);
            }
        }

        /// <summary>Counts unlisted ingredients so posted stock is not consumed remotely by recipes.</summary>
        /// <typeparam name="T">The ingredient component type.</typeparam>
        /// <param name="items">The inventory to inspect.</param>
        /// <returns>The available quantity.</returns>
        int Count<T>(IEnumerable<Entity> items) where T : IComponent => items.Where(e => world.Has<T>(e) && !world.Has<SaleLocationComponent>(e)).Sum(e => InventoryStacks.Count(world, e));

        /// <summary>Adds one non-failed choice with its configured continuation bonus.</summary>
        /// <param name="activity">The proposed activity.</param>
        /// <param name="target">The target entity ID.</param>
        /// <param name="points">Calculated points.</param>
        /// <param name="urgency">The survival category.</param>
        /// <param name="seller">An optional seller.</param>
        /// <param name="quantity">Purchase quantity or recipe index.</param>
        void AddChoice(AIActivity activity, EntityId target, double points, int urgency = 0, EntityId seller = default, int quantity = 1)
        {
            if (state.FailedTargets.ContainsKey(target) || !world.TryGetEntity(target, out var entity) || entity is null) return;
            if (state.Activity == activity && state.TargetId == target && state.SellerId == seller) points += settings.ContinuationBonus;
            choices.Add(new AIActionCandidate(activity, entity, seller, points, urgency, quantity));
        }
    }
}
