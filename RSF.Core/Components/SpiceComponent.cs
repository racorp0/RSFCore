namespace RSF.Core.Components;

/// <summary>Stores ingredient spice quantity in an inventory item.</summary>
/// <param name="Quantity">The available units of spice.</param>
public readonly record struct SpiceComponent(int Quantity) : IComponent;
