namespace RSF.Core.Components;

/// <summary>Stores tiredness from zero to 100; high values mean the NPC needs sleep.</summary>
/// <param name="Value">Current tiredness.</param>
public readonly record struct FatigueComponent(int Value) : IComponent;
