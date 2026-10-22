using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct RegenerationStatusComponent(
    int HealPerTick,
    GameDuration TickInterval,
    GameDuration Duration,
    GameMinute StartedAt,
    GameMinute LastTickAt) : IComponent;