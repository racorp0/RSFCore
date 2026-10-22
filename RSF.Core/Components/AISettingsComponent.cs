using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Opts an NPC into AI and configures its scoring, stability, and retry timing.</summary>
public sealed record AISettingsComponent : IComponent
{
    public double HealingPoints { get; init; } = 1000;
    public double EatingPoints { get; init; } = 500;
    /// <summary>Preparation is a fallback below a ready serving, while sharing its survival urgency.</summary>
    public double FoodPreparationPenaltyPoints { get; init; } = 25;
    /// <summary>Discourages slow food preparation when a quicker feasible recipe can address hunger.</summary>
    public double FoodPreparationTimeWeight { get; init; } = 0.1;
    /// <summary>Well-fed actors preserve a bounded surplus before offering all fresh inputs for sale.</summary>
    public double FoodPreservationPoints { get; init; } = 350;
    /// <summary>Extra points per missing percentage point below the recovery target.</summary>
    public double HealthSeverityWeight { get; init; } = 2;
    /// <summary>Extra points per missing percentage point below the satisfaction target.</summary>
    public double HungerSeverityWeight { get; init; } = 2;
    public double ContinuationBonus { get; init; } = 25;
    public double SwitchMargin { get; init; } = 20;
    /// <summary>Penalty per gold coin when ranking a purchase against other available actions.</summary>
    public double PurchaseCostWeight { get; init; } = 1;
    public GameDuration DecisionInterval { get; init; } = GameDuration.FromMinutes(5);
    public GameDuration ActionInterval { get; init; } = GameDuration.FromMinutes(1);
    public GameDuration RetryDelay { get; init; } = GameDuration.FromMinutes(15);

    /// <summary>Rejects negative scores, non-finite weights, and non-positive update intervals.</summary>
    public void Validate()
    {
        if (!IsValidPoint(HealingPoints) || !IsValidPoint(EatingPoints) ||
            !IsValidPoint(FoodPreparationTimeWeight) || !IsValidPoint(FoodPreservationPoints) || !IsValidPoint(FoodPreparationPenaltyPoints) || !IsValidPoint(HealthSeverityWeight) || !IsValidPoint(HungerSeverityWeight) ||
            !IsValidPoint(ContinuationBonus) || !IsValidPoint(SwitchMargin) || !IsValidPoint(PurchaseCostWeight))
        {
            throw new ArgumentException("AI score settings must be finite and non-negative.");
        }
        if (!DecisionInterval.IsPositive || !ActionInterval.IsPositive || !RetryDelay.IsPositive)
        {
            throw new ArgumentException("AI intervals must be positive game durations.");
        }
    }

    /// <summary>Checks one score without allocating a collection during NPC updates.</summary>
    /// <param name="value">The configured priority or weight.</param>
    /// <returns>True if the value is finite and non-negative.</returns>
    private static bool IsValidPoint(double value)
    {
        return double.IsFinite(value) && value >= 0;
    }
}
