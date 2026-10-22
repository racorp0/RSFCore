using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Shares map locations and configurable village settings; systems perform all behavior.</summary>
public sealed class VillageComponent : IComponent
{
    /// <summary>Physical grid dimensions and tile scale, copied from map.json for debugging.</summary>
    public int Width { get; set; } = 32;
    public int Height { get; set; } = 20;
    public double MetresPerTile { get; set; } = 1;
    public EntityId MarketId { get; set; }
    public EntityId InnId { get; set; }
    public EntityId EntranceId { get; set; }
    public Dictionary<(int X, int Y), MapZone> Cells { get; } = new();
    public GameMinute CurrentMinute { get; set; }
    public int MinutesPerTravelTile { get; init; } = 1;
    public int InnBeds { get; init; } = 2;
    public int MerchantBread { get; init; } = 3;
    public int MerchantFishTarget { get; init; } = 3;
    public int RentPerDay { get; init; } = 5;
    public int MinMerchantDays { get; init; } = 5;
    public int MaxMerchantDays { get; init; } = 12;
    public int MaxMerchants { get; init; } = 1;
    public int MerchantIdleMinutes { get; init; } = 120;
    public int MaxVisitMinutes { get; init; } = 3 * GameDate.MinutesPerDay;
    public int MerchantGold { get; init; } = 200;
    public int MerchantBait { get; init; } = 30;
    public int MerchantSpice { get; init; } = 30;
    public int BaitPrice { get; init; } = 1;
    public int SpicePrice { get; init; } = 1;
    public GameMinute? NextMerchantAt { get; set; }
    public int MerchantsArrived { get; set; }
    /// <summary>Preserves visitor and cargo summaries after departure rather than losing their history.</summary>
    public List<string> DepartedMerchants { get; } = new();
}
