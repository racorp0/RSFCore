using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores a bed entitlement paid in advance for exact 1440-minute rental periods.</summary>
/// <param name="InnId">The rented inn.</param>
/// <param name="PaidUntil">The exclusive end of the current paid period.</param>
public readonly record struct InnStayComponent(EntityId InnId, GameMinute PaidUntil) : IComponent;
