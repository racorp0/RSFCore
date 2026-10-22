using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Identifies one buyer's current request so repeated observations cannot multiply demand.</summary>
/// <param name="BuyerId">The existing NPC entity ID.</param>
/// <param name="Commodity">An item reference or a general substitute category.</param>
public readonly record struct MarketRequestKey(EntityId BuyerId, string Commodity);

/// <summary>Stores a quantity with its unit; grams must never be added to item counts.</summary>
/// <param name="Quantity">The positive stock or requested amount.</param>
/// <param name="Unit">Count for individual goods, or grams for fish and meat.</param>
public readonly record struct MarketQuantity(long Quantity, string Unit);

/// <summary>Stores the quantities from one completed trade so they can expire together from the rolling window.</summary>
/// <param name="At">The game's trade minute.</param>
/// <param name="Quantities">Specific commodity and category views of this purchase.</param>
public sealed record MarketSaleRecord(GameMinute At, IReadOnlyDictionary<string, MarketQuantity> Quantities);

/// <summary>Stores a market's latest stock and affordable outstanding requests; it never changes trade prices.</summary>
public sealed class MarketActivityComponent : IComponent
{
    public Dictionary<string, MarketQuantity> Supply { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, MarketQuantity> Demand { get; } = new(StringComparer.Ordinal);
    public Dictionary<MarketRequestKey, MarketQuantity> OutstandingRequests { get; } = new();
    /// <summary>Running totals of purchases still inside the configured sales window.</summary>
    public Dictionary<string, MarketQuantity> RollingSales { get; } = new(StringComparer.Ordinal);
    /// <summary>Observed commodity-to-category relationships, including meals from individual NPC recipe overrides.</summary>
    public Dictionary<string, string> PriceSignals { get; } = new(StringComparer.Ordinal);
    /// <summary>Oldest trades come out first; expired entries are removed rather than keeping lifetime history.</summary>
    public Queue<MarketSaleRecord> SalesHistory { get; } = new();
    /// <summary>First observation or trade minute, used to avoid assuming a full window during startup.</summary>
    public GameMinute? HistoryStartedAt { get; set; }
    /// <summary>Null until the tracking system has observed this market.</summary>
    public GameMinute? LastTrackedAt { get; set; }
}
