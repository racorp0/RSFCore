using RSF.Core.Components;
using RSF.Core.Systems;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates bait supplies using the existing inventory item quantity convention.</summary>
public sealed class BaitBlueprint
{
    /// <summary>Creates named bait from JSON; omitted identity preserves existing worm rewards and starting supplies.</summary>
    /// <param name="world">The world that owns the bait.</param>
    /// <param name="quantity">The number of bait pieces.</param>
    /// <param name="baitId">The configured bait identity, defaulting to worms.</param>
    /// <returns>The new bait inventory item.</returns>
    public Entity Spawn(World world, int quantity, string baitId = "worm")
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var definition = BaitRules.Find(world, baitId) ?? throw new ArgumentException("Unknown bait identity.", nameof(baitId));
        Entity bait = world.CreateEntity();
        world.Add(bait, new ItemComponent(definition.Name));
        world.Add(bait, new BaitComponent(quantity, baitId));
        return bait;
    }
}
