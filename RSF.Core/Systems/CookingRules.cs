using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Shares private and public station access, optional requirements, and input planning between cooking AI and commands.</summary>
public static class CookingRules
{
    /// <summary>Checks all recipe requirements and usable species-meat grams without consuming ingredients or chopping fish.</summary>
    /// <param name="world">The cooking world.</param>
    /// <param name="cook">The recipe owner.</param>
    /// <param name="recipe">The JSON cooking recipe.</param>
    /// <param name="requireNear">Whether to require station distance now or allow AI to plan travel.</param>
    /// <param name="station">Optional selected station; omitted resolves a compatible station while preserving owned-inn lookup.</param>
    /// <returns>True if this recipe can be started after any required travel.</returns>
    public static bool CanStart(World world, Entity cook, Recipe recipe, bool requireNear = true, Entity? station = null)
    {
        return world.TryGet<CookingSettingsComponent>(cook, out var settings) && settings is not null &&
            !world.Has<CookingAttemptComponent>(cook) && !world.Has<SleepingComponent>(cook) && !world.Has<FishingAttemptComponent>(cook) &&
            KitchenAccess(world, cook, recipe, requireNear, station) && ProcessingRules.Requirements(world, cook, recipe) &&
            ProcessingRules.Plan(world, cook, recipe, out _);
    }

    /// <summary>Checks station access, preserving private inn kitchens while allowing public recipe stations.</summary>
    /// <param name="world">The cooking world.</param>
    /// <param name="cook">The actor preparing food.</param>
    /// <param name="recipe">The recipe's station requirement.</param>
    /// <param name="requireNear">Whether physical interaction range is required.</param>
    /// <param name="station">Optional explicit station.</param>
    /// <returns>True for an accessible station and a living village resident.</returns>
    public static bool KitchenAccess(World world, Entity cook, Recipe recipe, bool requireNear = true, Entity? station = null)
    {
        if (!world.TryGet<HealthComponent>(cook, out var health) || health.Current <= 0 || world.Has<DeadComponent>(cook) ||
            !world.TryGet<VillageResidentComponent>(cook, out var resident) || resident is null ||
            !world.TryGet<CookingSettingsComponent>(cook, out var settings) || settings is null) return false;
        station ??= FindStation(world, cook, recipe);
        if (station is null) return false;
        // A public campfire does not grant access to another NPC's inn kitchen.
        if (recipe.ProcessingStation == settings.KitchenStationType &&
            (station.Id != resident.Village.InnId || !world.TryGet<OwnershipComponent>(station, out var owner) || owner?.OwnerId != cook.Id)) return false;
        return ProcessingRules.Host(world, cook, station, recipe, requireNear);
    }

    /// <summary>Finds the nearest compatible station through the existing component index or shared AI station list.</summary>
    /// <param name="world">The cooking world.</param>
    /// <param name="cook">The actor planning travel.</param>
    /// <param name="recipe">The recipe to prepare.</param>
    /// <param name="stations">Optional shared candidate list from this AI update.</param>
    /// <returns>A compatible station, or null when none is accessible.</returns>
    public static Entity? FindStation(World world, Entity cook, Recipe recipe, IEnumerable<Entity>? stations = null)
    {
        if (!world.TryGet<CookingSettingsComponent>(cook, out var settings) || settings is null ||
            !world.TryGet<VillageResidentComponent>(cook, out var resident) || resident is null) return null;
        if (recipe.ProcessingStation == settings.KitchenStationType)
            return world.TryGetEntity(resident.Village.InnId, out var inn) && inn is not null &&
                world.TryGet<OwnershipComponent>(inn, out var owner) && owner?.OwnerId == cook.Id ? inn : null;
        world.TryGet<PositionComponent>(cook, out var position);
        return (stations ?? world.GetEntitiesWith<ProcessingStationComponent>())
            .Where(station => ProcessingRules.Host(world, cook, station, recipe, false))
            .OrderBy(station => world.TryGet<PositionComponent>(station, out var target)
                ? Math.Abs(position.X - target.X) + Math.Abs(position.Y - target.Y) : float.MaxValue).FirstOrDefault();
    }
}
