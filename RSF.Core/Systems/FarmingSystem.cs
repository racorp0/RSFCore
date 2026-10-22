using RSF.Core.Configuration;
using RSF.Core.Blueprints;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>
/// Plows farmland, grows JSON-defined plants using elapsed minutes, and creates their configured harvest outputs.
/// Commands call its actions, and Simulation calls its regular updates.
/// </summary>
public sealed class FarmingSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly InventorySystem _inventorySystem;
    private readonly Random _random;
    private readonly FarmingSettingsComponent _settings;

    /// <summary>Connects farming to the existing world, clock, and inventory.</summary>
    /// <param name="world">The world containing farmers and map tiles.</param>
    /// <param name="clock">The simulation's shared game clock.</param>
    /// <param name="inventorySystem">The existing inventory operations.</param>
    /// <param name="random">Optional repeatable generator for worm rewards.</param>
    public FarmingSystem(
        World world,
        WorldClock clock,
        InventorySystem inventorySystem, Random? random = null)
    {
        _world = world;
        _clock = clock;
        _inventorySystem = inventorySystem;
        _random = random ?? new Random();
        _settings = GameSettings.Find(world)?.Farming ?? new FarmingSettingsComponent();
    }

    /// <summary>Plows unassigned or owned farmland and stores the configured worm reward in inventory.</summary>
    /// <param name="farmer">The living entity doing the work.</param>
    /// <param name="tile">The farmland tile to plow.</param>
    /// <returns>True if a new plot was created; otherwise false.</returns>
    public bool Plow(Entity farmer, Entity tile)
    {
        if (!CanFarm(farmer, tile) || !OwnsOrUnowned(farmer, tile))
        {
            return false;
        }

        // Roads and other zones must keep their existing map purpose.
        if (!_world.TryGet<MapTileComponent>(tile, out var mapTile) ||
            mapTile is null ||
            mapTile.Zone != MapZone.Farmland ||
            _world.Has<FarmPlotComponent>(tile))
        {
            return false;
        }

        if (!_world.TryGet<InventoryComponent>(farmer, out var bag) || bag is null) return false;
        var settings = _world.TryGet<FarmingSettingsComponent>(farmer, out var configured) && configured is not null
            ? configured : _settings;
        if (settings.MinimumWorms < 0 || settings.MaximumWorms < settings.MinimumWorms || settings.MaximumWorms == int.MaxValue) return false;
        // Rewards happen only after validation, so re-plowing an existing plot gives nothing.
        int worms = _random.Next(settings.MinimumWorms, settings.MaximumWorms + 1);
        if (worms > 0)
        {
            Entity bait = new BaitBlueprint().Spawn(_world, worms);
            _world.Add(bait, new ItemComponent("Worms"));
            bag.Items.Add(bait);
        }
        _world.Add(tile, new FarmPlotComponent(null));
        return true;
    }

    /// <summary>Plants a JSON crop, spending seeds only after the plot and matching definition are valid.</summary>
    /// <param name="farmer">The farmer with seeds in inventory.</param>
    /// <param name="plot">The empty plowed farmland.</param>
    /// <param name="seedItem">The seed stack identifying which plant to grow.</param>
    /// <returns>True when a new crop and its lifetime harvest budget were created.</returns>
    public bool PlantCrop(Entity farmer, Entity plot, Entity seedItem)
    {
        if (!CanFarm(farmer, plot) || !OwnsOrUnowned(farmer, plot) || !_world.Has<FarmPlotComponent>(plot) ||
            _world.Has<PlantCropComponent>(plot) ||
            !_world.TryGetEntity(seedItem.Id, out var registered) || registered != seedItem ||
            !_inventorySystem.HasItem(farmer, seedItem) || _world.Has<SaleLocationComponent>(seedItem) ||
            !_world.TryGet<PlantSeedComponent>(seedItem, out var seeds)) return false;
        PlantDefinition? plant = _settings.Plants.FirstOrDefault(p => p.Id == seeds.PlantId);
        if (plant is null || seeds.Quantity < plant.SeedsPerPlanting) return false;
        // Roll once at planting, so retrying a harvest cannot reroll the plant's lifespan.
        int harvests = _random.Next(plant.MinimumHarvests, plant.MaximumHarvests + 1);
        _world.Add(plot, new PlantCropComponent(plant, 0, plant.GrowthDays, _clock.CurrentMinute, false, harvests));
        _world.Add(seedItem, seeds with { Quantity = seeds.Quantity - plant.SeedsPerPlanting });
        if (seeds.Quantity == plant.SeedsPerPlanting)
        { _inventorySystem.RemoveItem(farmer, seedItem); _world.DestroyEntity(seedItem.Id); }
        return true;
    }

    /// <summary>Rolls every configured harvest output, then removes a single-harvest crop or starts repeat regrowth.</summary>
    /// <param name="farmer">The farmer receiving the produce.</param>
    /// <param name="plot">The mature crop's existing tile.</param>
    /// <returns>True only when produce was stored and the crop advanced exactly once.</returns>
    public bool HarvestCrop(Entity farmer, Entity plot)
    {
        if (!CanFarm(farmer, plot) || !OwnsOrUnowned(farmer, plot) || !_world.Has<FarmPlotComponent>(plot) ||
            !_world.TryGet<PlantCropComponent>(plot, out var crop) || !crop.IsMature ||
            !_world.TryGet<InventoryComponent>(farmer, out var bag) || bag is null) return false;
        // Store every configured output before advancing the crop, so failed storage cannot spend a harvest.
        if (!StoreOutputs(farmer, crop.Definition.HarvestOutputs)) return false;
        if (crop.Definition.HarvestMode == PlantHarvestMode.Single)
        {
            _world.Remove<PlantCropComponent>(plot);
            _world.Remove<FarmPlotComponent>(plot);
        }
        else
        {
            // A repeat plant stays rooted. Its last harvest leaves a visible dried crop to clear.
            _world.Add(plot, crop with { GrowthDays = 0, RequiredGrowthDays = crop.Definition.RegrowthDays,
                LastGrowthAt = _clock.CurrentMinute, WasWatered = false, RemainingHarvests = crop.RemainingHarvests - 1,
                HarvestCount = crop.HarvestCount + 1, IsDry = crop.RemainingHarvests == 1 });
        }
        return true;
    }

    /// <summary>Clears an exhausted repeat-harvest plant while preserving the farmland and ownership.</summary>
    /// <param name="farmer">The farmer clearing their plot.</param>
    /// <param name="plot">The tile containing a dried crop.</param>
    /// <returns>True if the next planting now requires fresh plowing.</returns>
    public bool ClearCrop(Entity farmer, Entity plot)
    {
        if (!CanFarm(farmer, plot) || !OwnsOrUnowned(farmer, plot) ||
            !_world.TryGet<PlantCropComponent>(plot, out var crop) || !crop.IsDry) return false;
        _world.Remove<PlantCropComponent>(plot);
        _world.Remove<FarmPlotComponent>(plot);
        return true;
    }

    /// <summary>Applies the existing rolling watering rule to a JSON-defined growing crop.</summary>
    /// <param name="farmer">The living farmer using a tool.</param>
    /// <param name="plot">The growing crop's tile.</param>
    /// <param name="can">The inventory watering can.</param>
    /// <returns>True if watered without bypassing the elapsed-minute cooldown.</returns>
    public bool Water(Entity farmer, Entity plot, Entity can)
    {
        if (!CanFarm(farmer, plot) || !OwnsOrUnowned(farmer, plot) ||
            !_world.TryGet<FarmPlotComponent>(plot, out var farm) || !_world.TryGet<PlantCropComponent>(plot, out var crop) ||
            crop.IsDry || crop.IsMature || crop.WasWatered || !_inventorySystem.HasItem(farmer, can) ||
            !_world.TryGetEntity(can.Id, out var registered) || registered != can || !_world.Has<WateringCanComponent>(can)) return false;
        if (farm.LastWateredAt is GameMinute last &&
            _clock.CurrentMinute.ElapsedSince(last).Minutes < crop.Definition.WateringIntervalMinutes) return false;
        _world.Add(plot, farm with { LastWateredAt = _clock.CurrentMinute });
        _world.Add(plot, crop with { WasWatered = true });
        return true;
    }

    /// <summary>Processes a crop's elapsed initial-growth or regrowth intervals using the shared watered progress rule.</summary>
    /// <param name="plot">The existing tile to update.</param>
    /// <param name="crop">The crop snapshot read before updating.</param>
    private void UpdatePlant(Entity plot, PlantCropComponent crop)
    {
        if (crop.IsDry || crop.IsMature || !_world.Has<FarmPlotComponent>(plot)) return;
        while (!crop.IsMature && _clock.CurrentMinute.ElapsedSince(crop.LastGrowthAt).Minutes >= crop.Definition.GrowthIntervalMinutes)
        {
            crop = crop with { GrowthDays = crop.WasWatered ? Math.Min(crop.GrowthDays + 1, crop.RequiredGrowthDays) : Math.Max(0, crop.GrowthDays - 1),
                LastGrowthAt = crop.LastGrowthAt.Add(crop.Definition.GrowthIntervalMinutes), WasWatered = false };
        }
        _world.Add(plot, crop);
    }

    /// <summary>Prevents generic crop actions from taking another farmer's assigned plot.</summary>
    /// <param name="farmer">The acting farmer.</param>
    /// <param name="plot">The target tile.</param>
    /// <returns>True for unassigned tiles or tiles belonging to this farmer.</returns>
    private bool OwnsOrUnowned(Entity farmer, Entity plot) => !_world.TryGet<OwnershipComponent>(plot, out var owner) || owner is null || owner.OwnerId == farmer.Id;

    /// <summary>Creates configured product stacks, applies optional raw-food effects, and rolls back failed storage.</summary>
    /// <param name="farmer">The actor receiving the output items.</param>
    /// <param name="outputs">The validated harvest or processing output definitions.</param>
    /// <returns>True if the complete output batch was stored.</returns>
    private bool StoreOutputs(Entity farmer, IReadOnlyList<HarvestOutputDefinition> outputs)
    {
        List<Entity> created = new();
        foreach (var output in outputs)
        {
            Entity item = _world.CreateEntity();
            created.Add(item);
            _world.Add(item, new ItemComponent(output.Name));
            _world.Add(item, new PlantProduceComponent(output.ItemId, _random.Next(output.MinimumQuantity, output.MaximumQuantity + 1)));
            RawFoodRules.Apply(_world, item);
            if (_inventorySystem.AddItem(farmer, item)) continue;
            foreach (var failed in created) { _inventorySystem.RemoveItem(farmer, failed); _world.DestroyEntity(failed.Id); }
            return false;
        }
        return true;
    }

    /// <summary>Processes indexed crops' rolling elapsed-minute growth, including initial growth and regrowth.</summary>
    /// <param name="deltaTime">The simulation step; elapsed time is read from the shared clock.</param>
    public void Update(float deltaTime)
    {
        // Skip terrain without crops. Growth replaces existing crop data without adding or removing indexed entries.
        foreach (Entity plot in _world.GetEntitiesWith<PlantCropComponent>())
            if (_world.TryGet<PlantCropComponent>(plot, out var crop)) UpdatePlant(plot, crop);
    }

    /// <summary>Rejects invalid actors and requires village farmers to reach their target map tile.</summary>
    /// <param name="farmer">The entity requesting the action.</param>
    /// <param name="plot">The target tile or inventory processing input.</param>
    /// <returns>True when both entities are valid and any required physical interaction is in range.</returns>
    private bool CanFarm(Entity farmer, Entity plot)
    {
        return _world.TryGetEntity(farmer.Id, out var registeredFarmer) &&
            registeredFarmer == farmer &&
            _world.TryGetEntity(plot.Id, out var registeredPlot) &&
            registeredPlot == plot &&
            !_world.Has<DeadComponent>(farmer) &&
            _world.TryGet<HealthComponent>(farmer, out var health) &&
            health.Current > 0 &&
            (!_world.Has<MapTileComponent>(plot) || VillageRules.Near(_world, farmer, plot.Id));
    }
}
