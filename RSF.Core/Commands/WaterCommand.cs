using RSF.Core.Entities;

namespace RSF.Core.Commands;

/// <summary>Requests watering a planted plot using an inventory tool.</summary>
/// <param name="Farmer">The entity watering the crop.</param>
/// <param name="Plot">The planted tile.</param>
/// <param name="WateringCan">The watering can held in inventory.</param>
public readonly record struct WaterCommand(Entity Farmer, Entity Plot, Entity WateringCan);
