using RSF.Core.Common;
using RSF.Core.Simulation;
namespace RSF.Core.Components;

/// <summary>Stores a fresh food batch's original creation time and absolute spoilage deadline.</summary>
/// <param name="CreatedAt">The game minute this food was created.</param>
/// <param name="ExpiresAt">Food is unusable at this minute and later.</param>
public readonly record struct FoodExpirationComponent(GameMinute CreatedAt, GameMinute ExpiresAt) : IComponent;

/// <summary>Retains a spoiled item's deadline for inspection after it leaves the fresh-food update index.</summary>
/// <param name="ExpiredAt">The original deadline, preserved even if the update observes it later.</param>
public readonly record struct SpoiledFoodComponent(GameMinute ExpiredAt) : IComponent;

/// <summary>Exposes the existing simulation clock to food creation and command validation; it does not own another clock.</summary>
/// <param name="Clock">The simulation's shared clock.</param>
public sealed record FoodClockComponent(WorldClock Clock) : IComponent;
