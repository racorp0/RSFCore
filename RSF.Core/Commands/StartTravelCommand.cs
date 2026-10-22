using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests a cached route to a physical destination.</summary>
/// <param name="Npc">The acting NPC.</param>
/// <param name="Destination">The destination entity.</param>
public readonly record struct StartTravelCommand(Entity Npc, Entity Destination);
