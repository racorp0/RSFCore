using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates a reusable fishing rod for the existing inventory system.</summary>
public sealed class FishingRodBlueprint
{
    /// <summary>Creates a rod item recognized by FishingSystem.</summary>
    /// <param name="world">The world that owns the rod.</param>
    /// <returns>The new fishing rod.</returns>
    public Entity Spawn(World world)
    {
        Entity rod = world.CreateEntity();
        world.Add(rod, new ItemComponent("Fishing Rod"));
        world.Add(rod, new FishingRodComponent());
        return rod;
    }
}
