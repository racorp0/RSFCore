using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Assembles a reusable watering tool for RSF's inventory.</summary>
public sealed class WateringCanBlueprint
{
    /// <summary>Creates a watering can that farming actions can recognize.</summary>
    /// <param name="world">The world that owns the tool.</param>
    /// <returns>The newly created watering can.</returns>
    public Entity Spawn(World world)
    {
        Entity can = world.CreateEntity();
        world.Add(can, new ItemComponent("Watering Can"));
        world.Add(can, new WateringCanComponent());

        return can;
    }
}
