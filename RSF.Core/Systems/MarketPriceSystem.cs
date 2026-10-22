using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Adjusts local multipliers from rolling purchases, stock coverage, and unmet affordable requests.</summary>
public sealed class MarketPriceSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly GameSettings? _settings;

    /// <summary>Reuses existing market components and the shared simulation clock.</summary>
    /// <param name="world">The world containing market data and sale offers.</param>
    /// <param name="clock">The existing game-minute clock.</param>
    public MarketPriceSystem(World world, WorldClock clock)
    {
        _world = world;
        _clock = clock;
        _settings = GameSettings.Find(world);
    }

    /// <summary>Checks due markets after tracking and refreshes only NPC-generated offers when their market changes.</summary>
    /// <param name="deltaTime">Elapsed time; the shared game clock controls update intervals.</param>
    public void Update(float deltaTime)
    {
        HashSet<EntityId> changed = new();
        foreach (Entity market in _world.GetEntitiesWith<MarketPriceComponent>())
        {
            _world.TryGet<MarketPriceComponent>(market, out var policy);
            if (policy is null || !_world.TryGet<MarketActivityComponent>(market, out var activity) || activity?.HistoryStartedAt is null) continue;
            long previous = (policy.LastEvaluatedAt ?? activity.HistoryStartedAt.Value).Value;
            if (_clock.CurrentMinute.Value - previous < policy.UpdateIntervalMinutes) continue;
            MarketSales.Prune(activity, _clock.CurrentMinute, policy.SalesWindowMinutes);
            double observedMinutes = Math.Max(1, Math.Min(policy.SalesWindowMinutes, _clock.CurrentMinute.Value - activity.HistoryStartedAt.Value.Value));
            var signals = activity.Supply.Keys.Concat(activity.Demand.Keys).Concat(activity.RollingSales.Keys)
                .Concat(policy.PriceMultipliers.Keys).Select(key => activity.PriceSignals.GetValueOrDefault(key) ?? MarketPricing.Signal(key, _settings)).Distinct().ToList();
            foreach (string signal in signals)
            {
                activity.Supply.TryGetValue(signal, out var stock);
                activity.Demand.TryGetValue(signal, out var requests);
                activity.RollingSales.TryGetValue(signal, out var purchases);
                // Comparing grams to item counts would create false shortages. Each signal must keep one unit.
                if ((stock.Quantity > 0 && requests.Quantity > 0 && stock.Unit != requests.Unit) ||
                    (stock.Quantity > 0 && purchases.Quantity > 0 && stock.Unit != purchases.Unit) ||
                    (requests.Quantity > 0 && purchases.Quantity > 0 && requests.Unit != purchases.Unit)) continue;
                double pressure = Pressure(stock.Quantity, requests.Quantity, purchases.Quantity, observedMinutes, policy);
                double current = policy.PriceMultipliers.GetValueOrDefault(signal, 1);
                double updated = Math.Clamp(current * (1 + pressure * policy.MaximumChangePercentPerUpdate / 100),
                    policy.MinimumPriceMultiplier, policy.MaximumPriceMultiplier);
                if (updated == current) continue;
                policy.PriceMultipliers[signal] = updated;
                changed.Add(market.Id);
                policy.LastPriceUpdateAt = _clock.CurrentMinute;
            }
            policy.LastEvaluatedAt = _clock.CurrentMinute;
        }
        if (changed.Count > 0) RefreshOffers(changed);
    }

    /// <summary>Calculates gradual price pressure while leaving healthy stock coverage inside its stable band.</summary>
    /// <param name="stock">Currently offered quantity.</param>
    /// <param name="requests">Current affordable outstanding requests, including those existing stock can fulfill.</param>
    /// <param name="purchases">Completed purchases inside the rolling window.</param>
    /// <param name="observedMinutes">Actual observed history, capped at the configured window.</param>
    /// <param name="policy">Coverage target and stable range.</param>
    /// <returns>A pressure between minus one and one; zero preserves the current multiplier.</returns>
    private static double Pressure(long stock, long requests, long purchases, double observedMinutes, MarketPriceComponent policy)
    {
        // Outstanding requests are not all shortages: available stock can satisfy part or all of them.
        double unmet = Math.Max(0, (double)requests - stock);
        double pressure = 0;
        if (purchases > 0)
        {
            double target = purchases / observedMinutes * policy.TargetStockCoverageMinutes;
            double tolerance = policy.CoverageTolerancePercent / 100;
            if (stock < target * (1 - tolerance) || stock > target * (1 + tolerance))
                pressure = (target - stock) / Math.Max(1, Math.Max(target, stock));
        }
        // With no sales evidence, excess opening stock alone must not cause an automatic price collapse.
        if (unmet > 0) pressure = Math.Max(pressure, unmet / Math.Max(1, requests));
        return Math.Clamp(pressure, -1, 1);
    }

    /// <summary>Refreshes existing automatic listings from original base prices, respecting ownership and manual overrides.</summary>
    /// <param name="markets">The markets whose multipliers changed during this check.</param>
    private void RefreshOffers(HashSet<EntityId> markets)
    {
        // Snapshot the small offer index because updating/removing its components can change that index.
        foreach (Entity item in _world.GetEntitiesWith<AutomaticMarketPriceComponent>().ToList())
        {
            _world.TryGet<AutomaticMarketPriceComponent>(item, out var automatic);
            if (!markets.Contains(automatic.MarketId)) continue;
            if (!_world.TryGet<TradePriceComponent>(item, out var oldPrice) || oldPrice.GoldPerUnit != automatic.LastQuotedPrice)
            {
                _world.Remove<AutomaticMarketPriceComponent>(item);
                continue;
            }
            if (!_world.TryGetEntity(automatic.SellerId, out var seller) || seller is null || _world.Has<DeadComponent>(seller) ||
                !_world.TryGet<HealthComponent>(seller, out var health) || health.Current <= 0 ||
                !_world.TryGet<InventoryComponent>(seller, out var bag) || bag is null || !bag.Items.Contains(item) ||
                !_world.TryGet<VillageResidentComponent>(seller, out var resident) || resident?.Village.MarketId != automatic.MarketId ||
                !_world.TryGet<SaleLocationComponent>(item, out var sale) || sale.PlaceId != VillageRules.SalePlace(_world, seller, item) ||
                (_world.TryGet<EquipmentComponent>(seller, out var equipment) && equipment?.Weapon == item)) continue;
            int price = MarketPricing.QuoteItem(_world, seller, item, automatic.BaseGoldPerUnit);
            if (price <= 0) continue;
            _world.Add(item, new TradePriceComponent(price));
            _world.Add(item, automatic with { LastQuotedPrice = price });
        }
    }
}
