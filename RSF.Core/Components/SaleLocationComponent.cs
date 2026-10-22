using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Records goods deposited at a market stall or inn counter, still owned by their seller.</summary>
/// <param name="PlaceId">The physical sale location; the seller need not stay at the counter.</param>
public readonly record struct SaleLocationComponent(EntityId PlaceId) : IComponent;
