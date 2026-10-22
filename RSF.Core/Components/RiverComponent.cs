using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Stores a fishing water's catch table; the existing river component also represents lakes and sea spots.</summary>
/// <param name="FishTypes">Available species, catch chances, and weight ranges.</param>
public sealed record RiverComponent(IReadOnlyList<RiverFishDefinition> FishTypes) : IComponent
{
    /// <summary>Configured village fishing spots; the root settings own this list.</summary>
    public IReadOnlyList<FishingLocationDefinition> Locations { get; init; } = Array.Empty<FishingLocationDefinition>();
    /// <summary>The JSON spot identity; empty for older manually created water.</summary>
    public string LocationId { get; init; } = "";
    /// <summary>The water category displayed by debugging utilities.</summary>
    public string WaterType { get; init; } = "river";

    /// <summary>JSON bait types with their river catch chances; empty preserves manual worm fishing.</summary>
    public IReadOnlyList<BaitDefinition> Baits { get; init; } = Array.Empty<BaitDefinition>();

    /// <summary>The shortest possible wait, measured in game minutes.</summary>
    public int MinimumCatchMinutes { get; init; } = 5;
    /// <summary>The longest possible wait, measured in game minutes.</summary>
    public int MaximumCatchMinutes { get; init; } = 240;
}
