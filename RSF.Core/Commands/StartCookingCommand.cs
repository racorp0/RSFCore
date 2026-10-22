using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests a timed recipe batch at an owned kitchen or compatible public station.</summary>
/// <param name="Cook">The acting cook.</param>
/// <param name="RecipeIndex">The recipe index in personal cooking settings.</param>
/// <param name="Station">Optional selected station; omitted preserves existing callers.</param>
public readonly record struct StartCookingCommand(Entity Cook, int RecipeIndex, Entity? Station = null);
