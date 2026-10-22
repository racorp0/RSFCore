using RSF.Core.Entities;

namespace RSF.Core.Commands;

/// <summary>Requests that a farmer plow an existing farmland tile.</summary>
/// <param name="Farmer">The entity performing the work.</param>
/// <param name="Tile">The farmland tile to turn into a plot.</param>
public readonly record struct PlowCommand(Entity Farmer, Entity Tile);
