using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests the pause action through the existing command dispatcher.</summary>
/// <param name="Npc">The npc for this processing action.</param>
public readonly record struct PauseProcessingCommand(Entity Npc);
