namespace RSF.Core.Components;

/// <summary>Stores bait quantity; one bait is consumed when a fishing attempt starts.</summary>
/// <param name="BaitId">The JSON bait identity, retained when pieces are spent or traded.</param>
/// <param name="Quantity">The number of bait pieces remaining.</param>
public readonly record struct BaitComponent(int Quantity, string BaitId = "worm") : IComponent;
