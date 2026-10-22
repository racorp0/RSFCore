namespace RSF.Core.Common;

/// <summary>Defines one JSON bait type, its finite merchant stock, and species chances for the existing fishing system.</summary>
public sealed record BaitDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int Price { get; init; } = 1;
    public int MerchantStock { get; init; }
    /// <summary>Absolute catch percentages keyed by species; missing species have zero chance.</summary>
    public IReadOnlyDictionary<string, int> CatchChances { get; init; } = new Dictionary<string, int>();
}
