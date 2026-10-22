using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Blueprints;

/// <summary>Creates map tiles using loaded dimensions and standard parcels, retaining legacy manual setup.</summary>
public static class MapBlueprint
{
    /// <summary>Creates the configured grid, blocking building footprints except their entrances while preserving farm crop tiles.</summary>
    /// <param name="world">The world receiving map tile entities.</param>
    public static void Create(World world)
    {
        var settings = GameSettings.Find(world);
        var layout = settings?.Map;
        layout?.Validate();
        int width = layout?.Width ?? 32;
        int height = layout?.Height ?? 20;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                MapZone zone;

                if (layout is not null)
                {
                    var plot = layout.PlotAt(x, y);
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                        zone = x == 0 && y == layout.EntranceY ? MapZone.Road : MapZone.Blocked;
                    else if (plot is not null)
                        zone = plot.Kind == MapPlotKind.Farm ? MapZone.Farmland
                            : x == plot.EntranceX && y == plot.EntranceY ? MapZone.Building : MapZone.Blocked;
                    else if ((x == layout.MarketX && y == layout.MarketY) || settings!.Processing.Stations.Any(station => station.X == x && station.Y == y))
                        zone = MapZone.Building;
                    else zone = MapZone.Road;
                }
                else if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                {
                    zone = x == 0 && y == 10 ? MapZone.Road : MapZone.Blocked;
                }
                else if (x >= 2 && x <= 7 && y >= 2 && y <= 5)
                {
                    zone = MapZone.Farmland;
                }
                else if (x >= 2 && x <= 7 && y == 15)
                {
                    zone = MapZone.Water;
                }
                else if (x >= 12 && x <= 28 && y >= 3 && y <= 16)
                {
                    zone = MapZone.Building;
                }
                else
                {
                    zone = MapZone.Road;
                }

                Entity tile = world.CreateEntity();

                world.Add(
                    tile,
                    new PositionComponent(x, y));

                world.Add(
                    tile,
                    new MapTileComponent(zone));
            }
        }
    }
}