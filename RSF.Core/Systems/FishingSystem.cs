using RSF.Core.Blueprints;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Starts and resolves timed fishing attempts using RSF's shared world and clock.</summary>
public sealed class FishingSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly InventorySystem _inventorySystem;
    private readonly Random _random;
    // Preserve world order so each fisher receives the same rolls from the shared random generator.
    private List<Entity> _fishers = new();
    private readonly HashSet<EntityId> _fisherIds = new();

    /// <summary>Connects fishing to existing simulation services.</summary>
    /// <param name="world">The world containing fishers and rivers.</param>
    /// <param name="clock">The simulation's game-minute clock.</param>
    /// <param name="inventorySystem">Existing item storage operations.</param>
    /// <param name="random">Optional random generator for repeatable demonstrations or checks.</param>
    public FishingSystem(World world, WorldClock clock, InventorySystem inventorySystem, Random? random = null)
    {
        _world = world;
        _clock = clock;
        _inventorySystem = inventorySystem;
        _random = random ?? new Random();
    }

    /// <summary>Validates the selected bait table, pauses manual processing, and remembers the spent bait type for the timed attempt.</summary>
    /// <param name="fisher">A living NPC with fishing skill of at least level one.</param>
    /// <param name="river">A valid water entity containing a catch table.</param>
    /// <param name="rod">The fishing rod in the fisher's inventory.</param>
    /// <param name="bait">The bait inventory item.</param>
    /// <returns>True if fishing started; false leaves bait and the existing attempt untouched.</returns>
    public bool StartFishing(Entity fisher, Entity river, Entity rod, Entity bait)
    {
        if (!CanFish(fisher, river, rod) ||
            _world.Has<FishingAttemptComponent>(fisher) ||
            !IsRegistered(bait) ||
            !_inventorySystem.HasItem(fisher, bait) ||
            !_world.TryGet<BaitComponent>(bait, out var supply) ||
            supply.Quantity <= 0 || _world.Has<SaleLocationComponent>(bait))
        {
            return false;
        }

        if (!_world.TryGet<RiverComponent>(river, out var water) || water is null) return false;
        if (water.MinimumCatchMinutes <= 0 || water.MaximumCatchMinutes < water.MinimumCatchMinutes || water.MaximumCatchMinutes == int.MaxValue) return false;
        if (!BaitRules.CanUse(_world, water, supply.BaitId)) return false;
        // Random.Next excludes its upper bound; adding one includes the configured maximum.
        GameDuration duration = GameDuration.FromMinutes(_random.Next(water.MinimumCatchMinutes, water.MaximumCatchMinutes + 1));
        _world.Add(bait, supply with { Quantity = supply.Quantity - 1 });
        if (supply.Quantity == 1)
        {
            _inventorySystem.RemoveItem(fisher, bait);
        }

        _world.Remove<OperatingProcessComponent>(fisher);
        _world.Add(fisher, new FishingAttemptComponent(river.Id, rod.Id, _clock.CurrentMinute, duration, supply.BaitId));
        Console.WriteLine($"Fisher {fisher.Id} will finish fishing in {duration.Minutes} minutes.");
        return true;
    }

    /// <summary>Ends an active attempt without returning the bait consumed when it started.</summary>
    /// <param name="fisher">The registered NPC to stop fishing.</param>
    /// <returns>True if an attempt was cancelled.</returns>
    public bool Cancel(Entity fisher)
    {
        if (!IsRegistered(fisher)) return false;
        bool cancelled = _world.Remove<FishingAttemptComponent>(fisher);
        if (cancelled) Console.WriteLine($"Fisher {fisher.Id} cancelled fishing; bait remains spent.");
        return cancelled;
    }

    /// <summary>Resolves indexed attempts in world order using their consumed bait identity and cancels invalid attempts.</summary>
    /// <param name="deltaTime">The simulation step; waiting is measured with the shared clock.</param>
    public void Update(float deltaTime)
    {
        // A stable fisher list allows attempts to end and catches to create entities without changing this enumeration.
        foreach (Entity fisher in GetFishers())
        {
            if (!_world.TryGet<FishingAttemptComponent>(fisher, out var attempt))
            {
                continue;
            }

            if (!_world.TryGetEntity(attempt.RiverId, out var river) || river is null ||
                !_world.TryGetEntity(attempt.RodId, out var rod) || rod is null ||
                !CanFish(fisher, river, rod))
            {
                _world.Remove<FishingAttemptComponent>(fisher);
                Console.WriteLine($"Fisher {fisher.Id} stopped fishing; requirements are no longer met.");
                continue;
            }

            if (_clock.CurrentMinute.ElapsedSince(attempt.StartedAt) < attempt.Duration)
            {
                continue;
            }

            // Remove the attempt before creating its result, preventing duplicate catches on later updates.
            _world.Remove<FishingAttemptComponent>(fisher);
            ResolveCatch(fisher, river, attempt.BaitId);
        }
    }

    /// <summary>Finds active fishers through the component index, rebuilding world order only when membership changes.</summary>
    /// <returns>A stable fisher list that preserves the existing order of random catch rolls.</returns>
    private List<Entity> GetFishers()
    {
        List<Entity> current = _world.GetEntitiesWith<FishingAttemptComponent>().ToList();
        if (current.Count != _fisherIds.Count || current.Any(fisher => !_fisherIds.Contains(fisher.Id)))
        {
            _fisherIds.Clear();
            foreach (Entity fisher in current) _fisherIds.Add(fisher.Id);
            // Attempt attachment order must not change which fisher receives each catch and weight roll.
            _fishers = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _fisherIds.Contains(entity.Id)).ToList();
        }
        return _fishers;
    }

    /// <summary>Uses the consumed bait catch chances; locked species and unused probability remain no-catch outcomes.</summary>
    /// <param name="fisher">The NPC receiving any caught fish.</param>
    /// <param name="river">The validated river containing the catch table.</param>
    /// <param name="baitId">The bait spent at the start, independent of any remaining inventory.</param>
    private void ResolveCatch(Entity fisher, Entity river, string baitId)
    {
        if (!_world.TryGet<RiverComponent>(river, out var water) || water is null)
        {
            return;
        }

        if (!BaitRules.CanUse(_world, water, baitId)) return;
        _world.TryGet<FishingSkillComponent>(fisher, out var skill);
        int roll = _random.Next(100);
        int cumulativeChance = 0;

        foreach (RiverFishDefinition fishType in water.FishTypes)
        {
            cumulativeChance += BaitRules.Chance(water, baitId, fishType);
            if (roll >= cumulativeChance)
            {
                continue;
            }

            if (skill.Level < fishType.MinimumSkillLevel) break;

            int weight = _random.Next(fishType.MinWeightGrams, fishType.MaxWeightGrams + 1);
            Entity fish = new FishBlueprint().Spawn(_world, fishType.Species, weight);
            if (!_inventorySystem.AddItem(fisher, fish))
            {
                _world.DestroyEntity(fish.Id);
                return;
            }

            Console.WriteLine($"Fisher {fisher.Id} caught {fishType.Species}, {weight} g.");
            return;
        }

        // Any probability left after the table is an unsuccessful attempt; the bait stays spent.
        Console.WriteLine($"Fisher {fisher.Id} caught no fish this time.");
    }

    /// <summary>Validates the NPC, its rod, and a nearby river with a usable probability table.</summary>
    /// <param name="fisher">The entity attempting to fish.</param>
    /// <param name="river">The target water entity.</param>
    /// <param name="rod">The tool that must remain in inventory.</param>
    /// <returns>True if all fishing requirements are met.</returns>
    private bool CanFish(Entity fisher, Entity river, Entity rod)
    {
        if (!VillageRules.Near(_world, fisher, river.Id) || !IsRegistered(fisher) || !IsRegistered(river) || !IsRegistered(rod) ||
            _world.Has<DeadComponent>(fisher) ||
            !_world.TryGet<HealthComponent>(fisher, out var health) || health.Current <= 0 ||
            !_world.TryGet<FishingSkillComponent>(fisher, out var skill) || skill.Level < 1 ||
            !_world.TryGet<InventoryComponent>(fisher, out var inventory) || inventory is null ||
            !_world.Has<FishingRodComponent>(rod) || !_inventorySystem.HasItem(fisher, rod) ||
            !_world.TryGet<MapTileComponent>(river, out var tile) || tile is null || tile.Zone != MapZone.Water ||
            !_world.TryGet<RiverComponent>(river, out var water) || water is null || water.FishTypes is null ||
            water.FishTypes.Count == 0)
        {
            return false;
        }

        int totalChance = 0;
        foreach (RiverFishDefinition fishType in water.FishTypes)
        {
            // Keep malformed tables from spending bait or causing invalid random ranges.
            if (fishType is null || string.IsNullOrWhiteSpace(fishType.Species) ||
                fishType.MinimumSkillLevel < 1 || fishType.CatchChancePercent < 0 || fishType.CatchChancePercent > 100 ||
                fishType.MinWeightGrams <= 0 || fishType.MaxWeightGrams < fishType.MinWeightGrams ||
                fishType.MaxWeightGrams == int.MaxValue)
            {
                return false;
            }

            totalChance += fishType.CatchChancePercent;
            if (totalChance > 100)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Rejects destroyed entities and entities from another world.</summary>
    /// <param name="entity">The entity to check.</param>
    /// <returns>True if this exact entity belongs to the fishing world.</returns>
    private bool IsRegistered(Entity entity)
    {
        return _world.TryGetEntity(entity.Id, out var registered) && registered == entity;
    }
}

