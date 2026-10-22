namespace RSF.Core.Components;

/// <summary>Stores cooking skill; initial recipes require level one.</summary>
/// <param name="Level">Current cooking skill level.</param>
public readonly record struct CookingSkillComponent(int Level) : IComponent;
