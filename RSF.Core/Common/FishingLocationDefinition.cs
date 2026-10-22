namespace RSF.Core.Common;

/// <summary>Describes a JSON fishing spot that the village blueprint attaches to an existing map tile.</summary>
public sealed record FishingLocationDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>The water category: river, lake, or sea.</summary>
    public string WaterType { get; init; } = "river";
    public int X { get; init; }
    public int Y { get; init; }
    public int MinimumCatchMinutes { get; init; } = 5;
    public int MaximumCatchMinutes { get; init; } = 240;
    /// <summary>Species available here, referring to the shared fishTypes definitions.</summary>
    public IReadOnlyList<string> Species { get; init; } = Array.Empty<string>();
    /// <summary>Absolute species chances grouped by bait ID. Omitted bait types cannot be used here.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, int>> BaitCatchChances { get; init; } = new Dictionary<string, Dictionary<string, int>>();
}
