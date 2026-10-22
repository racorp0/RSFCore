using RSF.Core.Components;

namespace RSF.Core.Configuration;

/// <summary>Configures local market price multipliers without replacing existing commodity base prices.</summary>
public sealed record MarketSettings
{
    public int UpdateIntervalMinutes { get; init; } = 360;
    /// <summary>Snapshot frequency, separate from the price adjustment interval.</summary>
    public int TrackingIntervalMinutes { get; init; } = 15;
    /// <summary>Successful purchases older than this rolling window no longer influence prices.</summary>
    public int SalesWindowMinutes { get; init; } = 4320;
    /// <summary>Desired offered stock expressed as minutes of recent purchases.</summary>
    public int TargetStockCoverageMinutes { get; init; } = 2880;
    /// <summary>Stock within this percentage of its target leaves the multiplier unchanged.</summary>
    public double CoverageTolerancePercent { get; init; } = 25;
    public double MinimumPriceMultiplier { get; init; } = 0.5;
    public double MaximumPriceMultiplier { get; init; } = 2;
    public double MaximumChangePercentPerUpdate { get; init; } = 10;
    /// <summary>Starting multipliers keyed by existing commodities or supported categories; omitted references start at one.</summary>
    public IReadOnlyDictionary<string, double> InitialPriceMultipliers { get; init; } = new Dictionary<string, double>();

    /// <summary>Rejects invalid intervals, bounds, adjustment limits, and starting multipliers before creating market state.</summary>
    public void Validate()
    {
        // Bounds include one so an unlisted commodity always has a valid neutral starting multiplier.
        if (UpdateIntervalMinutes <= 0 || TrackingIntervalMinutes <= 0 || SalesWindowMinutes <= 0 ||
            TargetStockCoverageMinutes <= 0 || !double.IsFinite(CoverageTolerancePercent) ||
            CoverageTolerancePercent < 0 || CoverageTolerancePercent >= 100 || !double.IsFinite(MinimumPriceMultiplier) ||
            !double.IsFinite(MaximumPriceMultiplier) || MinimumPriceMultiplier <= 0 ||
            MinimumPriceMultiplier > 1 || MaximumPriceMultiplier < 1 ||
            !double.IsFinite(MaximumChangePercentPerUpdate) || MaximumChangePercentPerUpdate <= 0 ||
            MaximumChangePercentPerUpdate > 100 || InitialPriceMultipliers is null)
            throw new ArgumentException("Market intervals and coverage must be positive, coverage tolerance must be within [0, 100), multiplier bounds must include one, and change percent must be within (0, 100].");

        foreach (var pair in InitialPriceMultipliers)
        {
            string[] reference = pair.Key.Split(':', 2);
            bool category = pair.Key is "category:food" or "category:bait" or "category:whole_fish" or "category:healing";
            if (reference.Length != 2 || (!category && reference[0] is not ("item" or "seed" or "bait" or "fish")) ||
                string.IsNullOrWhiteSpace(reference[1]) || !double.IsFinite(pair.Value) ||
                pair.Value < MinimumPriceMultiplier || pair.Value > MaximumPriceMultiplier)
                throw new ArgumentException($"Invalid market commodity reference or starting multiplier: {pair.Key}.");
        }
    }

    /// <summary>Creates independent local state so villages never share a mutable price dictionary.</summary>
    /// <returns>A new market component using these starting settings.</returns>
    public MarketPriceComponent CreateMarket() => new(this);
}
