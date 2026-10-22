using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Stores one active fishing attempt on its fisher; Simulation advances the shared clock.</summary>
/// <param name="RiverId">The river being fished.</param>
/// <param name="RodId">The rod that must remain in inventory.</param>
/// <param name="StartedAt">The game minute the attempt began.</param>
/// <param name="BaitId">The consumed bait type, retained after its stack runs out.</param>
/// <param name="Duration">The randomly selected wait before resolving the catch.</param>
public readonly record struct FishingAttemptComponent(
    EntityId RiverId,
    EntityId RodId,
    GameMinute StartedAt,
    GameDuration Duration,
    string BaitId = "worm") : IComponent;
