using RSF.Core.Configuration;
using RSF.Core.Systems;

using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates apple consumables through existing blueprints with configured hunger effects.</summary>
public sealed class AppleBlueprint
{
    /// <summary>Creates one item with loaded food effects and expiration, preserving legacy worlds without settings.</summary>
    /// <param name="world">The world owning the item.</param>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <returns>The new consumable item.</returns>
    public Entity Spawn(
        World world,
        float x,
        float y)
    {
        Entity apple = world.CreateEntity();

        world.Add(apple, new ItemComponent("Apple"));
        world.Add(apple, new PositionComponent(x, y));
        world.Add(apple, new ConsumableItemComponent());
        world.Add(apple, new RestoreHungerEffectComponent(GameSettings.Find(world)?.Needs.AppleHunger ?? 5));

        FoodRules.Start(world, apple, GameSettings.Find(world)?.Food.RawProduceShelfLifeMinutes);
        return apple;
    }
}
