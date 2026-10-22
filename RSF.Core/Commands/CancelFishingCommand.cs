using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests ending an active fishing attempt without refunding its spent bait.</summary>
/// <param name="Fisher">The NPC whose fishing should stop.</param>
public readonly record struct CancelFishingCommand(Entity Fisher);
