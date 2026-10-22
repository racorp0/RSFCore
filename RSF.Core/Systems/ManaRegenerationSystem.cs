using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Restores mana for configured entities using RSF's existing components and shared game clock.</summary>
public sealed class ManaRegenerationSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    /// <summary>Connects mana regeneration to the world and elapsed game minutes.</summary>
    /// <param name="world">The world containing mana and regeneration components.</param>
    /// <param name="clock">The shared simulation clock.</param>
    public ManaRegenerationSystem(
        World world,
        WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Checks only entities configured for mana regeneration while preserving existing tick calculations.</summary>
    /// <param name="deltaTime">The simulation step; tick timing uses the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Skip unrelated terrain through the existing regeneration index.
        // A tick replaces existing component values without adding or removing indexed entries.
        foreach (Entity entity in _world.GetEntitiesWith<ManaRegenerationComponent>())
        {
            if (!_world.TryGet<ManaComponent>(
                entity,
                out var mana))
            {
                continue;
            }

            if (!_world.TryGet<ManaRegenerationComponent>(
                entity,
                out var regeneration))
            {
                continue;
            }

            RegenerateMana(
                entity,
                mana,
                regeneration);
        }
    }

    private void RegenerateMana(
    Entity entity,
    ManaComponent mana,
    ManaRegenerationComponent regeneration)
    {
        GameDuration sinceLastTick =
            _clock.CurrentMinute.ElapsedSince(
                regeneration.LastTickAt);

        if (sinceLastTick < regeneration.TickInterval)
        {
            return;
        }

        int newMana =
            Math.Min(
                mana.Current + regeneration.ManaPerTick,
                mana.Max);

        if (newMana == mana.Current)
        {
            return;
        }

        _world.Add(
            entity,
            new ManaComponent(
                newMana,
                mana.Max));

        regeneration = regeneration with
        {
            LastTickAt = _clock.CurrentMinute
        };

        _world.Add(
            entity,
            regeneration);

        Console.WriteLine(
            $"Entity {entity.Id} regenerated " +
            $"{regeneration.ManaPerTick} mana.");

        Console.WriteLine(
            $"Mana: {newMana}/{mana.Max}");
    }
}
