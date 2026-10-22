using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct CooldownComponent(
    GameMinute StartedAt,
    GameDuration Duration
) : IComponent;