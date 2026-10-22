using RSF.Core.Common;
using RSF.Core.Components;
namespace RSF.Core.Configuration;

/// <summary>Contains village economy defaults without runtime entity IDs, map cells, or visitor history.</summary>
public sealed class EconomySettings
{
    /// <summary>Starting local market data; this phase does not apply multipliers to trade offers.</summary>
    public MarketSettings Market { get; init; } = new();
    /// <summary>Optional diagnostic histories; actual transfers remain in TradeSystem and InnSystem.</summary>
    public TransactionHistorySettings TransactionHistory { get; init; } = new();
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

    /// <summary>Creates fresh village data so separate simulations never share mutable state.</summary>
    /// <returns>A new village component containing only the configured starting values.</returns>
    public VillageComponent CreateVillage() => new()
    {
        MinutesPerTravelTile = MinutesPerTravelTile,
        InnBeds = InnBeds,
        MerchantBread = MerchantBread,
        MerchantFishTarget = MerchantFishTarget,
        RentPerDay = RentPerDay,
        MinMerchantDays = MinMerchantDays,
        MaxMerchantDays = MaxMerchantDays,
        MaxMerchants = MaxMerchants,
        MerchantIdleMinutes = MerchantIdleMinutes,
        MaxVisitMinutes = MaxVisitMinutes,
        MerchantGold = MerchantGold,
        MerchantBait = MerchantBait,
        MerchantSpice = MerchantSpice,
        BaitPrice = BaitPrice,
        SpicePrice = SpicePrice,
    };
}
