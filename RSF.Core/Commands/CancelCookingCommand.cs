using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests interruption of a cooking batch without ingredient refunds.</summary>
/// <param name="Cook">The acting cook.</param>
public readonly record struct CancelCookingCommand(Entity Cook);
