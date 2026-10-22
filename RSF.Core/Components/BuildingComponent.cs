using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Marks a village place; ownership remains in OwnershipComponent.</summary>
/// <param name="Kind">The place's purpose.</param>
/// <param name="Capacity">Number of available rental beds for an inn.</param>
public sealed record BuildingComponent(BuildingKind Kind, int Capacity = 1) : IComponent;
