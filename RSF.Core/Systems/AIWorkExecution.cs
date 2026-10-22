using RSF.Core.Configuration;
using RSF.Core.Commands;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Executes one selected job action through existing handlers and verifies the resulting world state.</summary>
internal static class AIWorkExecution
{
    /// <summary>Executes existing job commands, including cook or smoker ingredient purchases, with local sale-price validation.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="dispatcher">The registered gameplay command dispatcher.</param>
    /// <param name="npc">The worker performing the action.</param>
    /// <param name="state">The selected activity and target.</param>
    /// <returns>True if the action had its expected result.</returns>
    public static bool Execute(World world, CommandDispatcher dispatcher, Entity npc, AIStateComponent state)
    {
        if (state.Activity == AIActivity.CancelFishing)
        {
            dispatcher.Dispatch(new CancelFishingCommand(npc));
            return !world.Has<FishingAttemptComponent>(npc);
        }
        if (!world.TryGet<AIWorkSettingsComponent>(npc, out var work) || work is null ||
            !world.TryGet<ScheduleStateComponent>(npc, out var schedule) || schedule is null ||
            schedule.Activity != ScheduledActivity.Work || schedule.Stage != LifeStage.Adult || schedule.Job == Job.None ||
            !world.TryGet<NeedsStateComponent>(npc, out var needs) || needs.HealthCritical || needs.HungerUrgent ||
            (!schedule.WorkStarted && needs.HungerPercent < work.BeforeWorkHungerPercent) ||
            !world.TryGet<InventoryComponent>(npc, out var inventory) || inventory is null ||
            !world.TryGetEntity(state.TargetId, out var target) || target is null) return false;

        bool success = false;
        switch (state.Activity)
        {
            case AIActivity.Plow:
                if (schedule.Job != Job.Farmer || !OwnsPlot()) break;
                dispatcher.Dispatch(new PlowCommand(npc, target));
                success = world.Has<FarmPlotComponent>(target);
                break;
            case AIActivity.PlantCrop:
                if (schedule.Job != Job.Farmer || !OwnsPlot()) break;
                var plants = GameSettings.Find(world)?.Farming.Plants;
                if (plants is null || state.Quantity < 0 || state.Quantity >= plants.Count || world.Has<PlantCropComponent>(target)) break;
                var definition = plants[state.Quantity];
                Entity? plantSeeds = inventory.Items.FirstOrDefault(e => world.TryGet<PlantSeedComponent>(e, out var seed) && seed.PlantId == definition.Id && seed.Quantity >= definition.SeedsPerPlanting && !world.Has<SaleLocationComponent>(e));
                if (plantSeeds is null) break;
                dispatcher.Dispatch(new PlantCropCommand(npc, target, plantSeeds));
                success = world.TryGet<PlantCropComponent>(target, out var planted) && planted.Definition.Id == definition.Id;
                break;
            case AIActivity.HarvestCrop:
                if (schedule.Job != Job.Farmer || !OwnsPlot() || !world.TryGet<PlantCropComponent>(target, out var ready) || !ready.IsMature) break;
                int previousHarvests = ready.HarvestCount;
                dispatcher.Dispatch(new HarvestCropCommand(npc, target));
                success = !world.Has<PlantCropComponent>(target) || (world.TryGet<PlantCropComponent>(target, out var harvested) && harvested.HarvestCount == previousHarvests + 1);
                break;
            case AIActivity.ClearCrop:
                if (schedule.Job != Job.Farmer || !OwnsPlot() || !world.TryGet<PlantCropComponent>(target, out var dry) || !dry.IsDry) break;
                dispatcher.Dispatch(new ClearCropCommand(npc, target));
                success = !world.Has<PlantCropComponent>(target) && !world.Has<FarmPlotComponent>(target);
                break;
            case AIActivity.Water:
                if (schedule.Job != Job.Farmer || !OwnsPlot()) break;
                Entity? can = inventory.Items.FirstOrDefault(item => world.Has<WateringCanComponent>(item));
                if (can is null) break;
                if (world.TryGet<PlantCropComponent>(target, out var growingPlant))
                {
                    if (growingPlant.WasWatered || growingPlant.IsDry || growingPlant.IsMature) break;
                    dispatcher.Dispatch(new WaterCommand(npc, target, can));
                    success = world.TryGet<PlantCropComponent>(target, out var wateredPlant) && wateredPlant.WasWatered;
                    break;
                }
                break;
            case AIActivity.StartFishing:
                if (schedule.Job != Job.Fisher) break;
                Entity? rod = inventory.Items.FirstOrDefault(item => world.Has<FishingRodComponent>(item));
                Entity? bait = inventory.Items.FirstOrDefault(item => world.TryGet<BaitComponent>(item, out var supply) && supply.Quantity > 0 && !world.Has<SaleLocationComponent>(item) &&
                    world.TryGet<RiverComponent>(target, out var water) && water is not null && BaitRules.CanUse(world, water, supply.BaitId));
                if (rod is null || bait is null || world.Has<FishingAttemptComponent>(npc)) break;
                dispatcher.Dispatch(new StartFishingCommand(npc, target, rod, bait));
                success = world.Has<FishingAttemptComponent>(npc);
                break;
            case AIActivity.WaitForFishing:
                success = schedule.Job == Job.Fisher && world.Has<FishingAttemptComponent>(npc);
                break;
            case AIActivity.OfferGoods:
                int price = world.Has<FishComponent>(target) && schedule.Job == Job.Fisher ? FishPricing.SalePrice(world, target, work.FishPrice)
                    : world.TryGet<ItemComponent>(target, out var itemData) && itemData.Name == "Bread" && schedule.Job == Job.Cook ? work.BreadPrice : 0;
                if (price <= 0) break;
                // Keep the original base independently; fish weight is rounded only after the local adjustment.
                double basePrice = world.Has<FishComponent>(target) ? FishPricing.UnroundedSalePrice(world, target, work.FishPrice) : price;
                price = MarketPricing.QuoteItem(world, npc, target, basePrice);
                if (price <= 0) break;
                dispatcher.Dispatch(new OfferItemCommand(npc, target, price));
                success = world.TryGet<TradePriceComponent>(target, out var offered) && offered.GoldPerUnit == price;
                if (success) MarketPricing.MarkAutomatic(world, npc, target, basePrice);
                break;
            case AIActivity.BuyIngredients:
                if (schedule.Job is not (Job.Cook or Job.Smoker) || !world.TryGetEntity(state.SellerId, out var seller) || seller is null) break;
                string? itemId = world.TryGet<PlantProduceComponent>(target, out var boughtProduct) ? boughtProduct.ItemId : null;
                long before = itemId is null ? 0 : InventoryStacks.CountItem(world, inventory.Items, itemId);
                dispatcher.Dispatch(new BuyItemCommand(npc, seller, target, state.Quantity));
                success = world.Has<FishComponent>(target) ? inventory.Items.Contains(target) : itemId is not null && InventoryStacks.CountItem(world, inventory.Items, itemId) == before + state.Quantity;
                break;
        }
        if (success) schedule.WorkStarted = true;
        return success;

        /// <summary>Prevents acting on a different farmer's assigned plot.</summary>
        /// <returns>True when the selected plot belongs to this NPC.</returns>
        bool OwnsPlot() => world.TryGet<OwnershipComponent>(target, out var owner) && owner is not null && owner.OwnerId == npc.Id;

    }
}
