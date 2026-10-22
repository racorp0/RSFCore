using RSF.Core.Entities;

namespace RSF.Core.Systems;

/// <summary>Groups one seller's usable consumable entries for this AI update; existing components still hold prices.</summary>
/// <param name="Seller">The inventory owner in the existing world order.</param>
/// <param name="Items">Usable inventory entries in their original order, including duplicates.</param>
internal readonly record struct AISurvivalOffer(Entity Seller, IReadOnlyList<Entity> Items);
