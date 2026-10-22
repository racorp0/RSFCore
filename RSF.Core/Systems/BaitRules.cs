using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares JSON bait lookup and catch-table validation across fishing, inventory creation, and village AI.</summary>
public static class BaitRules
{
    /// <summary>Finds configured bait metadata; manual worlds retain the original worm supply.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="id">The exact bait identity.</param>
    /// <returns>The bait definition, or null when unknown.</returns>
    public static BaitDefinition? Find(World world, string id) => GameSettings.Find(world) is GameSettings settings
        ? settings.Fishing.Baits.FirstOrDefault(bait => bait.Id == id)
        : id == "worm" ? new BaitDefinition { Id = "worm", Name = "Worm Bait" } : null;

    /// <summary>Reads a bait's unit sale price, retaining the village price in manual worlds.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="id">The bait identity.</param>
    /// <param name="fallback">The legacy manual-world unit price.</param>
    /// <returns>A positive unit price, or zero for an unknown bait.</returns>
    public static int Price(World world, string id, int fallback) => GameSettings.Find(world) is null
        ? id == "worm" ? fallback : 0 : Find(world, id)?.Price ?? 0;

    /// <summary>Reads an absolute species chance, preserving the old table for manual worm fishing.</summary>
    /// <param name="water">The water's existing catch data.</param>
    /// <param name="baitId">The consumed bait identity.</param>
    /// <param name="fish">The species being rolled.</param>
    /// <returns>The percentage chance, with missing species treated as zero.</returns>
    public static int Chance(RiverComponent water, string baitId, RiverFishDefinition fish)
    {
        if (water.Baits.Count == 0) return baitId == "worm" ? fish.CatchChancePercent : 0;
        var bait = water.Baits.FirstOrDefault(definition => definition.Id == baitId);
        return bait is not null && bait.CatchChances.TryGetValue(fish.Species, out int chance) ? chance : 0;
    }

    /// <summary>Rejects unknown bait, unavailable location bait types, or malformed tables before any bait is spent.</summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="water">The targeted water's catch data.</param>
    /// <param name="baitId">The selected bait identity.</param>
    /// <returns>True for a usable table whose absolute chances total at most 100.</returns>
    public static bool CanUse(World world, RiverComponent water, string baitId)
    {
        if (Find(world, baitId) is null) return false;
        if (water.Baits.Count == 0) return string.IsNullOrEmpty(water.LocationId) && GameSettings.Find(world) is null && baitId == "worm";
        var bait = water.Baits.FirstOrDefault(definition => definition.Id == baitId);
        if (bait is null || bait.CatchChances is null || bait.CatchChances.Count == 0) return false;
        long total = 0;
        foreach (var entry in bait.CatchChances)
        {
            if (entry.Value < 0 || entry.Value > 100 || !water.FishTypes.Any(fish => fish.Species == entry.Key)) return false;
            total += entry.Value;
        }
        // Remaining probability means no catch; skill locks never redistribute these chances.
        return total <= 100;
    }
}
