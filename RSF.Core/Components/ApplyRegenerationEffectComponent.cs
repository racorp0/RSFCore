using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct ApplyRegenerationEffectComponent(
    int HealPerTick,
    GameDuration TickInterval,
    GameDuration Duration
) : IComponent;
