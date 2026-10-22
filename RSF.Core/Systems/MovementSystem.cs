using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Applies existing velocity components to positions without visiting stationary terrain entities.</summary>
public sealed class MovementSystem : ISystem
{
    private readonly World _world;

    /// <summary>Connects movement to the existing world and component storage.</summary>
    /// <param name="world">The world containing movable entities.</param>
    public MovementSystem(World world)
    {
        _world = world;
    }

    /// <summary>Applies velocities only to matching entities, preserving the existing death, sleep, cooking, and position checks.</summary>
    /// <param name="deltaTime">The elapsed simulation step used for position integration.</param>
    public void Update(float deltaTime)
    {
        // Position updates do not change the velocity index, so this live enumeration is safe here.
        foreach (var entity in _world.GetEntitiesWith<VelocityComponent>())
        {
            if (_world.TryGet<DeadComponent>(
                entity,
                out _))
            {
                continue;
            }

            // Sleep and cooking are stationary activities even if an old move request remains.
            if (_world.Has<SleepingComponent>(entity) || _world.Has<CookingAttemptComponent>(entity)) continue;

            if (!_world.TryGet<PositionComponent>(
                entity,
                out var position))
            {
                continue;
            }

            if (!_world.TryGet<VelocityComponent>(
                entity,
                out var velocity))
            {
                continue;
            }

            PositionComponent newPosition =
                new PositionComponent(
                    position.X + velocity.X * deltaTime,
                    position.Y + velocity.Y * deltaTime);

            _world.Set(
                entity,
                newPosition);
        }
    }

    /// <summary>Stores a velocity through the normal component path; later queries see it automatically.</summary>
    /// <param name="entity">The entity receiving the velocity.</param>
    /// <param name="x">The horizontal speed.</param>
    /// <param name="y">The vertical speed.</param>
    public void SetVelocity(
    Entity entity,
    int x,
    int y)
    {
        VelocityComponent velocity =
            new(x, y);

        _world.Add(
            entity,
            velocity);
    }
}