using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Shares physical interaction checks without changing legacy worlds that have no village.</summary>
public static class VillageRules
{
    /// <summary>Checks grid interaction distance between a registered actor and a place.</summary>
    /// <param name="world">The actor's world.</param>
    /// <param name="actor">The acting NPC.</param>
    /// <param name="placeId">The target place.</param>
    /// <returns>True within one Manhattan tile, or in a legacy world without village rules.</returns>
    public static bool Near(World world, Entity actor, EntityId placeId)
    {
        if (!world.Has<VillageResidentComponent>(actor)) return true;
        return world.TryGetEntity(placeId, out var place) && place is not null &&
            world.TryGet<PositionComponent>(actor, out var a) && world.TryGet<PositionComponent>(place, out var b) &&
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= 1;
    }

    /// <summary>Chooses the market for supplies, or the seller's inn counter for food.</summary>
    /// <param name="world">The trading world.</param>
    /// <param name="seller">The owner publishing an offer.</param>
    /// <param name="item">The offered item.</param>
    /// <returns>The physical trading place, or an empty ID for legacy trading.</returns>
    public static EntityId SalePlace(World world, Entity seller, Entity item)
    {
        if (!world.TryGet<VillageResidentComponent>(seller, out var resident) || resident is null) return EntityId.Empty;
        if (world.Has<ConsumableItemComponent>(item) && world.TryGetEntity(resident.Village.InnId, out var inn) && inn is not null &&
            world.TryGet<OwnershipComponent>(inn, out var owner) && owner is not null && owner.OwnerId == seller.Id)
            return inn.Id;
        return resident.Village.MarketId;
    }

    /// <summary>Checks whether an item can be physically consumed from inventory or its posted location.</summary>
    /// <param name="world">The world containing the item.</param>
    /// <param name="actor">The item owner.</param>
    /// <param name="item">The item being consumed.</param>
    /// <returns>True when the item is carried or its counter is within reach.</returns>
    public static bool CanConsume(World world, Entity actor, Entity item)
    {
        return !world.TryGet<SaleLocationComponent>(item, out var sale) || Near(world, actor, sale.PlaceId);
    }
}
