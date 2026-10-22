using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests cancelling an obsolete route and its velocity.</summary>
/// <param name="Npc">The NPC changing activities.</param>
public readonly record struct StopTravelCommand(Entity Npc);
