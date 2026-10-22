using RSF.Core.Blueprints;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Runs shared manual and automatic jobs, preserving inputs, progress, machinery reservations, and rolled output until collection.</summary>
public sealed class ProcessingSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly TravelSystem _travel;
    private readonly ProcessingSettings _settings;
    private readonly Random _random;
    // Preserve host order for shared operators and the random generator used by output groups.
    private List<Entity> _hosts = new();
    private readonly HashSet<EntityId> _hostIds = new();
    /// <summary>Connects processing to existing world storage, travel, and elapsed game time.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="clock">The shared game clock.</param>
    /// <param name="travel">Existing travel operations.</param>
    /// <param name="random">Optional repeatable random source for output tests.</param>
    public ProcessingSystem(World world, WorldClock clock, TravelSystem travel, Random? random = null)
    { _world = world; _clock = clock; _travel = travel; _settings = GameSettings.Find(world)?.Processing ?? new(); _random = random ?? new Random(); }

    /// <summary>Validates all requirements and spends a complete shared reservation, including typed fish or spice inputs, before starting a job.</summary>
    /// <param name="npc">The NPC activating the recipe.</param>
    /// <param name="host">The machinery, or this NPC for a station-free manual job.</param>
    /// <param name="recipeIndex">The recipe index in processing.json.</param>
    /// <returns>True only if a new job starts.</returns>
    public bool Start(Entity npc, Entity host, int recipeIndex)
    {
        if (recipeIndex < 0 || recipeIndex >= _settings.Recipes.Count || _world.Has<ProcessingJobComponent>(host) || Busy(npc)) return false;
        var recipe = _settings.Recipes[recipeIndex];
        if (!ProcessingRules.Requirements(_world, npc, recipe) || !ProcessingRules.Host(_world, npc, host, recipe) ||
            !ProcessingRules.Plan(_world, npc, recipe, out var spending)) return false;
        // The shared planner has reserved every input group before any stack is changed.
        ProcessingRules.Spend(_world, npc, spending);
        _world.Add(host, new ProcessingJobComponent(recipe, npc.Id, _clock.CurrentMinute));
        if (recipe.RequiresOperator) { _travel.Stop(npc); _world.Add(npc, new OperatingProcessComponent(host.Id)); }
        return true;
    }

    /// <summary>Resumes a paused manual job without spending inputs again.</summary>
    /// <param name="npc">The original job owner.</param>
    /// <param name="host">The entity storing the job.</param>
    /// <returns>True when this NPC can actively operate the job.</returns>
    public bool Operate(Entity npc, Entity host)
    {
        if (!_world.TryGet<ProcessingJobComponent>(host, out var job) || job is null || job.Completed || !job.Recipe.RequiresOperator ||
            job.OwnerId != npc.Id || !ProcessingRules.Requirements(_world, npc, job.Recipe) ||
            !ProcessingRules.Host(_world, npc, host, job.Recipe) || _world.Has<SleepingComponent>(npc) ||
            _world.Has<CookingAttemptComponent>(npc) || _world.Has<FishingAttemptComponent>(npc)) return false;
        if (_world.TryGet<OperatingProcessComponent>(npc, out var operating)) return operating.HostId == host.Id;
        // A paused interval is never counted when work resumes.
        job.LastUpdatedAt = _clock.CurrentMinute;
        _travel.Stop(npc); _world.Add(npc, new OperatingProcessComponent(host.Id));
        return true;
    }

    /// <summary>Stops active operation while leaving the job and consumed inputs at their host.</summary>
    /// <param name="npc">The operator changing activity.</param>
    public void Pause(Entity npc) { _world.Remove<OperatingProcessComponent>(npc); }

    /// <summary>Collects a completed owner's outputs once and frees its machinery even when chance produced nothing.</summary>
    /// <param name="npc">The original owner receiving outputs.</param>
    /// <param name="host">The machinery or actor storing the completed job.</param>
    /// <returns>True when collection completes.</returns>
    public bool Collect(Entity npc, Entity host)
    {
        if (!_world.TryGet<ProcessingJobComponent>(host, out var job) || job is null || !job.Completed || job.OwnerId != npc.Id ||
            !ProcessingRules.Host(_world, npc, host, job.Recipe) || !_world.TryGet<HealthComponent>(npc, out var hp) || hp.Current <= 0 ||
            _world.Has<DeadComponent>(npc) || !_world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) return false;
        foreach (var item in job.Outputs) bag.Items.Add(item);
        _world.Remove<ProcessingJobComponent>(host);
        return true;
    }

    /// <summary>Updates indexed jobs in world order, rolling outputs once and applying configured raw-food effects to products.</summary>
    /// <param name="deltaTime">The simulation step; job timing uses the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Use a stable host list because completion creates output entities while jobs remain reserved until collection.
        foreach (var host in GetHosts())
        {
            if (!_world.TryGet<ProcessingJobComponent>(host, out var job) || job is null || job.Completed) continue;
            long elapsed = Math.Max(0, _clock.CurrentMinute.ElapsedSince(job.LastUpdatedAt).Minutes);
            job.LastUpdatedAt = _clock.CurrentMinute;
            bool active = !job.Recipe.RequiresOperator;
            if (job.Recipe.RequiresOperator && _world.TryGetEntity(job.OwnerId, out var npc) && npc is not null)
            {
                active = _world.TryGet<OperatingProcessComponent>(npc, out var operation) && operation.HostId == host.Id &&
                    ProcessingRules.Requirements(_world, npc, job.Recipe) && ProcessingRules.Host(_world, npc, host, job.Recipe) &&
                    !_world.Has<TravelComponent>(npc) && !_world.Has<SleepingComponent>(npc) && !_world.Has<CookingAttemptComponent>(npc) && !_world.Has<FishingAttemptComponent>(npc) &&
                    (!_world.TryGet<VelocityComponent>(npc, out var velocity) || (velocity.X == 0 && velocity.Y == 0));
                if (!active) Pause(npc);
            }
            if (!active) continue;
            job.ProgressMinutes += Math.Min(elapsed, job.Recipe.ProcessingMinutes - job.ProgressMinutes);
            if (job.ProgressMinutes < job.Recipe.ProcessingMinutes) continue;
            job.Completed = true;
            if (_world.TryGetEntity(job.OwnerId, out var owner) && owner is not null &&
                _world.TryGet<OperatingProcessComponent>(owner, out var operating) && operating.HostId == host.Id) Pause(owner);
            // One roll per group chooses at most one alternative. Missing probability means no item.
            foreach (var group in job.Recipe.Outputs)
            {
                int roll = _random.Next(100), cumulative = 0;
                foreach (var output in group.Options)
                {
                    cumulative += output.ChancePercent;
                    if (roll >= cumulative) continue;
                    int quantity = _random.Next(output.MinimumQuantity, output.MaximumQuantity + 1);
                    Entity item;
                    if (output.Kind == ProcessingItemKind.Seed)
                    {
                        var plant = GameSettings.Find(_world)!.Farming.Plants.First(p => p.Id == output.PlantId);
                        item = new PlantSeedBlueprint().Spawn(_world, plant, quantity);
                    }
                    else
                    {
                        item = _world.CreateEntity(); _world.Add(item, new ItemComponent(output.Name));
                        _world.Add(item, new PlantProduceComponent(output.ItemId, quantity));
                        RawFoodRules.Apply(_world, item);
                    }
                    job.Outputs.Add(item); break;
                }
            }
        }
    }

    /// <summary>Finds job hosts through the component index, rebuilding world order only when membership changes.</summary>
    /// <returns>A stable host list that preserves operator handling and random output roll order.</returns>
    private List<Entity> GetHosts()
    {
        List<Entity> current = _world.GetEntitiesWith<ProcessingJobComponent>().ToList();
        if (current.Count != _hostIds.Count || current.Any(host => !_hostIds.Contains(host.Id)))
        {
            _hostIds.Clear();
            foreach (Entity host in current) _hostIds.Add(host.Id);
            // Job attachment order must not change which host receives each random roll or checks its operator first.
            _hosts = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _hostIds.Contains(entity.Id)).ToList();
        }
        return _hosts;
    }

    /// <summary>Prevents starting another task while the actor is sleeping, fishing, cooking, or actively operating.</summary>
    /// <param name="npc">The actor to inspect.</param>
    /// <returns>True if an incompatible activity is active.</returns>
    private bool Busy(Entity npc) => _world.Has<OperatingProcessComponent>(npc) || _world.Has<SleepingComponent>(npc) ||
        _world.Has<CookingAttemptComponent>(npc) || _world.Has<FishingAttemptComponent>(npc);
}
