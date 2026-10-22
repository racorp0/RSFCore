namespace RSF.Core.Components;

/// <summary>Records fishing ability; this version requires level one and does not award experience.</summary>
/// <param name="Level">The fisher's current skill level.</param>
public readonly record struct FishingSkillComponent(int Level) : IComponent;
