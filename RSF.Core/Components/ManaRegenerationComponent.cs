using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct ManaRegenerationComponent(
    int ManaPerTick,
    GameDuration TickInterval,
    GameMinute LastTickAt
) : IComponent;