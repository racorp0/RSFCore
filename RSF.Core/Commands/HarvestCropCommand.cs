using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Harvests a mature JSON-defined crop without bypassing farming validation.</summary>
/// <param name="Farmer">The farmer involved in this farming request.</param>
/// <param name="Plot">The plot involved in this farming request.</param>
public readonly record struct HarvestCropCommand(Entity Farmer, Entity Plot);
