namespace RSF.Core.Components;

public readonly record struct HungerComponent(
    int Current,
    int Max) : IComponent;