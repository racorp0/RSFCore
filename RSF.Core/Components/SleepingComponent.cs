using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Records where an NPC sleeps and when the last recovery tick happened.</summary>
/// <param name="PlaceId">The owned house or paid inn.</param>
/// <param name="LastTickAt">The most recent recovery minute.</param>
public readonly record struct SleepingComponent(EntityId PlaceId, GameMinute LastTickAt) : IComponent;
