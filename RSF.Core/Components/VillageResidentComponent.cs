namespace RSF.Core.Components;

/// <summary>Opts a resident or visitor into village locations, travel, and trade rules.</summary>
/// <param name="Village">The shared village configuration and map data.</param>
public sealed record VillageResidentComponent(VillageComponent Village) : IComponent;
