using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct PoisonStatusComponent(
    int DamagePerTick,
    GameDuration TickInterval,
    GameDuration Duration,
    GameMinute StartedAt,
    GameMinute LastTickAt) : IComponent;