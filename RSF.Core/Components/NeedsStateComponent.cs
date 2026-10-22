namespace RSF.Core.Components;

/// <summary>Stores derived need urgency; health and hunger components remain the source of truth.</summary>
/// <param name="HealthPercent">Current health percentage.</param>
/// <param name="HungerPercent">Current hunger percentage, where higher means better fed.</param>
/// <param name="SeekHealing">Whether healing is needed until the recovery target is reached.</param>
/// <param name="SeekFood">Whether food is needed until the satisfaction target is reached.</param>
/// <param name="HealthCritical">Whether current health crosses the critical threshold.</param>
/// <param name="HungerUrgent">Whether current hunger crosses the urgent threshold.</param>
public readonly record struct NeedsStateComponent(
    double HealthPercent, double HungerPercent,
    bool SeekHealing, bool SeekFood, bool HealthCritical, bool HungerUrgent) : IComponent;
