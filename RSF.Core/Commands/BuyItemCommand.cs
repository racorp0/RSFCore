using RSF.Core.Entities;

namespace RSF.Core.Commands;

/// <summary>Requests buying an offered item from another NPC through the command dispatcher.</summary>
/// <param name="Buyer">The NPC paying gold.</param>
/// <param name="Seller">The NPC offering the item.</param>
/// <param name="Item">The item currently in the seller's inventory.</param>
/// <param name="Quantity">The number of grains, or one for an individual item.</param>
public readonly record struct BuyItemCommand(Entity Buyer, Entity Seller, Entity Item, int Quantity = 1);
