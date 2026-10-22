using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores a cached grid route; TravelSystem supplies velocities to MovementSystem.</summary>
public sealed class TravelComponent : IComponent
{
    public EntityId DestinationId { get; init; }
    public Queue<(int X, int Y)> Steps { get; } = new();
    public GameMinute NextStepAt { get; set; }
}
