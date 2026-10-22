namespace RSF.Core.Common;

/// <summary>Defines optional raw-food restoration per serving of a JSON product.</summary>
public sealed record RawFoodDefinition
{
    public int Hunger { get; init; }
    public int Health { get; init; }
    /// <summary>One for a tomato; quantities use the product's existing count or gram unit.</summary>
    public int QuantityPerServing { get; init; } = 1;
}
