using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Tracks command cooldown readiness and reports cooldown status through RSF's shared game clock.</summary>
public sealed class CooldownSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    /// <summary>Connects cooldown checks to existing world components and simulation time.</summary>
    /// <param name="world">The world containing entities with cooldowns.</param>
    /// <param name="clock">The shared game-minute clock.</param>
    public CooldownSystem(
        World world,
        WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Reports current status only for entities with cooldown components, without changing readiness.</summary>
    /// <param name="deltaTime">The simulation step; elapsed time comes from the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Skip unrelated terrain through the existing cooldown index. Reporting does not change this collection.
        foreach (Entity entity in _world.GetEntitiesWith<CooldownComponent>())
        {

            if (!_world.TryGet<CooldownComponent>(
                entity,
                out var cooldown))
            {
                continue;
            }

            GameDuration elapsed =
                _clock.CurrentMinute.ElapsedSince(
                    cooldown.StartedAt);

            if (elapsed >= cooldown.Duration)
            {
                Console.WriteLine(
                    $"Entity {entity} cooldown READY");
            }
            else
            {
                Console.WriteLine(
                    $"Entity {entity} cooldown: " +
                    $"{elapsed.Minutes}/{cooldown.Duration.Minutes} minutes");
            }
        }
    }

    public bool IsReady(Entity entity)
    {
        if (!_world.TryGet<CooldownComponent>(
            entity,
            out var cooldown))
        {
            // Kalau tidak punya cooldown,
            // berarti tidak sedang cooldown.
            return true;
        }

        GameDuration elapsed =
            _clock.CurrentMinute.ElapsedSince(
                cooldown.StartedAt);

        return elapsed >= cooldown.Duration;
    }

    public void StartCooldown(
    Entity entity,
    GameDuration duration)
    {
        CooldownComponent cooldown =
            new(
                _clock.CurrentMinute,
                duration);

        _world.Add(
            entity,
            cooldown);
    }

    
}
