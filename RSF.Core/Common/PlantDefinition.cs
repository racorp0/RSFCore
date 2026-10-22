namespace RSF.Core.Common;

/// <summary>Describes the supported harvest lifecycle independently of a plant's name or category.</summary>
public enum PlantHarvestMode { Single, Repeat }

/// <summary>Defines a JSON plant's growth, harvest, seed supply, and market values; FarmingSystem performs its actions.</summary>
public sealed record PlantDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>A descriptive category, such as Vegetable; new category names need no C# enum changes.</summary>
    public string Type { get; init; } = "Vegetable";
    public PlantHarvestMode HarvestMode { get; init; } = PlantHarvestMode.Single;
    public int GrowthDays { get; init; } = 4;
    public int RegrowthDays { get; init; } = 2;
    public int GrowthIntervalMinutes { get; init; } = 1440;
    public int WateringIntervalMinutes { get; init; } = 1440;
    public int SeedsPerPlanting { get; init; } = 1;
    public int MinimumHarvests { get; init; } = 1;
    public int MaximumHarvests { get; init; } = 1;
    /// <summary>Every successful harvest independently rolls each listed item quantity.</summary>
    public IReadOnlyList<HarvestOutputDefinition> HarvestOutputs { get; init; } = Array.Empty<HarvestOutputDefinition>();
    public int StartingSeedsPerFarmer { get; init; } = 3;
    public int MerchantSeeds { get; init; } = 20;
    public int SeedStockTarget { get; init; } = 3;
    public int SeedPrice { get; init; } = 2;
    /// <summary>Preferred active plots per farmer; available land and seeds still limit planting.</summary>
    public int PreferredPlotsPerFarmer { get; init; } = 1;
}
