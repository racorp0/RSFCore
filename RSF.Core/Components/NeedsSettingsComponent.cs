namespace RSF.Core.Components;

/// <summary>Configures an NPC's need thresholds as percentages, without changing survival values.</summary>
public sealed record NeedsSettingsComponent : IComponent
{
    public double CriticalHealthPercent { get; init; } = 25;
    public double HealthyPercent { get; init; } = 60;
    public double UrgentHungerPercent { get; init; } = 30;
    public double SatisfiedHungerPercent { get; init; } = 60;

    /// <summary>Rejects invalid percentages and targets that would cause constant need switching.</summary>
    public void Validate()
    {
        if (!double.IsFinite(CriticalHealthPercent) || !double.IsFinite(HealthyPercent) ||
            !double.IsFinite(UrgentHungerPercent) || !double.IsFinite(SatisfiedHungerPercent) ||
            CriticalHealthPercent < 0 || HealthyPercent > 100 || HealthyPercent <= CriticalHealthPercent ||
            UrgentHungerPercent < 0 || SatisfiedHungerPercent > 100 || SatisfiedHungerPercent <= UrgentHungerPercent)
        {
            throw new ArgumentException("Need targets must be within 0-100 and above their urgency thresholds.");
        }
    }
}
