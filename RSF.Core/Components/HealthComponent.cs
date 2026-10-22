namespace RSF.Core.Components;

/// <summary>
/// Stores health information for an entity.
/// This component contains data only.
/// </summary>
public readonly record struct HealthComponent(
    int Current,
    int Max
) : IComponent;