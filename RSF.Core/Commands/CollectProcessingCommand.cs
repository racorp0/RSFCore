using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests the collect action through the existing command dispatcher.</summary>
/// <param name="Npc">The npc for this processing action.</param>
/// <param name="Host">The host for this processing action.</param>
public readonly record struct CollectProcessingCommand(Entity Npc, Entity Host);
