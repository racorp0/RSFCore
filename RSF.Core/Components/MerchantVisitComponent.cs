using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Tracks one visitor's useful trading time and eventual departure.</summary>
public sealed class MerchantVisitComponent : IComponent
{
    public GameMinute ArrivedAt { get; init; }
    public GameMinute LastTradeAt { get; set; }
    public bool Departing { get; set; }
    /// <summary>Starts a final shopping stage before the existing walk to the entrance.</summary>
    public bool PreparingDeparture { get; set; }
    /// <summary>Bounds waiting for journey food when the market has no affordable stock.</summary>
    public GameMinute? DepartureShoppingStartedAt { get; set; }
    public int FishTarget { get; init; } = 3;
}
