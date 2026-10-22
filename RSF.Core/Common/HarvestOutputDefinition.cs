namespace RSF.Core.Common;

/// <summary>Defines one named harvest or processing output and its inclusive quantity range in JSON.</summary>
public record HarvestOutputDefinition
{
    /// <summary>Optional food lifetime in game minutes; null leaves non-food products nonperishable.</summary>
    public int? ShelfLifeMinutes { get; init; }
    /// <summary>Optional raw-food effects; null keeps the product inedible without preparation.</summary>
    public RawFoodDefinition? RawFood { get; init; }
    public string ItemId { get; init; } = "";
    public string Name { get; init; } = "";
    public int MinimumQuantity { get; init; } = 1;
    public int MaximumQuantity { get; init; } = 1;
    /// <summary>Gold per unit when an AI worker posts this item for sale.</summary>
    public int SellPrice { get; init; } = 1;
    /// <summary>Desired stock per visiting merchant; zero disables merchant purchases of this item.</summary>
    public int MerchantStockTarget { get; init; } = 10;
    /// <summary>Desired stock per cook when this item appears in a known recipe.</summary>
    public int CookStockTarget { get; init; } = 0;
}

