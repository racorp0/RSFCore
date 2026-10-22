using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests sleep at an owned home or paid bed.</summary>
/// <param name="Npc">The acting NPC.</param>
/// <param name="Place">The sleeping place.</param>
public readonly record struct SleepCommand(Entity Npc, Entity Place);
