namespace RSF.Core.Components;

public readonly record struct ManaComponent(
    int Current,
    int Max) : IComponent;