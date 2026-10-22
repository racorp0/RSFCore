using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Blueprints;

/// <summary>Creates seeds for any loaded plant using existing entity and inventory components.</summary>
public sealed class PlantSeedBlueprint
{
    /// <summary>Creates a named seed stack without special cases for individual species.</summary>
    /// <param name="world">The world owning the item.</param>
    /// <param name="plant">The plant definition loaded from JSON.</param>
    /// <param name="quantity">The positive number of seeds.</param>
    /// <returns>The new inventory item.</returns>
    public Entity Spawn(World world, PlantDefinition plant, int quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Entity item = world.CreateEntity();
        world.Add(item, new ItemComponent(plant.Name + " Seeds"));
        world.Add(item, new PlantSeedComponent(plant.Id, quantity));
        return item;
    }
}
