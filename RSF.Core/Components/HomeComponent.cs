using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Identifies a resident's owned house.</summary>
/// <param name="HouseId">The entity representing the house.</param>
public readonly record struct HomeComponent(EntityId HouseId) : IComponent;
