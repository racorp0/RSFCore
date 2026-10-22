namespace RSF.Core.Components;

/// <summary>Configures elapsed sleep recovery, daily preferences, and urgency scores per NPC.</summary>
public sealed record SleepSettingsComponent : IComponent
{
    public int AwakeMinutesPerPoint { get; init; } = 20;
    public int SleepMinutesPerPoint { get; init; } = 5;
    public int PreferredFatigue { get; init; } = 40;
    public int UrgentFatigue { get; init; } = 90;
    public int WakeFatigue { get; init; } = 20;
    public int BedtimeMinute { get; init; } = 22 * 60;
    public int WakeTimeMinute { get; init; } = 6 * 60;
    public double SleepPoints { get; init; } = 350;
    public double SeverityWeight { get; init; } = 2;
}
