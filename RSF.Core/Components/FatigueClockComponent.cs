using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Tracks awake elapsed time independently for each NPC.</summary>
/// <param name="LastTickAt">The most recent fatigue increase.</param>
public readonly record struct FatigueClockComponent(GameMinute LastTickAt) : IComponent;
