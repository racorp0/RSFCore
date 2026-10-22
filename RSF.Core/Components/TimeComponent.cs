using RSF.Core.Common;

namespace RSF.Core.Components;

public readonly record struct TimeComponent(
    GameMinute CurrentMinute) : IComponent;