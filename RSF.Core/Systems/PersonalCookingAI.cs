using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Adds self-feeding choices when ready food is unavailable, reusing cooking validation and normal AI scores.</summary>
internal static class PersonalCookingAI
{
    /// <summary>Plans a complete recipe, including physical retrieval of owned sale inputs, without consuming anything.</summary>
    /// <param name="world">The NPC's world.</param>
    /// <param name="npc">The hungry actor.</param>
    /// <param name="state">Current activity and failed targets.</param>
    /// <param name="settings">Existing survival scores and preparation penalty.</param>
    /// <param name="snapshot">The existing shared station candidates.</param>
    /// <param name="choices">Survival and work candidates for this decision.</param>
    /// <param name="score">The already calculated food-need score.</param>
    /// <param name="urgency">The food need's normal urgency category.</param>
    public static void Add(World world, Entity npc, AIStateComponent state, AISettingsComponent settings,
        AIDecisionSnapshot snapshot, List<AIActionCandidate> choices, double score, int urgency)
    {
        if (!world.TryGet<CookingSettingsComponent>(npc, out var cooking) || cooking is null) return;
        if (world.TryGet<CookingAttemptComponent>(npc, out var batch) && batch is not null)
        {
            EntityId hostId = batch.StationId;
            if (hostId.IsEmpty && world.TryGet<VillageResidentComponent>(npc, out var resident) && resident is not null) hostId = resident.Village.InnId;
            if (world.TryGetEntity(hostId, out var host) && host is not null)
                choices.Add(new AIActionCandidate(AIActivity.WaitForCooking, host, EntityId.Empty, score + settings.ContinuationBonus, urgency));
            return;
        }
        // Keep existing ready-food choices. Preparation is a fallback, rather than a replacement shopping policy.
        if (choices.Any(choice => choice.Activity is AIActivity.Eat or AIActivity.BuyFood)) return;
        for (int index = 0; index < cooking.Recipes.Count; index++)
        {
            Recipe recipe = cooking.Recipes[index];
            if (!recipe.Outputs.Any(group => group.Options.Any(output => output.Hunger > 0 && output.ChancePercent > 0)) ||
                !ProcessingRules.Requirements(world, npc, recipe)) continue;
            Entity? station = CookingRules.FindStation(world, npc, recipe, snapshot.ProcessingStations);
            if (station is null || state.FailedTargets.ContainsKey(station.Id) || !CookingRules.KitchenAccess(world, npc, recipe, false, station)) continue;
            double points = Math.Max(0, score - settings.FoodPreparationPenaltyPoints - recipe.ProcessingMinutes * settings.FoodPreparationTimeWeight);
            if (ProcessingRules.Plan(world, npc, recipe, out _))
            {
                if (state.Activity == AIActivity.PrepareFood && state.TargetId == station.Id && state.Quantity == index && state.Urgency == urgency) points += settings.ContinuationBonus;
                choices.Add(new AIActionCandidate(AIActivity.PrepareFood, station, EntityId.Empty, points, urgency, index));
            }
            else if (ProcessingRules.Plan(world, npc, recipe, out var spending, includeListed: true))
            {
                // This look-ahead never spends posted goods. A command must retrieve them at the counter first.
                Entity? item = spending.Keys.FirstOrDefault(item => world.Has<SaleLocationComponent>(item) && !state.FailedTargets.ContainsKey(item.Id));
                if (item is not null)
                {
                    if (state.Activity == AIActivity.WithdrawGoods && state.TargetId == item.Id) points += settings.ContinuationBonus;
                    choices.Add(new AIActionCandidate(AIActivity.WithdrawGoods, item, EntityId.Empty, points, urgency));
                }
            }
        }
    }
    /// <summary>Adds bounded commercial preservation only for well-fed smokers during their scheduled work.</summary>
    /// <param name="world">The actor's world.</param>
    /// <param name="npc">The actor planning preservation.</param>
    /// <param name="needs">Current health and hunger priorities.</param>
    /// <param name="state">Current action and failed targets.</param>
    /// <param name="settings">Configurable preservation points and continuity.</param>
    /// <param name="snapshot">Shared station candidates.</param>
    /// <param name="choices">The existing scored decision choices.</param>
    public static void AddPreservation(World world, Entity npc, NeedsStateComponent needs, AIStateComponent state,
        AISettingsComponent settings, AIDecisionSnapshot snapshot, List<AIActionCandidate> choices)
    {
        var game = RSF.Core.Configuration.GameSettings.Find(world);
        if (game is null || game.Food.PreservedFoodStockTarget == 0 || needs.SeekFood || needs.SeekHealing || needs.HealthCritical || needs.HungerUrgent ||
            !world.TryGet<AIWorkSettingsComponent>(npc, out var work) || work is null || needs.HungerPercent < work.BeforeWorkHungerPercent ||
            !world.TryGet<ScheduleStateComponent>(npc, out var schedule) || schedule is null || schedule.Stage != LifeStage.Adult || schedule.Job != Job.Smoker ||
            schedule.Activity != ScheduledActivity.Work || schedule.PreparingFood ||
            world.Has<SleepingComponent>(npc) || world.Has<FishingAttemptComponent>(npc) ||
            !world.TryGet<CookingSettingsComponent>(npc, out var cooking) || cooking is null) return;
        if (world.TryGet<CookingAttemptComponent>(npc, out var active) && active is not null)
        {
            if (active.Recipe.PreservesFood && world.TryGetEntity(active.StationId, out var host) && host is not null)
                choices.Add(new AIActionCandidate(AIActivity.WaitForCooking, host, EntityId.Empty, settings.FoodPreservationPoints + settings.ContinuationBonus, 0));
            return;
        }
        // Publish each finished batch before starting another. Otherwise the higher production score would hide food until the stock target is full.
        if (world.TryGet<InventoryComponent>(npc, out var stock) && stock is not null && stock.Items.Any(item =>
            FoodProductionRules.IsPreserved(world, item) && AISurvivalRules.CanUse(world, item, false) && !world.Has<TradePriceComponent>(item))) return;
        if (FoodProductionRules.JourneyCount(world, npc) >= game.Food.PreservedFoodStockTarget) return;
        for (int index = 0; index < cooking.Recipes.Count; index++)
        {
            Recipe recipe = cooking.Recipes[index];
            if (!recipe.PreservesFood || !ProcessingRules.Requirements(world, npc, recipe)) continue;
            Entity? station = CookingRules.FindStation(world, npc, recipe, snapshot.ProcessingStations);
            if (station is null || state.FailedTargets.ContainsKey(station.Id) || !CookingRules.KitchenAccess(world, npc, recipe, false, station)) continue;
            int multiplier = 1 + game.Food.PreservationReserveBatches;
            if (recipe.Inputs.Any(group => group.Options.Any(option => (long)option.Quantity * multiplier > int.MaxValue))) continue;
            // Plan several batches without spending them: one for preservation, the rest remain as fresh reserves.
            Recipe reservePlan = recipe with { Inputs = recipe.Inputs.Select(group => new ProcessingInputGroup
                { Options = group.Options.Select(option => option with { Quantity = option.Quantity * multiplier }).ToList() }).ToList() };
            if (!ProcessingRules.Plan(world, npc, reservePlan, out _, includeListed: true)) continue;
            double points = settings.FoodPreservationPoints;
            if (ProcessingRules.Plan(world, npc, recipe, out _))
            {
                if (state.Activity == AIActivity.PrepareFood && state.TargetId == station.Id && state.Quantity == index) points += settings.ContinuationBonus;
                choices.Add(new AIActionCandidate(AIActivity.PrepareFood, station, EntityId.Empty, points, 0, index));
            }
            else if (ProcessingRules.Plan(world, npc, recipe, out var spending, includeListed: true))
            {
                Entity? item = spending.Keys.FirstOrDefault(item => world.Has<SaleLocationComponent>(item) && !state.FailedTargets.ContainsKey(item.Id));
                if (item is not null)
                {
                    if (state.Activity == AIActivity.WithdrawGoods && state.TargetId == item.Id) points += settings.ContinuationBonus;
                    choices.Add(new AIActionCandidate(AIActivity.WithdrawGoods, item, EntityId.Empty, points, 0));
                }
            }
        }
    }

}
