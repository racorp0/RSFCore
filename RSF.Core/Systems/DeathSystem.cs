using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
using RSF.Core.Systems;

/// <summary>Marks entities with depleted health as dead so existing RSF systems can stop their actions.</summary>
public sealed class DeathSystem : ISystem
{
    private readonly World _world;

    /// <summary>Connects death detection to the world's existing health and death components.</summary>
    /// <param name="world">The simulation world containing entities that can die.</param>
    public DeathSystem(World world)
    {
        _world = world;
    }

    /// <summary>Checks only entities with health and reports each newly detected death once.</summary>
    /// <param name="deltaTime">The simulation step; death detection reads current health directly.</param>
    public void Update(float deltaTime)
    {
        // Terrain without health cannot die, so use the existing health index to skip it.
        // Adding DeadComponent leaves the health collection unchanged and safe to enumerate.
        foreach (Entity entity in _world.GetEntitiesWith<HealthComponent>())
        {
            if (!_world.TryGet<HealthComponent>(
                entity,
                out var health))
            {
                continue;
            }

            if (health.Current > 0)
            {
                continue;
            }

            if (_world.TryGet<DeadComponent>(
                entity,
                out _))
            {
                continue;
            }

            _world.Add(
                entity,
                new DeadComponent());

            Console.WriteLine(
                $"Entity {entity.Id} died.");
        }
    }
}
