using RSF.Core.Components;
using RSF.Core.Common;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares small candidate lists during one AI update, keeping original world order for equal-score decisions.</summary>
internal sealed class AIDecisionSnapshot
{
    // Build each list only if a due NPC needs it; this snapshot lasts for one AI update.
    private List<AISurvivalOffer>? _foodOffers;
    private List<AISurvivalOffer>? _healingOffers;
    public List<Entity> InventoryOwners { get; } = new();
    public List<Entity> OwnedEntities { get; } = new();
    public List<Entity> Waters { get; } = new();
    public List<Entity> ProcessingJobs { get; } = new();
    public List<Entity> ProcessingStations { get; } = new();
    public List<Entity> InnGuests { get; } = new();

    /// <summary>Collects indexed candidate IDs and preserves world order once when the first NPC needs a decision; later due NPCs reuse the lists.</summary>
    /// <param name="world">The world containing current stock, ownership, water, and jobs.</param>
    public AIDecisionSnapshot(World world)
    {
        HashSet<EntityId> relevant = new();
        Include<InventoryComponent>(world, relevant);
        Include<OwnershipComponent>(world, relevant);
        Include<RiverComponent>(world, relevant);
        Include<ProcessingJobComponent>(world, relevant);
        Include<ProcessingStationComponent>(world, relevant);
        Include<InnStayComponent>(world, relevant);
        if (relevant.Count == 0) return;
        // Keep world order, but only inspect components on IDs already found in the existing indexes.
        foreach (Entity entity in world.GetEntities())
        {
            if (!relevant.Contains(entity.Id)) continue;
            // Independent checks keep NPC-hosted jobs and occupied stations in every relevant list.
            if (world.Has<InventoryComponent>(entity)) InventoryOwners.Add(entity);
            if (world.Has<OwnershipComponent>(entity)) OwnedEntities.Add(entity);
            if (world.Has<RiverComponent>(entity)) Waters.Add(entity);
            if (world.Has<ProcessingJobComponent>(entity)) ProcessingJobs.Add(entity);
            if (world.Has<ProcessingStationComponent>(entity)) ProcessingStations.Add(entity);
            if (world.Has<InnStayComponent>(entity)) InnGuests.Add(entity);
        }
        // This object is discarded after the AI update, so no long-lived cache can hide world changes.
    }

    /// <summary>Shares usable sale items in original seller and inventory order for this AI update.</summary>
    /// <param name="world">The current world used by the existing survival eligibility rules.</param>
    /// <param name="healing">True for healing offers; false for food offers.</param>
    /// <returns>Seller groups of usable entries, including duplicates, without caching prices or buyer-specific decisions.</returns>
    public IReadOnlyList<AISurvivalOffer> GetSurvivalOffers(World world, bool healing)
    {
        List<AISurvivalOffer>? cached = healing ? _healingOffers : _foodOffers;
        if (cached is not null) return cached;

        List<AISurvivalOffer> offers = new();
        foreach (Entity seller in InventoryOwners)
        {
            if (!world.TryGet<InventoryComponent>(seller, out var stock) || stock is null) continue;
            List<Entity> items = new();
            foreach (Entity item in stock.Items)
            {
                // Commands execute after AI evaluation, so inventory and item effects stay stable during this update.
                // Item usability is buyer-independent; buyer funds, prices, equipment, and failed targets are checked later.
                if (world.TryGet<TradePriceComponent>(item, out var price) && price.GoldPerUnit > 0 &&
                    AISurvivalRules.CanUse(world, item, healing))
                    items.Add(item);
            }
            if (items.Count > 0) offers.Add(new AISurvivalOffer(seller, items));
        }
        if (healing) _healingOffers = offers;
        else _foodOffers = offers;
        return offers;
    }

    /// <summary>Collects relevant IDs from an existing component index without checking terrain components.</summary>
    /// <typeparam name="T">The candidate component to include.</typeparam>
    /// <param name="world">The current decision world.</param>
    /// <param name="relevant">The union of IDs needed by this update's candidate lists.</param>
    private static void Include<T>(World world, HashSet<EntityId> relevant) where T : IComponent
    {
        foreach (Entity entity in world.GetEntitiesWith<T>()) relevant.Add(entity.Id);
    }
}
