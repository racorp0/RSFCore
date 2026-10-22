using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares JSON species-to-meat conversion between ingredient planning and NPC purchasing.</summary>
public static class FishMeat
{
    /// <summary>Finds the configured species metadata without inventing item identities for unknown fish.</summary>
    /// <param name="world">The inventory world.</param>
    /// <param name="species">The fish species name.</param>
    /// <returns>The species definition, or null when not configured.</returns>
    public static RiverFishDefinition? Definition(World world, string species) => GameSettings.Find(world)?.Fishing.FishTypes
        .FirstOrDefault(fish => string.Equals(fish.Species, species, StringComparison.OrdinalIgnoreCase));

    /// <summary>Counts prepared meat grams or the usable yield of an unchopped fish, rounding usable yield down to whole grams.</summary>
    /// <param name="world">The inventory world.</param>
    /// <param name="item">A whole fish or ordinary species-meat stack.</param>
    /// <returns>Usable meat grams, or zero for another item or invalid metadata.</returns>
    public static int Grams(World world, Entity item)
    {
        if (FoodRules.IsExpired(world, item)) return 0;
        if (world.TryGet<FishComponent>(item, out var fish))
        {
            var definition = Definition(world, fish.Species);
            if (definition is null || string.IsNullOrWhiteSpace(definition.MeatItemId) || definition.MeatYieldPercent <= 0 ||
                definition.MeatYieldPercent > 100 || fish.WeightGrams <= 0) return 0;
            // Long arithmetic avoids overflow. Integer division never creates a fraction of a gram.
            return (int)((long)fish.WeightGrams * definition.MeatYieldPercent / 100);
        }
        if (world.TryGet<PlantProduceComponent>(item, out var meat) &&
            GameSettings.Find(world)?.Fishing.FishTypes.Any(definition => definition.MeatItemId == meat.ItemId) == true)
            return Math.Max(0, meat.Quantity);
        return 0;
    }
}
