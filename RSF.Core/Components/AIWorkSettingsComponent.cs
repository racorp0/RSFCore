namespace RSF.Core.Components;

/// <summary>Configures job activity scores, preparation thresholds, and trading targets per NPC.</summary>
public sealed record AIWorkSettingsComponent : IComponent
{
    public double BeforeWorkHungerPercent { get; init; } = 60;
    public double BreakHungerPercent { get; init; } = 60;
    public double PreparationEatingPoints { get; init; } = 300;
    public double BreakEatingPoints { get; init; } = 200;
    public double ScheduleBonus { get; init; } = 50;
    public double PlowPoints { get; init; } = 100;
    /// <summary>Extra planting score while a crop is below its configured preferred plot count.</summary>
    public double CropPreferenceBonusPoints { get; init; } = 150;
    public double PlantPoints { get; init; } = 150;
    public double WaterPoints { get; init; } = 300;
    public double HarvestPoints { get; init; } = 400;
    /// <summary>Extra priority for seed production so processing does not consume all future planting material.</summary>
    public double SeedProductionBonusPoints { get; init; } = 100;
    public double ProcessPoints { get; init; } = 200;
    public double FishingPoints { get; init; } = 100;
    /// <summary>Sale offers outrank starting another fishing attempt so ingredients can reach the cook.</summary>
    public double OfferPoints { get; init; } = 180;
    public double IngredientBuyingPoints { get; init; } = 100;
    public int BreadPrice { get; init; } = 5;
    /// <summary>Fallback gold rate per 500 grams for manual worlds without loaded species settings.</summary>
    public int FishPrice { get; init; } = 10;
    public int BaitStockTarget { get; init; } = 5;
    /// <summary>Desired usable meat grams across prepared meat and whole fish; replaces portion-count purchasing.</summary>
    public int FishMeatStockTargetGrams { get; init; } = 600;

    /// <summary>Rejects invalid work scores, hunger thresholds, sale prices, and gram-based meat stock targets.</summary>
    public void Validate()
    {
        double[] scores = { PreparationEatingPoints, BreakEatingPoints, ScheduleBonus, PlowPoints,
            PlantPoints, CropPreferenceBonusPoints, WaterPoints, HarvestPoints, ProcessPoints, SeedProductionBonusPoints, FishingPoints, OfferPoints, IngredientBuyingPoints };
        foreach (double score in scores)
        {
            if (!double.IsFinite(score) || score < 0) throw new ArgumentException("Work scores must be finite and non-negative.");
        }
        if (!double.IsFinite(BeforeWorkHungerPercent) || BeforeWorkHungerPercent < 0 || BeforeWorkHungerPercent > 100 ||
            !double.IsFinite(BreakHungerPercent) || BreakHungerPercent < 0 || BreakHungerPercent > 100 ||
            BreadPrice <= 0 || FishPrice <= 0 || FishMeatStockTargetGrams < 0 || BaitStockTarget < 0)
            throw new ArgumentException("Invalid work hunger thresholds, sale prices, or stock targets.");
    }
}
