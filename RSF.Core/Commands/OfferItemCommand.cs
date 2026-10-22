using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests offering an owned inventory item for sale.</summary>
/// <param name="Seller">The NPC offering the item.</param>
/// <param name="Item">The item in inventory.</param>
/// <param name="GoldPerUnit">The positive price for one unit.</param>
public readonly record struct OfferItemCommand(Entity Seller, Entity Item, int GoldPerUnit);
