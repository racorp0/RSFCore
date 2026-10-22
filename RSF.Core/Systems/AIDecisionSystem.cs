using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Scores survival and scheduled work together while retaining targets and respecting retry times.</summary>
public sealed class AIDecisionSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    /// <summary>Connects decisions to the existing world and game-minute clock.</summary>
    /// <param name="world">The world holding NPC needs and available resources.</param>
    /// <param name="clock">The shared simulation clock.</param>
    public AIDecisionSystem(World world, WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Scores survival, demand-led cooking, smoker work, and journey shopping while retaining stable travel goals.</summary>
    /// <param name="deltaTime">The simulation step; review times use game minutes.</param>
    public void Update(float deltaTime)
    {
        // No terrain scan or candidate snapshot is needed when every NPC is waiting or travelling.
        AIDecisionSnapshot? snapshot = null;
        foreach (Entity npc in _world.GetEntitiesWith<AISettingsComponent>().ToList())
        {
            if (!_world.TryGet<AISettingsComponent>(npc, out var settings) || settings is null ||
                !_world.TryGet<NeedsSettingsComponent>(npc, out var needsSettings) || needsSettings is null)
            {
                continue;
            }
            settings.Validate();
            if (!_world.TryGet<AIStateComponent>(npc, out var state) || state is null)
            {
                state = new AIStateComponent();
                _world.Add(npc, state);
            }
            if (_world.Has<DeadComponent>(npc) ||
                !_world.TryGet<HealthComponent>(npc, out var health) || health.Current <= 0 ||
                !_world.TryGet<NeedsStateComponent>(npc, out var needs))
            {
                state.Pending = false;
                state.Activity = AIActivity.Wait;
                continue;
            }

            _world.TryGet<ScheduleStateComponent>(npc, out var schedule);
            bool scheduleChanged = schedule is not null && schedule.Revision != state.ScheduleRevision;
            if (schedule is not null) state.ScheduleRevision = schedule.Revision;
            bool dependent = _world.TryGet<LifeStageComponent>(npc, out var life) &&
                (life.Stage == LifeStage.Baby || life.Stage == LifeStage.Toddler);
            if (dependent)
            {
                state.Activity = _world.Has<FishingAttemptComponent>(npc) ? AIActivity.CancelFishing
                    : _world.Has<CookingAttemptComponent>(npc) ? AIActivity.CancelCooking
                    : _world.Has<TravelComponent>(npc) ? AIActivity.CancelTravel : AIActivity.Wait;
                state.TargetId = npc.Id;
                state.Pending = state.Activity != AIActivity.Wait;
                state.LastResult = "Requires caregiver; independent survival actions unavailable";
                continue;
            }

            bool newUrgency = (needs.HealthCritical && !state.PreviousHealthCritical) ||
                (needs.HungerUrgent && !state.PreviousHungerUrgent);
            state.PreviousHealthCritical = needs.HealthCritical;
            state.PreviousHungerUrgent = needs.HungerUrgent;
            if (newUrgency || scheduleChanged)
            {
                // Urgent needs and changed job schedules can interrupt the ordinary action cooldown.
                state.NextActionAt = _clock.CurrentMinute;
            }
            bool journeyChange = _world.TryGet<MerchantVisitComponent>(npc, out var visit) && visit is not null &&
                (visit.Departing ? state.Activity != AIActivity.LeaveVillage : visit.PreparingDeparture && state.Activity is not
                    (AIActivity.BuySupplies or AIActivity.Eat or AIActivity.BuyFood or AIActivity.Heal or AIActivity.BuyHealing or AIActivity.Sleep or AIActivity.RentBed));
            if (!newUrgency && !scheduleChanged && !journeyChange && _world.Has<TravelComponent>(npc))
            {
                // Keep a route's goal stable; urgent needs and schedule changes may still interrupt it.
                state.Pending = true;
                continue;
            }
            if (!newUrgency && !scheduleChanged && _clock.CurrentMinute.Value < state.NextDecisionAt.Value)
            {
                continue;
            }

            foreach (var failure in state.FailedTargets.ToList())
            {
                if (_clock.CurrentMinute.Value >= failure.Value.Value)
                {
                    state.FailedTargets.Remove(failure.Key);
                }
            }

            // All due NPCs share these lists. Their order matches the old world scan for equal-score choices.
            snapshot ??= new AIDecisionSnapshot(_world);
            List<AIActionCandidate> candidates = new();
            if (needs.SeekHealing)
            {
                double score = settings.HealingPoints +
                    Math.Max(0, needsSettings.HealthyPercent - needs.HealthPercent) * settings.HealthSeverityWeight;
                AddCandidates(npc, true, needs.HealthCritical ? 3 : 1, score, state, settings, snapshot, candidates);
            }
            _world.TryGet<AIWorkSettingsComponent>(npc, out var workSettings);
            bool prepareFood = false;
            bool breakFood = false;
            if (schedule is not null && workSettings is not null)
            {
                workSettings.Validate();
                if (schedule.Activity == ScheduledActivity.Work && !schedule.WorkStarted &&
                    needs.HungerPercent < workSettings.BeforeWorkHungerPercent)
                    schedule.PreparingFood = true;
                if (needs.HungerPercent >= workSettings.BeforeWorkHungerPercent)
                    schedule.PreparingFood = false;
                prepareFood = schedule.Activity == ScheduledActivity.Work && schedule.PreparingFood;
                breakFood = schedule.Activity == ScheduledActivity.MealBreak &&
                    needs.HungerPercent < workSettings.BreakHungerPercent;
            }
            if (needs.SeekFood || prepareFood || breakFood)
            {
                double eatingPoints = needs.SeekFood ? settings.EatingPoints : 0;
                double target = needsSettings.SatisfiedHungerPercent;
                if (prepareFood && workSettings is not null)
                {
                    eatingPoints = Math.Max(eatingPoints, workSettings.PreparationEatingPoints);
                    target = Math.Max(target, workSettings.BeforeWorkHungerPercent);
                }
                if (breakFood && workSettings is not null)
                {
                    eatingPoints = Math.Max(eatingPoints, workSettings.BreakEatingPoints);
                    target = Math.Max(target, workSettings.BreakHungerPercent);
                }
                double score = eatingPoints + Math.Max(0, target - needs.HungerPercent) * settings.HungerSeverityWeight;
                AddCandidates(npc, false, needs.HungerUrgent ? 2 : 1, score, state, settings, snapshot, candidates);
                PersonalCookingAI.Add(_world, npc, state, settings, snapshot, candidates, score, needs.HungerUrgent ? 2 : 1);
            }

            if (schedule is not null && workSettings is not null)
                AIWorkDecision.AddCandidates(_world, _clock, npc, needs, schedule, workSettings, settings, state, snapshot, candidates);

            VillageAIDecision.Add(_world, _clock, npc, needs, state, settings, snapshot, candidates);
            PersonalCookingAI.AddPreservation(_world, npc, needs, state, settings, snapshot, candidates);

            // Urgency is compared first: no amount of ordinary preference points can beat critical HP.
            AIActionCandidate? best = null;
            AIActionCandidate? current = null;
            foreach (AIActionCandidate candidate in candidates)
            {
                if (candidate.Activity == state.Activity && candidate.Item.Id == state.TargetId &&
                    candidate.SellerId == state.SellerId &&
                    (candidate.Activity is not (AIActivity.PrepareFood or AIActivity.CookMeal) || candidate.Quantity == state.Quantity))
                {
                    current = candidate;
                }
                if (best is null || candidate.Urgency > best.Urgency ||
                    (candidate.Urgency == best.Urgency && candidate.Score > best.Score))
                {
                    best = candidate;
                }
            }
            // A newly urgent need can replace a planned slow preservation recipe before it starts.
            if (!newUrgency && current is not null && best is not null && current.Urgency == best.Urgency &&
                best.Score <= current.Score + settings.SwitchMargin)
            {
                best = current;
            }

            state.NextDecisionAt = _clock.CurrentMinute.Add(settings.DecisionInterval.Minutes);
            if (best is null)
            {
                state.Activity = AIActivity.Wait;
                // Let execution stop an obsolete route even when no replacement action is feasible.
                state.Pending = _world.Has<TravelComponent>(npc);
                state.TargetId = EntityId.Empty;
                state.SellerId = EntityId.Empty;
                state.Score = 0;
                state.Urgency = 0;
                state.LastResult = needs.SeekHealing || needs.SeekFood || prepareFood || breakFood
                    ? "Needs blocked: no usable resource" : "No eligible work; following free time or waiting for resources";
                if (needs.SeekHealing || needs.SeekFood || prepareFood || breakFood)
                {
                    state.NextDecisionAt = _clock.CurrentMinute.Add(settings.RetryDelay.Minutes);
                }
                continue;
            }

            state.Activity = best.Activity;
            state.TargetId = best.Item.Id;
            state.SellerId = best.SellerId;
            state.Score = best.Score;
            state.Urgency = best.Urgency;
            state.Quantity = best.Quantity;
            state.Pending = true;
        }
    }

    /// <summary>Scores usable survival items, preferring fresh food for visitors while keeping smoked meals available in emergencies.</summary>
    /// <param name="npc">The NPC choosing an action.</param>
    /// <param name="healing">Whether to search for healing rather than food.</param>
    /// <param name="urgency">The need category, compared before points.</param>
    /// <param name="baseScore">Priority points plus need severity.</param>
    /// <param name="state">The NPC's current decision and failed targets.</param>
    /// <param name="settings">Per-NPC score settings.</param>
    /// <param name="snapshot">Filtered candidates shared by all due NPCs in this update.</param>
    /// <param name="candidates">The list receiving feasible actions.</param>
    private void AddCandidates(Entity npc, bool healing, int urgency, double baseScore,
        AIStateComponent state, AISettingsComponent settings, AIDecisionSnapshot snapshot, List<AIActionCandidate> candidates)
    {
        if (!_world.TryGet<InventoryComponent>(npc, out var inventory) || inventory is null)
        {
            return;
        }
        // Stable ordering breaks equal food-score ties without overriding urgency or continuation points.
        IEnumerable<Entity> carried = healing ? inventory.Items : inventory.Items.OrderBy(item => FoodRules.Deadline(_world, item));
        foreach (Entity item in carried)
        {
            Add(item, EntityId.Empty, 0);
        }
        if (!_world.TryGet<GoldComponent>(npc, out var wallet) || wallet.Amount < 0)
        {
            return;
        }
        foreach (AISurvivalOffer offer in snapshot.GetSurvivalOffers(_world, healing))
        {
            Entity seller = offer.Seller;
            if (seller == npc || _world.Has<DeadComponent>(seller) ||
                !_world.TryGet<HealthComponent>(seller, out var hp) || hp.Current <= 0 ||
                !_world.TryGet<GoldComponent>(seller, out var gold) || gold.Amount < 0 ||
                !_world.TryGet<InventoryComponent>(seller, out var stock) || stock is null)
            {
                continue;
            }
            foreach (Entity item in offer.Items)
            {
                // Only buyer-independent item usability is shared; keep existing price, ownership, and equipment exclusions.
                if (inventory.Items.Contains(item) ||
                    !_world.TryGet<TradePriceComponent>(item, out var price) || price.GoldPerUnit <= 0 ||
                    (long)price.GoldPerUnit * RawFoodRules.Units(_world, item) > wallet.Amount ||
                    (long)gold.Amount + (long)price.GoldPerUnit * RawFoodRules.Units(_world, item) > int.MaxValue ||
                    _world.Has<PlantSeedComponent>(item) || _world.Has<BaitComponent>(item) ||
                    (_world.Has<PlantProduceComponent>(item) && !_world.Has<FoodServingComponent>(item)))
                {
                    continue;
                }
                if (_world.TryGet<EquipmentComponent>(seller, out var equipment) &&
                    equipment is not null && equipment.Weapon == item)
                {
                    continue;
                }
                Add(item, seller.Id, (long)price.GoldPerUnit * RawFoodRules.Units(_world, item), knownUsable: true);
            }
        }

        /// <summary>Scores one eligible item, preserving an existing target when its value is similar.</summary>
        /// <param name="item">The candidate item.</param>
        /// <param name="sellerId">Empty for a carried item; otherwise the seller.</param>
        /// <param name="cost">The purchase price, or zero for a carried item.</param>
        /// <param name="knownUsable">True only for offers checked by this update's survival snapshot; carried items use live checks.</param>
        void Add(Entity item, EntityId sellerId, long cost, bool knownUsable = false)
        {
            if (state.FailedTargets.ContainsKey(item.Id) || (!knownUsable && !AISurvivalRules.CanUse(_world, item, healing)))
            {
                return;
            }
            AIActivity activity = sellerId.IsEmpty
                ? (healing ? AIActivity.Heal : AIActivity.Eat)
                : (healing ? AIActivity.BuyHealing : AIActivity.BuyFood);
            double score = baseScore - cost * settings.PurchaseCostWeight;
            // Preserve journey rations when fresh meals can satisfy the same need; this never outranks critical health.
            if (!healing && _world.Has<MerchantVisitComponent>(npc) && FoodProductionRules.IsPreserved(_world, item))
                score -= RSF.Core.Configuration.GameSettings.Find(_world)?.Food.FreshFoodPreferencePoints ?? 100;
            if (state.Activity == activity && state.TargetId == item.Id && state.SellerId == sellerId)
            {
                score += settings.ContinuationBonus;
            }
            candidates.Add(new AIActionCandidate(activity, item, sellerId, score, urgency));
        }
    }

}
