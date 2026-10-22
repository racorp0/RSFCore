using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Marks expired food through the fresh-food component index, leaving spoiled items visible in inventories.</summary>
public sealed class FoodExpirationSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    /// <summary>Connects food helpers to the existing simulation clock through ordinary component storage.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="clock">The existing shared game clock.</param>
    public FoodExpirationSystem(World world, WorldClock clock)
    {
        _world = world; _clock = clock;
        var existing = world.GetEntitiesWith<FoodClockComponent>().ToList();
        if (existing.Any(entity => !world.TryGet<FoodClockComponent>(entity, out var time) || time is null || !ReferenceEquals(time.Clock, clock)))
            throw new InvalidOperationException("Food expiration must use this world's existing simulation clock.");
        if (existing.Count == 0) world.Add(world.CreateEntity(), new FoodClockComponent(clock));
    }
    /// <summary>Replaces reached fresh deadlines with spoilage markers before gameplay consumes or trades food.</summary>
    /// <param name="deltaTime">The simulation step; absolute deadlines use the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Only reached items are copied before removing components. Spoiled items leave this index permanently.
        var expired = _world.GetEntitiesWith<FoodExpirationComponent>().Where(item =>
            _world.TryGet<FoodExpirationComponent>(item, out var expiration) && _clock.CurrentMinute.Value >= expiration.ExpiresAt.Value).ToList();
        foreach (Entity item in expired)
        {
            _world.TryGet<FoodExpirationComponent>(item, out var expiration);
            FoodRules.Spoil(_world, item, expiration.ExpiresAt);
        }
    }
}
