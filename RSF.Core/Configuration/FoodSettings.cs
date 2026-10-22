namespace RSF.Core.Configuration;

/// <summary>Defines food lifetimes, smoker production, and travelers' journey supplies using existing AI actions.</summary>
public sealed record FoodSettings
{
    public int OrdinaryMealShelfLifeMinutes { get; init; } = 720;
    public int BreadShelfLifeMinutes { get; init; } = 10080;
    public int RawProduceShelfLifeMinutes { get; init; } = 7200;
    public int FreshFishShelfLifeMinutes { get; init; } = 1440;
    public int FreshMeatShelfLifeMinutes { get; init; } = 1440;
    /// <summary>Target preserved servings suitable for a long journey; older edible stock can still serve local buyers.</summary>
    public int PreservedFoodStockTarget { get; init; } = 16;
    /// <summary>Fresh recipe batches to leave available after starting one preservation batch.</summary>
    public int PreservationReserveBatches { get; init; } = 2;
    public int MerchantJourneyFoodTarget { get; init; } = 12;
    public int DepartureShoppingMinutes { get; init; } = 360;
    /// <summary>Journey stock must remain edible for at least this many game minutes.</summary>
    public int MinimumJourneyShelfLifeMinutes { get; init; } = 43200;
    public double DepartureShoppingPoints { get; init; } = 400;
    /// <summary>Visitors prefer fresh food during their stay while retaining preserved food as an emergency fallback.</summary>
    public double FreshFoodPreferencePoints { get; init; } = 100;

    /// <summary>Rejects invalid lifetimes, stock targets, travel limits, and nonfinite preference scores.</summary>
    public void Validate()
    {
        if (OrdinaryMealShelfLifeMinutes <= 0 || OrdinaryMealShelfLifeMinutes > 12 * RSF.Core.Common.GameDate.MinutesPerHour ||
            BreadShelfLifeMinutes <= 0 || RawProduceShelfLifeMinutes <= 0 || FreshFishShelfLifeMinutes <= 0 || FreshMeatShelfLifeMinutes <= 0 ||
            PreservedFoodStockTarget < 0 || PreservationReserveBatches < 0 || PreservationReserveBatches > 100 ||
            MerchantJourneyFoodTarget < 0 || DepartureShoppingMinutes <= 0 || MinimumJourneyShelfLifeMinutes <= 0 ||
            !double.IsFinite(DepartureShoppingPoints) || DepartureShoppingPoints < 0 ||
            !double.IsFinite(FreshFoodPreferencePoints) || FreshFoodPreferencePoints < 0)
            throw new ArgumentException("Food lifetimes and shopping durations must be positive, ordinary meals cannot exceed 720 minutes, targets and finite scores cannot be negative, and reserve batches must be within 0-100.");
    }
}
