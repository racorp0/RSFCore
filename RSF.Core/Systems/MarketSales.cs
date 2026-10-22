using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Records successful local trades and expires old purchases without keeping unlimited lifetime history.</summary>
public static class MarketSales
{
    /// <summary>Records transferred units and complete food servings, reducing fulfilled requests with separate commodity and category views.</summary>
    /// <param name="world">The trading world.</param>
    /// <param name="buyer">The NPC that successfully paid.</param>
    /// <param name="item">The purchased item, still registered after transfer.</param>
    /// <param name="quantity">The number of trade units actually purchased.</param>
    public static void Record(World world, Entity buyer, Entity item, int quantity)
    {
        if (!world.TryGet<VillageResidentComponent>(buyer, out var resident) || resident is null ||
            !world.TryGetEntity(resident.Village.MarketId, out var market) || market is null ||
            !world.TryGet<MarketActivityComponent>(market, out var activity) || activity is null ||
            !world.TryGet<MarketPriceComponent>(market, out var policy) || policy is null) return;
        GameMinute now = resident.Village.CurrentMinute;
        activity.HistoryStartedAt ??= now;
        Prune(activity, now, policy.SalesWindowMinutes);
        Dictionary<string, MarketQuantity> quantities = new(StringComparer.Ordinal);
        var settings = GameSettings.Find(world);
        var identity = MarketPricing.Identify(world, item, settings);
        if (identity.Key is not null)
        {
            activity.PriceSignals[identity.Key] = MarketPricing.ItemSignal(world, item, identity.Key, settings);
            quantities[identity.Key] = new MarketQuantity(world.Has<FishComponent>(item) ? identity.Quantity : quantity, identity.Unit);
        }
        if (AISurvivalRules.CanUse(world, item, false)) quantities["category:food"] = new(quantity / RawFoodRules.Units(world, item), "count");
        if (AISurvivalRules.CanUse(world, item, true)) quantities["category:healing"] = new(quantity / RawFoodRules.Units(world, item), "count");
        if (world.TryGet<BaitComponent>(item, out var bait) && BaitRules.Find(world, bait.BaitId) is not null)
            quantities["category:bait"] = new(quantity, "count");
        if (world.Has<FishComponent>(item)) quantities["category:whole_fish"] = new(quantity, "count");
        if (quantities.Count == 0) return;
        foreach (var pair in quantities.Where(pair => pair.Value.Quantity <= 0).ToList()) quantities.Remove(pair.Key);
        foreach (var pair in quantities)
        {
            // A completed purchase must not remain an outstanding request until the next snapshot.
            var requestKey = new MarketRequestKey(buyer.Id, pair.Key);
            if (activity.OutstandingRequests.TryGetValue(requestKey, out var requested) && requested.Unit == pair.Value.Unit)
            {
                long fulfilled = Math.Min(requested.Quantity, pair.Value.Quantity);
                if (fulfilled == requested.Quantity) activity.OutstandingRequests.Remove(requestKey);
                else activity.OutstandingRequests[requestKey] = requested with { Quantity = requested.Quantity - fulfilled };
                if (activity.Demand.TryGetValue(pair.Key, out var demand))
                {
                    long remaining = demand.Quantity - fulfilled;
                    if (remaining <= 0) activity.Demand.Remove(pair.Key);
                    else activity.Demand[pair.Key] = demand with { Quantity = remaining };
                }
            }
            activity.RollingSales.TryGetValue(pair.Key, out var previous);
            activity.RollingSales[pair.Key] = pair.Value with { Quantity = previous.Quantity + pair.Value.Quantity };
        }
        activity.SalesHistory.Enqueue(new MarketSaleRecord(now, quantities));
    }

    /// <summary>Subtracts each expired trade once, keeping the queue and its running totals in agreement.</summary>
    /// <param name="activity">One market's history data.</param>
    /// <param name="now">Current game time.</param>
    /// <param name="windowMinutes">The positive rolling window length.</param>
    public static void Prune(MarketActivityComponent activity, GameMinute now, int windowMinutes)
    {
        long cutoff = now.Value - windowMinutes;
        while (activity.SalesHistory.TryPeek(out var sale) && sale.At.Value <= cutoff)
        {
            activity.SalesHistory.Dequeue();
            foreach (var pair in sale.Quantities)
            {
                var current = activity.RollingSales[pair.Key];
                long remaining = current.Quantity - pair.Value.Quantity;
                if (remaining <= 0) activity.RollingSales.Remove(pair.Key);
                else activity.RollingSales[pair.Key] = current with { Quantity = remaining };
            }
        }
    }
}
