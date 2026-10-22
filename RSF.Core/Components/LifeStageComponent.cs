using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores the life stage used to select schedules and allowed AI activities.</summary>
/// <param name="Stage">The current life stage.</param>
public readonly record struct LifeStageComponent(LifeStage Stage) : IComponent;
