using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests an advance-paid daily inn bed.</summary>
/// <param name="Guest">The guest paying rent.</param>
/// <param name="Inn">The inn being rented.</param>
public readonly record struct RentBedCommand(Entity Guest, Entity Inn);
