using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>
/// Marks an existing farmland tile as plowed and remembers when it was last watered.
/// </summary>
/// <param name="LastWateredAt">The last watering minute, or null if never watered.</param>
public readonly record struct FarmPlotComponent(
    GameMinute? LastWateredAt) : IComponent;
