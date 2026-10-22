using RSF.Core.Entities;

namespace RSF.Core.Commands;

/// <summary>Requests one timed fishing attempt through RSF's command dispatcher.</summary>
/// <param name="Fisher">The living NPC attempting to fish.</param>
/// <param name="River">The river containing possible catches.</param>
/// <param name="Rod">The rod in the fisher's inventory.</param>
/// <param name="Bait">The bait supply to spend one piece from.</param>
public readonly record struct StartFishingCommand(Entity Fisher, Entity River, Entity Rod, Entity Bait);
