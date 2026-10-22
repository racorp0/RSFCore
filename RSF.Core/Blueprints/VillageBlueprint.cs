using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Blueprints;

/// <summary>Creates a larger map and shared village places without scripting NPC behavior.</summary>
public static class VillageBlueprint
{
    /// <summary>Builds existing village places and attaches independent local price data to the market.</summary>
    /// <param name="world">The initially empty world.</param>
    /// <param name="settings">Optional village economy, travel, and visitor settings.</param>
    /// <param name="marketSettings">Optional local market override; omitted uses loaded economy settings or manual defaults.</param>
    /// <returns>The entity containing shared village data.</returns>
    public static Entity Create(World world, VillageComponent? settings = null, MarketSettings? marketSettings = null)
    {
        // Validate before creating map entities, and give every market its own mutable state.
        MarketPriceComponent marketPrices = (marketSettings ?? GameSettings.Find(world)?.Economy.Market ?? new MarketSettings()).CreateMarket();
        VillageComponent village = settings ?? GameSettings.Find(world)?.Economy.CreateVillage() ?? new VillageComponent();
        if (village.InnBeds <= 0 || village.MerchantBread < 0 || village.MerchantFishTarget < 0 ||
            village.MinutesPerTravelTile <= 0 || village.RentPerDay <= 0 || village.MinMerchantDays < 1 ||
            village.MaxMerchantDays < village.MinMerchantDays || village.MaxMerchantDays > 3650 ||
            village.MaxMerchants < 0 || village.MerchantIdleMinutes <= 0 || village.MaxVisitMinutes <= 0 ||
            village.MerchantGold < 0 || village.MerchantBait < 0 || village.MerchantSpice < 0 ||
            village.BaitPrice <= 0 || village.SpicePrice <= 0)
            throw new ArgumentException("Village settings must have valid positive times, prices, and stock ranges.");
        if (world.GetEntities().Any(e => world.Has<MapTileComponent>(e)))
            throw new InvalidOperationException("Create the village before other map tiles.");
        MapBlueprint.Create(world);
        var fishing = GameSettings.Find(world)?.Fishing;
        if (fishing is not null && fishing.Locations.Count > 0)
        {
            foreach (var location in fishing.Locations)
                new RiverBlueprint().Spawn(world, location.Name, location.X, location.Y, location.Id);
        }
        else new RiverBlueprint().Spawn(world, "Village River", 4, 15);
        var layout = GameSettings.Find(world)?.Map;
        Entity market;
        if (layout is not null)
        {
            village.Width = layout.Width; village.Height = layout.Height; village.MetresPerTile = layout.MetresPerTile;
            foreach (var plot in layout.Plots)
            {
                Entity anchor;
                if (plot.Kind == MapPlotKind.Farm)
                {
                    anchor = world.GetEntities().First(entity => world.TryGet<MapTileComponent>(entity, out _) &&
                        world.TryGet<PositionComponent>(entity, out var position) && position.X == plot.X && position.Y == plot.Y);
                    world.Add(anchor, new NameComponent(plot.Name));
                }
                else
                {
                    BuildingKind kind = plot.Kind == MapPlotKind.House ? BuildingKind.House
                        : plot.Kind == MapPlotKind.Inn ? BuildingKind.Inn : BuildingKind.Workshop;
                    anchor = Place(plot.EntranceX, plot.EntranceY, plot.Name, kind, kind == BuildingKind.Inn ? village.InnBeds : 1);
                    if (kind == BuildingKind.Inn) village.InnId = anchor.Id;
                }
                world.Add(anchor, new MapPlotComponent(plot.Id, plot.Kind, plot.X, plot.Y, layout.PlotWidth, layout.PlotHeight, plot.Floors));
            }
            market = Place(layout.MarketX, layout.MarketY, "Village Market", BuildingKind.Market);
            village.MarketId = market.Id;
        }
        else
        {
            market = Place(16, 8, "Village Market", BuildingKind.Market);
            village.MarketId = market.Id;
            village.InnId = Place(22, 8, "Village Inn", BuildingKind.Inn, village.InnBeds).Id;
        }
        world.Add(market, marketPrices);
        world.Add(market, new MarketActivityComponent());
        if (GameSettings.Find(world) is GameSettings configuration && world.TryGetEntity(village.InnId, out var kitchen) && kitchen is not null)
            world.Add(kitchen, new ProcessingStationComponent(configuration.Cooking.KitchenStationType));
        village.EntranceId = Place(0, layout?.EntranceY ?? 10, "Village Entrance", BuildingKind.Entrance).Id;
        // Reuse buildable tiles as generic machinery so houses cannot be assigned on top of stations.
        foreach (var station in GameSettings.Find(world)?.Processing.Stations ?? Array.Empty<ProcessingStationDefinition>())
        {
            var tile = world.GetEntities().FirstOrDefault(e => world.Has<MapTileComponent>(e) &&
                world.TryGet<PositionComponent>(e, out var p) && p.X == station.X && p.Y == station.Y);
            if (tile is null || world.Has<BuildingComponent>(tile) || !world.TryGet<MapTileComponent>(tile, out var zone) || zone is null || zone.Zone != MapZone.Building)
                throw new ArgumentException($"processing.json: station {station.Id} must use an unoccupied Building tile.");
            world.Add(tile, new BuildingComponent(BuildingKind.Workshop));
            world.Add(tile, new ProcessingStationComponent(station.StationType));
            world.Add(tile, new NameComponent(station.Name));
        }
        foreach (Entity tile in world.GetEntities())
            if (world.TryGet<MapTileComponent>(tile, out var map) && map is not null &&
                world.TryGet<PositionComponent>(tile, out var pos)) village.Cells[((int)pos.X, (int)pos.Y)] = map.Zone;
        Entity root = world.CreateEntity();
        world.Add(root, village);
        world.Add(root, new NameComponent("Village"));
        return root;

        /// <summary>Reuses a map tile as a building or entrance instead of creating conflicting grid tiles.</summary>
        /// <param name="x">The tile's column.</param>
        /// <param name="y">The tile's row.</param>
        /// <param name="name">The place name.</param>
        /// <param name="kind">The purpose of the place.</param>
        /// <param name="capacity">The available rental beds.</param>
        /// <returns>The existing tile carrying the new place data.</returns>
        Entity Place(int x, int y, string name, BuildingKind kind, int capacity = 1)
        {
            Entity tile = world.GetEntities().First(e => world.TryGet<MapTileComponent>(e, out _) &&
                world.TryGet<PositionComponent>(e, out var pos) && pos.X == x && pos.Y == y);
            world.Add(tile, new NameComponent(name));
            world.Add(tile, new BuildingComponent(kind, capacity));
            return tile;
        }
    }
}
