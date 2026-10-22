using RSF.Core.Configuration;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class HungerSystem : ISystem
{
    private readonly World _world;

    private float _elapsedMinutes;
    private readonly SurvivalSettings _settings;

    /// <summary>Connects hunger updates to loaded rates, retaining legacy defaults for manual worlds.</summary>
    /// <param name="world">The world containing survival components and optional settings.</param>
    public HungerSystem(World world)
    {
        _world = world;
        _settings = GameSettings.Find(world)?.Needs ?? new SurvivalSettings();
    }

    /// <summary>Applies hunger loss when the configured game-minute interval elapses.</summary>
    /// <param name="deltaTime">Elapsed game minutes supplied by Simulation.</param>
    public void Update(float deltaTime)
    {
        _elapsedMinutes += deltaTime;

        if (_elapsedMinutes < _settings.HungerTickMinutes)
        {
            return;
        }

        _elapsedMinutes -= _settings.HungerTickMinutes;

        foreach (Entity entity in _world.GetEntities())
        {
            if (_world.TryGet<DeadComponent>(
                entity,
                out _))
            {
                continue;
            }

            if (!_world.TryGet<HungerComponent>(
                entity,
                out var hunger))
            {
                continue;
            }

            ProcessHunger(
                entity,
                hunger);
        }
    }

    /// <summary>Applies configured hunger loss quietly and reports configured starvation damage when food runs out.</summary>
    /// <param name="entity">The living entity whose hunger is being processed.</param>
    /// <param name="hunger">The entity's current hunger values.</param>
    private void ProcessHunger(
        Entity entity,
        HungerComponent hunger)
    {
        // Hunger decreases over time
        int newHunger =
            Math.Max(
                hunger.Current - _settings.HungerLossPerTick,
                0);

        if (newHunger != hunger.Current)
        {
            _world.Add(
                entity,
                new HungerComponent(
                    newHunger,
                    hunger.Max));

            hunger = new HungerComponent(
                newHunger,
                hunger.Max);

        }

        // Starvation
        if (hunger.Current > 0)
        {
            return;
        }

        if (!_world.TryGet<HealthComponent>(
            entity,
            out var health))
        {
            return;
        }

        int starvationDamage = _settings.StarvationDamage;

        int newHealth =
            Math.Max(
                health.Current - starvationDamage,
                0);

        _world.Add(
            entity,
            new HealthComponent(
                newHealth,
                health.Max));

        Console.WriteLine(
            $"Entity {entity.Id} is starving " +
            $"and took {starvationDamage} damage.");

        Console.WriteLine(
            $"HP: {newHealth}/{health.Max}");
    }
}
