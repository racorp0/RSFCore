namespace RSF.Core.Components;

/// <summary>Stores the number of ordinary stack units consumed by one food use; effects apply once per serving.</summary>
/// <param name="Quantity">Positive stack units in a complete serving.</param>
public readonly record struct FoodServingComponent(int Quantity) : IComponent;
