using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Updates timed poison and regeneration effects using RSF's existing components and game clock.</summary>
public sealed class StatusEffectSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    /// <summary>Connects status processing to the simulation world and shared clock.</summary>
    /// <param name="world">The world containing entities with timed effects.</param>
    /// <param name="clock">The game clock used for effect ticks and expiry.</param>
    public StatusEffectSystem(
        World world,
        WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Updates only affected entities, applying poison before regeneration on each entity.</summary>
    /// <param name="deltaTime">The simulation step; effect timing uses the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Skip terrain by combining the existing status indexes. Distinct prevents double ticks when both effects exist.
        // Copy first because processing can remove expired effects from the indexed collections.
        List<Entity> affected = _world.GetEntitiesWith<PoisonStatusComponent>()
            .Concat(_world.GetEntitiesWith<RegenerationStatusComponent>())
            .Distinct()
            .ToList();
        foreach (Entity entity in affected)
        {
            if (_world.TryGet<PoisonStatusComponent>(
                entity,
                out var poison))
            {
                ProcessPoison(
                    entity,
                    poison);
            }

            if (_world.TryGet<RegenerationStatusComponent>(
                entity,
                out var regeneration))
            {
                ProcessRegeneration(
                    entity,
                    regeneration);
            }
        }
    }

    private void ProcessPoison(
    Entity entity,
    PoisonStatusComponent poison)
    {
        GameDuration sinceLastTick =
            _clock.CurrentMinute.ElapsedSince(
                poison.LastTickAt);

        if (sinceLastTick >= poison.TickInterval)
        {
            ApplyPoisonDamage(
                entity,
                poison);

            poison = poison with
            {
                LastTickAt = _clock.CurrentMinute
            };

            _world.Add(
                entity,
                poison);
        }

        GameDuration elapsed =
            _clock.CurrentMinute.ElapsedSince(
                poison.StartedAt);

        if (elapsed >= poison.Duration)
        {
            RemovePoison(entity);
        }
    }

    private void RemovePoison(Entity entity)
    {
        _world.Remove<PoisonStatusComponent>(
            entity);
    }

    private void ApplyPoisonDamage(
    Entity entity,
    PoisonStatusComponent poison)
    {
        if (!_world.TryGet<HealthComponent>(
            entity,
            out var health))
        {
            return;
        }

        int newHealth =
            Math.Max(
                health.Current - poison.DamagePerTick,
                0);

        _world.Add(
            entity,
            new HealthComponent(
                newHealth,
                health.Max));

        Console.WriteLine(
            $"Entity {entity.Id} took " +
            $"{poison.DamagePerTick} poison damage.");
    }

    private void ProcessRegeneration(
    Entity entity,
    RegenerationStatusComponent regeneration)
    {
        GameDuration sinceLastTick =
            _clock.CurrentMinute.ElapsedSince(
                regeneration.LastTickAt);

        if (sinceLastTick >= regeneration.TickInterval)
        {
            ApplyRegeneration(
                entity,
                regeneration);

            regeneration = regeneration with
            {
                LastTickAt = _clock.CurrentMinute
            };

            _world.Add(
                entity,
                regeneration);
        }

        GameDuration elapsed =
            _clock.CurrentMinute.ElapsedSince(
                regeneration.StartedAt);

        if (elapsed >= regeneration.Duration)
        {
            RemoveRegeneration(entity);
        }
    }

    private void ApplyRegeneration(
    Entity entity,
    RegenerationStatusComponent regeneration)
    {
        if (!_world.TryGet<HealthComponent>(
            entity,
            out var health))
        {
            return;
        }

        int newHealth =
            Math.Min(
                health.Current +
                regeneration.HealPerTick,
                health.Max);

        _world.Add(
            entity,
            new HealthComponent(
                newHealth,
                health.Max));

        Console.WriteLine(
            $"Entity {entity.Id} regenerated " +
            $"{regeneration.HealPerTick} health.");
    }

    private void RemoveRegeneration(
    Entity entity)
    {
        _world.Remove<RegenerationStatusComponent>(
            entity);
    }
}
