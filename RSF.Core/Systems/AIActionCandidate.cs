using RSF.Core.Common;
using RSF.Core.Entities;
namespace RSF.Core.Systems;

/// <summary>Holds one scored option while survival and work choices are compared together.</summary>
/// <param name="Activity">The gameplay action.</param>
/// <param name="Item">The target entity, including plots and rivers.</param>
/// <param name="SellerId">The seller for a purchase, or empty.</param>
/// <param name="Score">The calculated preference points.</param>
/// <param name="Urgency">The survival category, compared before points.</param>
/// <param name="Quantity">The grain quantity for a purchase, or one.</param>
internal sealed record AIActionCandidate(AIActivity Activity, Entity Item, EntityId SellerId,
    double Score, int Urgency, int Quantity = 1);
