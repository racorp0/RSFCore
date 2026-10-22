using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct VelocityComponent(
    float X,
    float Y) : IComponent;