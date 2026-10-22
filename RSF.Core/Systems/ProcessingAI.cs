using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Adds shared processing choices for any working profession without embedding recipe or station names in AI.</summary>
internal static class ProcessingAI
{
    /// <summary>Checks whether a carried product should be kept for a feasible processing recipe instead of offered for sale.</summary>
    /// <param name="world">The NPC's world.</param>
    /// <param name="npc">The actor with inputs.</param>
    /// <param name="item">The candidate sale item.</param>
    /// <returns>True when an eligible recipe still needs this input.</returns>
    public static bool WantsToProcess(World world, Entity npc, Entity item)
    {
        var settings = GameSettings.Find(world);
        if (settings is null || !world.TryGet<PlantProduceComponent>(item, out var product)) return false;
        return settings.Processing.Recipes.Any(r => r.Inputs.Any(g => g.Options.Any(o => o.Kind == ProcessingItemKind.Item && o.ItemId == product.ItemId)) &&
            Wanted(world, npc, r) && ProcessingRules.Requirements(world, npc, r) && ProcessingRules.Plan(world, npc, r, out _));
    }

    /// <summary>Adds existing processing choices from shared job and station lists while preserving work eligibility and scores.</summary>
    /// <param name="world">The shared world.</param>
    /// <param name="npc">The actor choosing actions.</param>
    /// <param name="state">Decision continuation and failure state.</param>
    /// <param name="snapshot">World-ordered processing jobs and stations shared by due NPCs.</param>
    /// <param name="choices">The scored action list.</param>
    /// <param name="canWork">Whether normal work is currently permitted.</param>
    public static void Add(World world, Entity npc, AIStateComponent state, AIDecisionSnapshot snapshot, List<AIActionCandidate> choices, bool canWork)
    {
        var settings = GameSettings.Find(world);
        if (settings is null || !world.TryGet<AIWorkSettingsComponent>(npc, out var work) || work is null ||
            !world.TryGet<AISettingsComponent>(npc, out var stability) || stability is null) return;
        foreach (var host in snapshot.ProcessingJobs)
        {
            if (!world.TryGet<ProcessingJobComponent>(host, out var job) || job is null || job.OwnerId != npc.Id) continue;
            if (job.Completed) AddChoice(AIActivity.CollectProcessing, host, 0, job.Recipe);
            else if (canWork && job.Recipe.RequiresOperator && ProcessingRules.Requirements(world, npc, job.Recipe) && ProcessingRules.Host(world, npc, host, job.Recipe, false))
                AddChoice(AIActivity.OperateProcessing, host, 0, job.Recipe);
        }
        if (!canWork || world.Has<OperatingProcessComponent>(npc)) return;
        for (int index = 0; index < settings.Processing.Recipes.Count; index++)
        {
            var recipe = settings.Processing.Recipes[index];
            if (!Wanted(world, npc, recipe) || !ProcessingRules.Requirements(world, npc, recipe) || !ProcessingRules.Plan(world, npc, recipe, out _)) continue;
            IEnumerable<Entity> hosts = recipe.ProcessingStation is null ? new[] { npc } : snapshot.ProcessingStations;
            foreach (var host in hosts)
                if (!world.Has<ProcessingJobComponent>(host) && ProcessingRules.Host(world, npc, host, recipe, false)) AddChoice(AIActivity.StartProcessing, host, index, recipe);
        }

        /// <summary>Scores a processing choice using the existing priority and continuation settings.</summary>
        /// <param name="activity">The shared processing activity.</param>
        /// <param name="host">The job host to reach.</param>
        /// <param name="recipe">The job recipe used for seed production priority.</param>
        /// <param name="index">The recipe index for starts.</param>
        void AddChoice(AIActivity activity, Entity host, int index, ProcessingDefinition recipe)
        {
            if (state.FailedTargets.ContainsKey(host.Id)) return;
            double score = work.ProcessPoints + work.ScheduleBonus;
            // Low seed stocks must be replenished before all harvested inputs are processed into food ingredients.
            if (recipe.Outputs.SelectMany(g => g.Options).Any(o => o.Kind == ProcessingItemKind.Seed && o.ChancePercent > 0))
                score += work.SeedProductionBonusPoints;
            if (state.Activity == activity && state.TargetId == host.Id) score += stability.ContinuationBonus;
            choices.Add(new AIActionCandidate(activity, host, EntityId.Empty, score, 0, index));
        }
    }

    /// <summary>Stops seed extraction once the configured stock target is reached, while other processing remains resource-limited.</summary>
    /// <param name="world">The world containing definitions.</param>
    /// <param name="npc">The prospective owner.</param>
    /// <param name="recipe">The processing recipe.</param>
    /// <returns>True if at least one useful output is still wanted.</returns>
    private static bool Wanted(World world, Entity npc, ProcessingDefinition recipe)
    {
        if (!world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) return false;
        foreach (var output in recipe.Outputs.SelectMany(g => g.Options).Where(o => o.ChancePercent > 0))
        {
            if (output.Kind == ProcessingItemKind.Item) return true;
            var plant = GameSettings.Find(world)!.Farming.Plants.First(p => p.Id == output.PlantId);
            long seeds = bag.Items.Where(e => !world.Has<SaleLocationComponent>(e) && world.TryGet<PlantSeedComponent>(e, out var held) && held.PlantId == plant.Id).Sum(e => (long)InventoryStacks.Count(world, e));
            if (seeds < plant.SeedStockTarget) return true;
        }
        return false;
    }
}
