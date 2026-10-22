using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates river, lake, and sea fishing spots using the existing water component and shared JSON species.</summary>
public sealed class RiverBlueprint
{
    /// <summary>Creates a fishing spot or reuses its map tile, selecting a configured location table when requested.</summary>
    /// <param name="world">The world containing the river.</param>
    /// <param name="name">The river's display name.</param>
    /// <param name="x">The river's horizontal position.</param>
    /// <param name="y">The river's vertical position.</param>
    /// <param name="locationId">Optional configured spot identity; omitted preserves the existing river workflow.</param>
    /// <returns>The water entity carrying the selected catch table.</returns>
    public Entity Spawn(World world, string name, float x, float y, string? locationId = null)
    {
        var settings = GameSettings.Find(world)?.Fishing;
        var location = locationId is null ? null : settings?.Locations.FirstOrDefault(spot => spot.Id == locationId);
        if (locationId is not null && location is null) throw new ArgumentException("Unknown fishing location.", nameof(locationId));
        if (location is not null) { name = location.Name; x = location.X; y = location.Y; }
        // Reusing the map tile prevents duplicate coordinates and keeps travel's water cells accurate.
        Entity river = world.GetEntities().FirstOrDefault(entity => world.Has<MapTileComponent>(entity) &&
            world.TryGet<PositionComponent>(entity, out var position) && position.X == x && position.Y == y) ?? world.CreateEntity();
        world.Add(river, new NameComponent(name));
        world.Add(river, new PositionComponent(x, y));
        world.Add(river, new MapTileComponent(MapZone.Water));
        // Manual worlds retain the original 90-percent table; configured spots receive their local table below.
        world.Add(river, settings ?? new RiverComponent(new List<RiverFishDefinition>
        {
            new("Carp", 45, 500, 3000),
            new("Catfish", 30, 1000, 5000),
            new("Tilapia", 15, 200, 1500)
        }.AsReadOnly()));
        if (location is not null && settings is not null)
        {
            // Build the existing runtime table from shared species metadata and local bait probabilities.
            world.Add(river, new RiverComponent(settings.FishTypes.Where(fish => location.Species.Contains(fish.Species)).ToArray())
            {
                LocationId = location.Id, WaterType = location.WaterType,
                MinimumCatchMinutes = location.MinimumCatchMinutes, MaximumCatchMinutes = location.MaximumCatchMinutes,
                Baits = settings.Baits.Where(bait => location.BaitCatchChances.ContainsKey(bait.Id))
                    .Select(bait => bait with { CatchChances = location.BaitCatchChances[bait.Id] }).ToArray()
            });
        }
        return river;
    }
}
