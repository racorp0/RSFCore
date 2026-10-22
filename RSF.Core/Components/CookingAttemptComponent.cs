using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores a batch whose ingredients have been consumed; cancelling does not refund them.</summary>
/// <param name="Recipe">The recipe being prepared.</param>
/// <param name="StartedAt">The game minute cooking began.</param>
public sealed record CookingAttemptComponent(Recipe Recipe, GameMinute StartedAt) : IComponent
{
    /// <summary>The actual kitchen or public station; empty preserves legacy manually constructed batches.</summary>
    public EntityId StationId { get; init; } = EntityId.Empty;
}
