using RSF.Core.Blueprints;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Transfers offered inventory items and gold immediately when a direct purchase is valid.</summary>
public sealed class TradeSystem
{
    private readonly World _world;
    private readonly InventorySystem _inventorySystem;

    /// <summary>Reuses the world's component storage and existing inventory operations.</summary>
    /// <param name="world">The world containing both trading NPCs.</param>
    /// <param name="inventorySystem">Existing inventory operations for item transfers.</param>
    public TradeSystem(World world, InventorySystem inventorySystem)
    {
        _world = world;
        _inventorySystem = inventorySystem;
    }

    /// <summary>Offers a fresh owned item at the explicit requested price; AI callers can opt into automatic repricing after success.</summary>
    /// <param name="seller">The living NPC offering stock.</param>
    /// <param name="item">The registered owned item.</param>
    /// <param name="goldPerUnit">The positive price per unit.</param>
    /// <returns>True if the sale price was assigned.</returns>
    public bool Offer(Entity seller, Entity item, int goldPerUnit)
    {
        if (!CanTrade(seller) || FoodRules.IsExpired(_world, item) || goldPerUnit <= 0 ||
            !_world.TryGetEntity(item.Id, out var registered) || registered != item ||
            !_world.Has<ItemComponent>(item) || !_inventorySystem.HasItem(seller, item)) return false;
        if (_world.TryGet<EquipmentComponent>(seller, out var equipment) && equipment is not null && equipment.Weapon == item)
            return false;
        var place = VillageRules.SalePlace(_world, seller, item);
        if (!place.IsEmpty)
        {
            if (!VillageRules.Near(_world, seller, place)) return false;
            _world.Add(item, new SaleLocationComponent(place));
        }
        // An explicit offer replaces any previous automatic pricing policy.
        _world.Remove<AutomaticMarketPriceComponent>(item);
        _world.Add(item, new TradePriceComponent(goldPerUnit));
        return true;
    }

    /// <summary>Retrieves an owned listing only at its sale location, preserving the original inventory and gold.</summary>
    /// <param name="owner">The living actor owning the inventory item.</param>
    /// <param name="item">The registered listed item.</param>
    /// <returns>True when sale markers were removed; false leaves the listing unchanged.</returns>
    public bool Withdraw(Entity owner, Entity item)
    {
        if (!CanTrade(owner) || !_world.TryGetEntity(item.Id, out var registered) || registered != item ||
            !_world.Has<ItemComponent>(item) || !_inventorySystem.HasItem(owner, item) || !_world.Has<TradePriceComponent>(item)) return false;
        if (_world.TryGet<SaleLocationComponent>(item, out var sale) && !VillageRules.Near(_world, owner, sale.PlaceId)) return false;
        if (_world.Has<VillageResidentComponent>(owner) && !_world.Has<SaleLocationComponent>(item)) return false;
        _world.Remove<TradePriceComponent>(item);
        _world.Remove<SaleLocationComponent>(item);
        _world.Remove<AutomaticMarketPriceComponent>(item);
        return true;
    }

    /// <summary>Transfers fresh goods and gold, then records rolling sales and diagnostic balances only after success.</summary>
    /// <param name="buyer">The living NPC paying gold.</param>
    /// <param name="seller">The living NPC receiving gold.</param>
    /// <param name="item">The registered item offered in the seller's inventory.</param>
    /// <param name="quantity">Positive stack quantity, or one for an individual item.</param>
    /// <returns>True if stock and payment were transferred; false changes neither.</returns>
    public bool Buy(Entity buyer, Entity seller, Entity item, int quantity = 1)
    {
        if (FoodRules.IsExpired(_world, item) || buyer == seller || quantity <= 0 || !CanTrade(buyer) || !CanTrade(seller) ||
            !_world.TryGetEntity(item.Id, out var registered) || registered != item ||
            !_world.Has<ItemComponent>(item) ||
            !_inventorySystem.HasItem(seller, item) || _inventorySystem.HasItem(buyer, item) ||
            !_world.TryGet<TradePriceComponent>(item, out var price) || price.GoldPerUnit <= 0 ||
            !_world.TryGet<GoldComponent>(buyer, out var buyerGold) || buyerGold.Amount < 0 ||
            !_world.TryGet<GoldComponent>(seller, out var sellerGold) || sellerGold.Amount < 0)
        {
            return false;
        }

        // Equipped tools must be unequipped through the established equipment workflow first.
        if (_world.TryGet<EquipmentComponent>(seller, out var equipment) &&
            equipment is not null && equipment.Weapon == item)
        {
            return false;
        }

        bool isStack = InventoryStacks.IsStack(_world, item);
        int available = InventoryStacks.Count(_world, item);
        if ((isStack && available < quantity) || (!isStack && quantity != 1)) return false;
        _world.TryGet<VillageResidentComponent>(buyer, out var resident);
        _world.TryGet<VillageResidentComponent>(seller, out var sellerResident);
        if (resident is not null || sellerResident is not null)
        {
            if (resident is null || sellerResident is null || sellerResident.Village != resident.Village ||
                !_world.TryGet<SaleLocationComponent>(item, out var sale) ||
                !VillageRules.Near(_world, buyer, sale.PlaceId)) return false;
        }

        // Calculate in long so an oversized price cannot overflow into an affordable purchase.
        long totalPrice = (long)quantity * price.GoldPerUnit;
        if (totalPrice > buyerGold.Amount || (long)sellerGold.Amount + totalPrice > int.MaxValue)
        {
            return false;
        }

        // All validation happens before mutation. A partial purchase creates its own grain item.
        bool partialStack = isStack && quantity < available;
        Entity purchased = partialStack ? InventoryStacks.Split(_world, item, quantity) : item;
        if (!_inventorySystem.AddItem(buyer, purchased))
        {
            if (partialStack)
            {
                _world.DestroyEntity(purchased.Id);
            }
            return false;
        }

        if (partialStack)
        {
            InventoryStacks.Set(_world, item, available - quantity);
        }
        else
        {
            _inventorySystem.RemoveItem(seller, item);
        }

        // A purchase does not automatically offer the buyer's new stock for resale.
        _world.Remove<TradePriceComponent>(purchased);
        _world.Remove<SaleLocationComponent>(purchased);
        _world.Remove<AutomaticMarketPriceComponent>(purchased);
        _world.Add(buyer, new GoldComponent(buyerGold.Amount - (int)totalPrice));
        _world.Add(seller, new GoldComponent(sellerGold.Amount + (int)totalPrice));
        // Failed purchases return above and never enter sales history.
        MarketSales.Record(_world, buyer, purchased, quantity);
        TransactionHistory.Purchase(_world, buyer, seller, purchased, quantity, price.GoldPerUnit, buyerGold.Amount, sellerGold.Amount);
        foreach (Entity trader in new[] { buyer, seller })
            if (_world.TryGet<MerchantVisitComponent>(trader, out var visit) && visit is not null &&
                _world.TryGet<VillageResidentComponent>(trader, out var context) && context is not null)
                visit.LastTradeAt = context.Village.CurrentMinute;
        return true;
    }

    /// <summary>Checks that a trading NPC belongs to this world, is alive, and has inventory.</summary>
    /// <param name="npc">The proposed buyer or seller.</param>
    /// <returns>True if the NPC can participate in a direct trade.</returns>
    private bool CanTrade(Entity npc)
    {
        return _world.TryGetEntity(npc.Id, out var registered) && registered == npc &&
            !_world.Has<DeadComponent>(npc) &&
            _world.TryGet<HealthComponent>(npc, out var health) && health.Current > 0 &&
            _world.TryGet<InventoryComponent>(npc, out var inventory) && inventory is not null;
    }
}
