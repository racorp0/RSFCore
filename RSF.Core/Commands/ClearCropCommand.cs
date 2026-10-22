using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Clears a dried crop before the next plowing cycle.</summary>
/// <param name="Farmer">The farmer involved in this farming request.</param>
/// <param name="Plot">The plot involved in this farming request.</param>
public readonly record struct ClearCropCommand(Entity Farmer, Entity Plot);
