using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests departure after a visiting merchant reaches the entrance.</summary>
/// <param name="Merchant">The departing visitor.</param>
public readonly record struct LeaveVillageCommand(Entity Merchant);
