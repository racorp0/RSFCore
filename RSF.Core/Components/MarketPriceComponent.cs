using RSF.Core.Common;
using RSF.Core.Configuration;

namespace RSF.Core.Components;

/// <summary>Stores one market's local multipliers and coverage policy, updated by MarketPriceSystem.</summary>
public sealed class MarketPriceComponent : IComponent
{
    public int UpdateIntervalMinutes { get; }
    public int TrackingIntervalMinutes { get; }
    public int SalesWindowMinutes { get; }
    public int TargetStockCoverageMinutes { get; }
    public double CoverageTolerancePercent { get; }
    public double MinimumPriceMultiplier { get; }
    public double MaximumPriceMultiplier { get; }
    public double MaximumChangePercentPerUpdate { get; }
    /// <summary>Independent local values; commodities absent from this dictionary have a neutral multiplier of one.</summary>
    public Dictionary<string, double> PriceMultipliers { get; } = new(StringComparer.Ordinal);
    /// <summary>The last actual price adjustment; null until a market system performs one.</summary>
    public GameMinute? LastPriceUpdateAt { get; set; }
    /// <summary>Last completed price check, even if the stock was stable and no multiplier changed.</summary>
    public GameMinute? LastEvaluatedAt { get; set; }

    /// <summary>Validates settings and copies initial values without sharing their dictionary.</summary>
    /// <param name="settings">The market's configured limits and starting multipliers.</param>
    public MarketPriceComponent(MarketSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        UpdateIntervalMinutes = settings.UpdateIntervalMinutes;
        TrackingIntervalMinutes = settings.TrackingIntervalMinutes;
        SalesWindowMinutes = settings.SalesWindowMinutes;
        TargetStockCoverageMinutes = settings.TargetStockCoverageMinutes;
        CoverageTolerancePercent = settings.CoverageTolerancePercent;
        MinimumPriceMultiplier = settings.MinimumPriceMultiplier;
        MaximumPriceMultiplier = settings.MaximumPriceMultiplier;
        MaximumChangePercentPerUpdate = settings.MaximumChangePercentPerUpdate;
        // Only policy values are copied. Existing item settings remain the source of base prices and price units.
        foreach (var pair in settings.InitialPriceMultipliers) PriceMultipliers.Add(pair.Key, pair.Value);
    }
}
