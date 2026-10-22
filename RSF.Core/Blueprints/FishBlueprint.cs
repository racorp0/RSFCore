using RSF.Core.Configuration;
using RSF.Core.Systems;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates an individual caught fish with a species name and weight.</summary>
public sealed class FishBlueprint
{
    /// <summary>Creates a fresh fish item with its configured absolute expiration deadline.</summary>
    /// <param name="world">The world containing the catch.</param>
    /// <param name="species">The fish species name.</param>
    /// <param name="weightGrams">The positive weight in whole grams.</param>
    /// <returns>The new fish item.</returns>
    public Entity Spawn(World world, string species, int weightGrams)
    {
        if (string.IsNullOrWhiteSpace(species))
        {
            throw new ArgumentException("A fish needs a species name.", nameof(species));
        }
        if (weightGrams <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weightGrams));
        }

        Entity fish = world.CreateEntity();
        world.Add(fish, new ItemComponent(species));
        world.Add(fish, new FishComponent(species, weightGrams));
        FoodRules.Start(world, fish, GameSettings.Find(world)?.Food.FreshFishShelfLifeMinutes);
        return fish;
    }
}
