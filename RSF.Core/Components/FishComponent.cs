namespace RSF.Core.Components;

/// <summary>Stores the species and individual weight of a caught fish inventory item.</summary>
/// <param name="Species">The caught fish's species.</param>
/// <param name="WeightGrams">The fish's weight in whole grams.</param>
public readonly record struct FishComponent(string Species, int WeightGrams) : IComponent;
