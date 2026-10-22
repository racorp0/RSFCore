using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests the start action through the existing command dispatcher.</summary>
/// <param name="Npc">The npc for this processing action.</param>
/// <param name="Host">The host for this processing action.</param>
/// <param name="RecipeIndex">The recipeindex for this processing action.</param>
public readonly record struct StartProcessingCommand(Entity Npc, Entity Host, int RecipeIndex);
