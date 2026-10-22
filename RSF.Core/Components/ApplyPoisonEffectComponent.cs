using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct ApplyPoisonEffectComponent(
    int DamagePerTick,
    GameDuration TickInterval,
    GameDuration Duration) : IComponent;