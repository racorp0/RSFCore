using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Plants any JSON-defined crop using matching seeds.</summary>
/// <param name="Farmer">The farmer involved in this farming request.</param>
/// <param name="Plot">The plot involved in this farming request.</param>
/// <param name="Seeds">The seeds involved in this farming request.</param>
public readonly record struct PlantCropCommand(Entity Farmer, Entity Plot, Entity Seeds);
