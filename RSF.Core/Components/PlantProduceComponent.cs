namespace RSF.Core.Components;

/// <summary>Stores a harvested or processed item stack with its JSON item ID for inventory, cooking, and trading.</summary>
/// <param name="ItemId">The stable output item ID from a harvest or processing definition.</param>
/// <param name="Quantity">The stack units: grams for configured fish meat, item count for ordinary produce.</param>
public readonly record struct PlantProduceComponent(string ItemId, int Quantity) : IComponent;
