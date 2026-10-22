using RSF.Core.Configuration;
using RSF.Core.Commands;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Maps selected village activities to physical destinations and the existing command pipeline.</summary>
internal static class VillageAIExecution
{
    /// <summary>Finds physical destinations for survival cooking, retrieval, and work without changing the goal during travel.</summary>
    /// <param name="world">The village world.</param>
    /// <param name="npc">The acting NPC.</param>
    /// <param name="state">The selected activity.</param>
    /// <returns>The destination ID, or empty when no travel is required.</returns>
    public static EntityId Destination(World world, Entity npc, AIStateComponent state)
    {
        if (!world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null) return EntityId.Empty;
        if (state.Activity is AIActivity.StartProcessing or AIActivity.OperateProcessing or AIActivity.CollectProcessing)
            return state.TargetId == npc.Id ? EntityId.Empty : state.TargetId;
        if (state.Activity is AIActivity.WithdrawGoods or AIActivity.BuyFood or AIActivity.BuyHealing or AIActivity.BuyIngredients or AIActivity.BuySupplies)
            return world.TryGetEntity(state.TargetId, out var item) && item is not null && world.TryGet<SaleLocationComponent>(item, out var sale) ? sale.PlaceId : EntityId.Empty;
        if (state.Activity == AIActivity.OfferGoods)
            return world.TryGetEntity(state.TargetId, out var offered) && offered is not null ? VillageRules.SalePlace(world, npc, offered) : EntityId.Empty;
        if (state.Activity == AIActivity.WaitForCooking)
            return world.TryGet<CookingAttemptComponent>(npc, out var batch) && batch is not null && !batch.StationId.IsEmpty ? batch.StationId : resident.Village.InnId;
        if (state.Activity is AIActivity.CookMeal or AIActivity.PrepareFood) return state.TargetId;
        if (state.Activity is AIActivity.Sleep or AIActivity.RentBed or AIActivity.GoToMarket or AIActivity.LeaveVillage or
            AIActivity.PlantCrop or AIActivity.HarvestCrop or AIActivity.ClearCrop or AIActivity.Plow or AIActivity.Water or AIActivity.StartFishing) return state.TargetId;
        if (state.Activity is AIActivity.Eat or AIActivity.Heal)
            return world.TryGetEntity(state.TargetId, out var food) && food is not null && world.TryGet<SaleLocationComponent>(food, out var location) ? location.PlaceId : EntityId.Empty;
        return EntityId.Empty;
    }

    /// <summary>Handles station cooking, owned-listing retrieval, and purchases including preserved journey meals through existing commands.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="dispatcher">The shared gameplay command dispatcher.</param>
    /// <param name="npc">The acting NPC.</param>
    /// <param name="state">The selected action and target.</param>
    /// <param name="success">Whether the handled command had its expected effect.</param>
    /// <returns>True if this activity was handled here.</returns>
    public static bool TryExecute(World world, CommandDispatcher dispatcher, Entity npc, AIStateComponent state, out bool success)
    {
        success = false;
        if (!world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null) return false;
        world.TryGetEntity(state.TargetId, out var target);
        switch (state.Activity)
        {
            case AIActivity.StartProcessing:
                if (target is not null) dispatcher.Dispatch(new StartProcessingCommand(npc, target, state.Quantity));
                success = target is not null && world.TryGet<ProcessingJobComponent>(target, out var started) && started is not null && started.OwnerId == npc.Id;
                break;
            case AIActivity.OperateProcessing:
                if (target is not null) dispatcher.Dispatch(new OperateProcessingCommand(npc, target));
                success = world.TryGet<OperatingProcessComponent>(npc, out var operating) && operating.HostId == state.TargetId;
                break;
            case AIActivity.CollectProcessing:
                bool completed = target is not null && world.TryGet<ProcessingJobComponent>(target, out var finished) && finished is not null && finished.Completed && finished.OwnerId == npc.Id;
                if (completed) dispatcher.Dispatch(new CollectProcessingCommand(npc, target!));
                success = completed && !world.Has<ProcessingJobComponent>(target!);
                break;
            case AIActivity.CancelTravel:
                dispatcher.Dispatch(new StopTravelCommand(npc));
                success = !world.Has<TravelComponent>(npc);
                break;
            case AIActivity.CancelCooking:
                dispatcher.Dispatch(new CancelCookingCommand(npc));
                success = !world.Has<CookingAttemptComponent>(npc);
                break;
            case AIActivity.WithdrawGoods:
                if (target is not null) dispatcher.Dispatch(new WithdrawOfferCommand(npc, target));
                success = target is not null && world.TryGet<InventoryComponent>(npc, out var owned) && owned is not null &&
                    owned.Items.Contains(target) && !world.Has<TradePriceComponent>(target) && !world.Has<SaleLocationComponent>(target);
                break;
            case AIActivity.PrepareFood:
            case AIActivity.CookMeal:
                dispatcher.Dispatch(new StartCookingCommand(npc, state.Quantity, target));
                success = world.Has<CookingAttemptComponent>(npc);
                break;
            case AIActivity.WaitForCooking:
                success = world.Has<CookingAttemptComponent>(npc);
                break;
            case AIActivity.Sleep:
                if (target is not null && !world.Has<SleepingComponent>(npc)) dispatcher.Dispatch(new SleepCommand(npc, target));
                success = world.Has<SleepingComponent>(npc);
                break;
            case AIActivity.RentBed:
                if (target is not null) dispatcher.Dispatch(new RentBedCommand(npc, target));
                success = world.TryGet<InnStayComponent>(npc, out var stay) && stay.PaidUntil.Value > resident.Village.CurrentMinute.Value;
                break;
            case AIActivity.LeaveVillage:
                dispatcher.Dispatch(new LeaveVillageCommand(npc));
                success = !world.TryGetEntity(npc.Id, out _);
                break;
            case AIActivity.GoToMarket:
                success = VillageRules.Near(world, npc, resident.Village.MarketId);
                break;
            case AIActivity.OfferGoods:
                if (target is null || !world.TryGet<AIWorkSettingsComponent>(npc, out var work) || work is null) break;
                int price = world.Has<FishComponent>(target) ? FishPricing.SalePrice(world, target, work.FishPrice)
                    : world.TryGet<BaitComponent>(target, out var offeredBait) ? BaitRules.Price(world, offeredBait.BaitId, resident.Village.BaitPrice)
                    : world.Has<SpiceComponent>(target) ? resident.Village.SpicePrice : work.BreadPrice;
                if (world.TryGet<PlantSeedComponent>(target, out var cropSeeds))
                    price = GameSettings.Find(world)?.Farming.Plants.FirstOrDefault(p => p.Id == cropSeeds.PlantId)?.SeedPrice ?? 0;
                if (world.TryGet<PlantProduceComponent>(target, out var cropProduce))
                    price = GameSettings.Find(world) is GameSettings configured && configured.Products.TryGetValue(cropProduce.ItemId, out var output) ? output.SellPrice : 0;
                if (price <= 0) break;
                if (world.Has<MealComponent>(target) && world.TryGet<CookingSettingsComponent>(npc, out var cooking) && cooking is not null) price = cooking.MealPrice;
                // Keep the original base independently; fish weight is rounded only after the local adjustment.
                double basePrice = world.Has<FishComponent>(target) ? FishPricing.UnroundedSalePrice(world, target, work.FishPrice) : price;
                price = MarketPricing.QuoteItem(world, npc, target, basePrice);
                if (price <= 0) break;
                dispatcher.Dispatch(new OfferItemCommand(npc, target, price));
                success = world.TryGet<TradePriceComponent>(target, out var posted) && posted.GoldPerUnit == price;
                if (success) MarketPricing.MarkAutomatic(world, npc, target, basePrice);
                break;
            case AIActivity.BuySupplies:
            case AIActivity.BuyIngredients:
                if (target is null || !world.TryGetEntity(state.SellerId, out var seller) || seller is null ||
                    !world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) break;
                int before = MatchingQuantity();
                dispatcher.Dispatch(new BuyItemCommand(npc, seller, target, state.Quantity));
                success = MatchingQuantity() == before + state.Quantity;
                break;

                /// <summary>Verifies quantity transfers by identity, including partial bait stacks and individual journey meals.</summary>
                /// <returns>The count of the purchased item type.</returns>
                int MatchingQuantity()
                {
                    if (world.TryGet<PlantSeedComponent>(target, out var seed)) return bag.Items.Where(e => world.TryGet<PlantSeedComponent>(e, out var held) && held.PlantId == seed.PlantId).Sum(e => InventoryStacks.Count(world, e));
                    if (world.TryGet<PlantProduceComponent>(target, out var produce)) return bag.Items.Where(e => world.TryGet<PlantProduceComponent>(e, out var held) && held.ItemId == produce.ItemId).Sum(e => InventoryStacks.Count(world, e));
                    if (world.Has<FishComponent>(target)) return bag.Items.Count(e => world.Has<FishComponent>(e));
                    if (world.Has<MealComponent>(target)) return bag.Items.Count(e => world.Has<MealComponent>(e));
                    return bag.Items.Where(e =>
                        (world.TryGet<BaitComponent>(target, out var boughtBait) && world.TryGet<BaitComponent>(e, out var heldBait) && boughtBait.BaitId == heldBait.BaitId) ||
                        (world.Has<SpiceComponent>(target) && world.Has<SpiceComponent>(e))).Sum(e => InventoryStacks.Count(world, e));
                }
            default: return false;
        }
        if (success && (state.Activity is AIActivity.CookMeal or AIActivity.OfferGoods or AIActivity.BuyIngredients or AIActivity.BuySupplies) &&
            world.TryGet<ScheduleStateComponent>(npc, out var schedule) && schedule is not null) schedule.WorkStarted = true;
        return true;
    }
}
