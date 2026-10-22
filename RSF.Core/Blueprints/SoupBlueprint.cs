using RSF.Core.Configuration;
using RSF.Core.Systems;

using System;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates soup consumables through existing blueprints with configured hunger effects.</summary>
public sealed class SoupBlueprint
{
    private readonly Random _random = new();

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
        Entity soup = world.CreateEntity();

        var settings = GameSettings.Find(world)?.Needs ?? new SurvivalSettings();
        int hungerRestored = _random.Next(settings.MinimumSoupHunger, settings.MaximumSoupHunger + 1);

        world.Add(soup, new ItemComponent("Soup"));
        world.Add(soup, new PositionComponent(x, y));
        world.Add(soup, new ConsumableItemComponent());
        world.Add(
            soup,
            new RestoreHungerEffectComponent(hungerRestored));

        FoodRules.Start(world, soup, GameSettings.Find(world)?.Food.OrdinaryMealShelfLifeMinutes);
        return soup;
    }
}
