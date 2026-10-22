using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Finds short grid routes once per destination and supplies movement velocities over game time.</summary>
public sealed class TravelSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly MovementSystem _movement;
    /// <summary>Reuses the existing movement system rather than adding another position integrator.</summary>
    /// <param name="world">The village world.</param>
    /// <param name="clock">The shared game clock.</param>
    /// <param name="movement">The existing velocity-based movement system.</param>
    public TravelSystem(World world, WorldClock clock, MovementSystem movement)
    { _world = world; _clock = clock; _movement = movement; }

    /// <summary>Creates a four-direction route and pauses manual processing before the NPC leaves to travel.</summary>
    /// <param name="npc">The living NPC to move.</param>
    /// <param name="destination">A registered place, river, plot, or building.</param>
    /// <returns>True if already near or a route exists; false does not teleport the NPC.</returns>
    public bool Start(Entity npc, Entity destination)
    {
        if (!_world.TryGetEntity(npc.Id, out var registered) || registered != npc ||
            !_world.TryGetEntity(destination.Id, out var target) || target != destination ||
            !_world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null ||
            !_world.TryGet<HealthComponent>(npc, out var hp) || hp.Current <= 0 || _world.Has<DeadComponent>(npc) ||
            _world.Has<SleepingComponent>(npc) || _world.Has<CookingAttemptComponent>(npc) || _world.Has<FishingAttemptComponent>(npc) ||
            !_world.TryGet<PositionComponent>(npc, out var position) || !_world.TryGet<PositionComponent>(destination, out var goal)) return false;
        _world.Remove<OperatingProcessComponent>(npc);
        if (VillageRules.Near(_world, npc, destination.Id)) { Stop(npc); return true; }
        if (_world.TryGet<TravelComponent>(npc, out var existing) && existing is not null && existing.DestinationId == destination.Id) return true;
        var start = ((int)position.X, (int)position.Y);
        Queue<(int X, int Y)> queue = new();
        Dictionary<(int X, int Y), (int X, int Y)> previous = new();
        queue.Enqueue(start); previous[start] = start;
        (int X, int Y)? end = null;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (Math.Abs(cell.X - goal.X) + Math.Abs(cell.Y - goal.Y) <= 1) { end = cell; break; }
            foreach (var step in new[] { (cell.X + 1, cell.Y), (cell.X - 1, cell.Y), (cell.X, cell.Y + 1), (cell.X, cell.Y - 1) })
            {
                if (previous.ContainsKey(step) || !resident.Village.Cells.TryGetValue(step, out var zone) ||
                    zone == MapZone.Blocked || zone == MapZone.Water) continue;
                previous[step] = cell; queue.Enqueue(step);
            }
        }
        if (end is null) { Stop(npc); return false; }
        List<(int X, int Y)> path = new();
        var current = end.Value;
        while (current != start) { path.Add(current); current = previous[current]; }
        path.Reverse();
        TravelComponent travel = new() { DestinationId = destination.Id, NextStepAt = _clock.CurrentMinute };
        foreach (var step in path) travel.Steps.Enqueue(step);
        _world.Add(npc, travel);
        return true;
    }

    /// <summary>Stops both the cached route and its movement velocity.</summary>
    /// <param name="npc">The NPC to stop.</param>
    public void Stop(Entity npc) { _world.Remove<TravelComponent>(npc); _movement.SetVelocity(npc, 0, 0); }

    /// <summary>Updates only travelers, moving one route tile at the configured interval and stopping on arrival or invalidation.</summary>
    /// <param name="deltaTime">The simulation step used by MovementSystem later in the same update.</param>
    public void Update(float deltaTime)
    {
        // Skip stationary terrain through the existing travel index. Copy first because Stop removes TravelComponent.
        foreach (Entity npc in _world.GetEntitiesWith<TravelComponent>().ToList())
        {
            if (!_world.TryGet<TravelComponent>(npc, out var travel) || travel is null) continue;
            if (!_world.TryGet<VillageResidentComponent>(npc, out var resident) || resident is null ||
                !_world.TryGetEntity(travel.DestinationId, out var destination) || destination is null ||
                !_world.TryGet<HealthComponent>(npc, out var hp) || hp.Current <= 0 ||
                _world.Has<SleepingComponent>(npc) || _world.Has<DeadComponent>(npc) || VillageRules.Near(_world, npc, destination.Id))
            { Stop(npc); continue; }
            if (!_world.TryGet<PositionComponent>(npc, out var position)) { Stop(npc); continue; }
            while (travel.Steps.Count > 0 && travel.Steps.Peek() == ((int)position.X, (int)position.Y)) travel.Steps.Dequeue();
            if (travel.Steps.Count == 0) { Stop(npc); continue; }
            var next = travel.Steps.Peek();
            if (!resident.Village.Cells.TryGetValue(next, out var zone) || zone == MapZone.Blocked || zone == MapZone.Water)
            { Stop(npc); continue; }
            if (_clock.CurrentMinute.Value < travel.NextStepAt.Value) { _movement.SetVelocity(npc, 0, 0); continue; }
            _movement.SetVelocity(npc, Math.Sign(next.X - position.X), Math.Sign(next.Y - position.Y));
            travel.NextStepAt = _clock.CurrentMinute.Add(resident.Village.MinutesPerTravelTile);
        }
    }
}
