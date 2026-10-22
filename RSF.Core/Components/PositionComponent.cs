namespace RSF.Core.Components;

public readonly record struct PositionComponent(
    float X,
    float Y) : IComponent;