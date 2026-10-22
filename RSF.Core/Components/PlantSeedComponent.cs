namespace RSF.Core.Components;

/// <summary>Identifies a seed stack by its JSON plant ID, so adding species requires no new component type.</summary>
/// <param name="PlantId">The stable ID from the plant definition.</param>
/// <param name="Quantity">The number of seeds or harvested items in the stack.</param>
public readonly record struct PlantSeedComponent(string PlantId, int Quantity) : IComponent;
