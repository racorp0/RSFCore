namespace RSF.Core.Components;

/// <summary>Marks an inventory item as offered for sale at a price per unit.</summary>
/// <param name="GoldPerUnit">The positive price for one loaf, fish, or grain.</param>
public readonly record struct TradePriceComponent(int GoldPerUnit) : IComponent;
