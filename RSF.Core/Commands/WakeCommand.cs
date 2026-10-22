using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests waking before another activity.</summary>
/// <param name="Npc">The acting NPC.</param>
public readonly record struct WakeCommand(Entity Npc);
