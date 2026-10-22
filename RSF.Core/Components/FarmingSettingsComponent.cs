using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores JSON plant definitions, and plowing rewards; systems perform all actions.</summary>
public sealed record FarmingSettingsComponent : IComponent
{
    public IReadOnlyList<PlantDefinition> Plants { get; init; } = Array.Empty<PlantDefinition>();
    public int MinimumWorms { get; init; } = 0;
    public int MaximumWorms { get; init; } = 10;
}
