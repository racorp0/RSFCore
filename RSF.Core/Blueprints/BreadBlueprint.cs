using RSF.Core.Configuration;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Systems;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Assembles bread using RSF's existing consumable item effects.</summary>
public sealed class BreadBlueprint
{
    /// <summary>Creates one loaf with configured restoration and lifetime, preserving legacy manual worlds without settings.</summary>
    /// <param name="world">The world that owns the bread.</param>
    /// <returns>The newly created bread item.</returns>
    public Entity Spawn(World world)
    {
        Entity bread = world.CreateEntity();
        world.Add(bread, new ItemComponent("Bread"));
        world.Add(bread, new ConsumableItemComponent());
        world.Add(bread, new RestoreHungerEffectComponent(GameSettings.Find(world)?.Needs.BreadHunger ?? 30));

        FoodRules.Start(world, bread, GameSettings.Find(world)?.Food.BreadShelfLifeMinutes);
        return bread;
    }
}
