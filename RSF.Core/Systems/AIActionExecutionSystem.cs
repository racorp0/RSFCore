using RSF.Core.Commands;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Executes selected survival and job activities through existing commands and verifies their effects.</summary>
public sealed class AIActionExecutionSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly CommandDispatcher _dispatcher;
    // Only actor membership/order is cached. Decisions, stock, and command validation stay live.
    private List<Entity> _actors = new();
    private readonly HashSet<EntityId> _actorIds = new();

    /// <summary>Connects AI execution to the existing command pipeline.</summary>
    /// <param name="world">The world containing NPC decisions.</param>
    /// <param name="clock">The shared simulation clock.</param>
    /// <param name="dispatcher">The dispatcher with handlers registered for the NPCs' supported activities.</param>
    public AIActionExecutionSystem(World world, WorldClock clock, CommandDispatcher dispatcher)
    {
        _world = world;
        _clock = clock;
        _dispatcher = dispatcher;
    }

    /// <summary>Performs survival, retrieval, and work commands in world order, verifying partial food servings and purchase results.</summary>
    /// <param name="deltaTime">The simulation step; action spacing uses configured game minutes.</param>
    public void Update(float deltaTime)
    {
        foreach (Entity npc in GetActors())
        {
            if (!_world.TryGet<AISettingsComponent>(npc, out var settings) || settings is null ||
                !_world.TryGet<AIStateComponent>(npc, out var state) || state is null ||
                !state.Pending || _clock.CurrentMinute.Value < state.NextActionAt.Value)
            {
                continue;
            }
            state.Pending = false;
            if (_world.Has<DeadComponent>(npc) ||
                !_world.TryGet<HealthComponent>(npc, out var health) || health.Current <= 0)
            {
                state.Activity = AIActivity.Wait;
                state.LastResult = "Unable to act";
                continue;
            }

            if (_world.Has<OperatingProcessComponent>(npc) && state.Activity != AIActivity.OperateProcessing)
                _dispatcher.Dispatch(new PauseProcessingCommand(npc));
            if (state.Activity == AIActivity.Wait)
            {
                if (_world.Has<TravelComponent>(npc)) _dispatcher.Dispatch(new StopTravelCommand(npc));
                continue;
            }

            _world.TryGet<ScheduleStateComponent>(npc, out var schedule);
            if (state.Urgency >= 2 && schedule is not null) schedule.WorkStarted = false;
            if (_world.Has<FishingAttemptComponent>(npc) &&
                state.Activity != AIActivity.WaitForFishing && state.Activity != AIActivity.CancelFishing)
            {
                // Interruption is its own command; the survival action runs at the next allowed step.
                _dispatcher.Dispatch(new CancelFishingCommand(npc));
                state.LastResult = "Fishing interrupted before changing activity";
                state.NextDecisionAt = _clock.CurrentMinute.Add(settings.ActionInterval.Minutes);
                state.NextActionAt = state.NextDecisionAt;
                continue;
            }

            if (_world.Has<SleepingComponent>(npc) && state.Activity != AIActivity.Sleep)
            {
                _dispatcher.Dispatch(new WakeCommand(npc));
                state.NextDecisionAt = _clock.CurrentMinute.Add(settings.ActionInterval.Minutes);
                state.NextActionAt = state.NextDecisionAt;
                continue;
            }
            if (_world.Has<CookingAttemptComponent>(npc) && state.Activity != AIActivity.WaitForCooking && state.Activity != AIActivity.CancelCooking)
            {
                _dispatcher.Dispatch(new CancelCookingCommand(npc));
                state.NextDecisionAt = _clock.CurrentMinute.Add(settings.ActionInterval.Minutes);
                state.NextActionAt = state.NextDecisionAt;
                continue;
            }
            EntityId destination = VillageAIExecution.Destination(_world, npc, state);
            if (destination.IsEmpty && _world.Has<TravelComponent>(npc)) _dispatcher.Dispatch(new StopTravelCommand(npc));
            bool travelFailed = false;
            if (!destination.IsEmpty && !VillageRules.Near(_world, npc, destination))
            {
                if (_world.TryGetEntity(destination, out var place) && place is not null)
                    _dispatcher.Dispatch(new StartTravelCommand(npc, place));
                if (_world.Has<TravelComponent>(npc))
                {
                    state.Pending = true;
                    state.LastResult = "Traveling to activity location";
                    state.NextActionAt = _clock.CurrentMinute.Add(1);
                    continue;
                }
                travelFailed = true;
            }

            bool isWork = state.Activity >= AIActivity.Plow && state.Activity is not (AIActivity.PrepareFood or AIActivity.WithdrawGoods);
            bool healing = state.Activity == AIActivity.Heal || state.Activity == AIActivity.BuyHealing;
            bool buying = state.Activity == AIActivity.BuyHealing || state.Activity == AIActivity.BuyFood;
            bool success = false;
            bool handled = !travelFailed && VillageAIExecution.TryExecute(_world, _dispatcher, npc, state, out success);
            if (!travelFailed && !handled && isWork)
            {
                success = AIWorkExecution.Execute(_world, _dispatcher, npc, state);
            }
            else if (!travelFailed && !handled && _world.TryGetEntity(state.TargetId, out var item) && item is not null &&
                AISurvivalRules.CanUse(_world, item, healing) &&
                _world.TryGet<InventoryComponent>(npc, out var inventory) && inventory is not null)
            {
                if (buying && _world.TryGetEntity(state.SellerId, out var seller) && seller is not null)
                {
                    var beforeItems = inventory.Items.ToHashSet();
                    int units = RawFoodRules.Units(_world, item);
                    _dispatcher.Dispatch(new BuyItemCommand(npc, seller, item, units));
                    Entity? purchased = inventory.Items.Contains(item) ? item : inventory.Items.FirstOrDefault(candidate => !beforeItems.Contains(candidate));
                    success = purchased is not null && AISurvivalRules.CanUse(_world, purchased, healing);
                    if (success) state.TargetId = purchased!.Id;
                }
                else if (!buying && inventory.Items.Contains(item))
                {
                    int before = healing ? health.Current : GetHunger(npc);
                    int beforeQuantity = InventoryStacks.Count(_world, item);
                    if (_world.Has<TradePriceComponent>(item)) _dispatcher.Dispatch(new WithdrawOfferCommand(npc, item));
                    if (!_world.Has<SaleLocationComponent>(item)) _dispatcher.Dispatch(new UseItemCommand(npc, item));
                    _world.TryGet<HealthComponent>(npc, out var updatedHealth);
                    int after = healing ? updatedHealth.Current : GetHunger(npc);
                    // Item removal alone is insufficient: the selected need must actually improve.
                    success = (!inventory.Items.Contains(item) || InventoryStacks.Count(_world, item) < beforeQuantity) && after > before;
                }
            }

            state.NextActionAt = _clock.CurrentMinute.Add(settings.ActionInterval.Minutes);
            state.NextDecisionAt = state.Activity is AIActivity.OperateProcessing or AIActivity.WaitForFishing or AIActivity.WaitForCooking or AIActivity.Sleep or AIActivity.GoToMarket
                ? _clock.CurrentMinute.Add(settings.DecisionInterval.Minutes) : state.NextActionAt;
            if (success)
            {
                state.LastResult = isWork || state.Activity is AIActivity.PrepareFood or AIActivity.WithdrawGoods
                    ? $"{state.Activity} succeeded" : buying ? "Purchase succeeded" : "Item use succeeded";
                if (buying)
                {
                    // Keep the purchased target for the next step instead of shopping again.
                    state.Activity = healing ? AIActivity.Heal : AIActivity.Eat;
                    state.SellerId = EntityId.Empty;
                }
            }
            else
            {
                state.FailedTargets[state.TargetId] = _clock.CurrentMinute.Add(settings.RetryDelay.Minutes);
                state.LastResult = "Action failed; target waiting for retry";
                state.Activity = AIActivity.Wait;
                state.Score = 0;
                state.Urgency = 0;
            }
        }
    }

    /// <summary>Returns AI actors in world order, rebuilding only when indexed membership changes.</summary>
    /// <returns>A stable actor list for this update, safe when commands remove actors or create items.</returns>
    private List<Entity> GetActors()
    {
        List<Entity> current = _world.GetEntitiesWith<AISettingsComponent>().ToList();
        if (current.Count != _actorIds.Count || current.Any(actor => !_actorIds.Contains(actor.Id)))
        {
            _actorIds.Clear();
            foreach (Entity actor in current) _actorIds.Add(actor.Id);
            // Competing purchases retain the original world order, even if settings were attached in a different order.
            _actors = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _actorIds.Contains(entity.Id)).ToList();
        }
        return _actors;
    }

    /// <summary>Reads hunger without introducing another source of need values.</summary>
    /// <param name="npc">The NPC whose food effect is being verified.</param>
    /// <returns>The current hunger value, or zero if the component is absent.</returns>
    private int GetHunger(Entity npc)
    {
        return _world.TryGet<HungerComponent>(npc, out var hunger) ? hunger.Current : 0;
    }
}
