using RSF.Core.Common;
using RSF.Core.Components;
namespace RSF.Core.Configuration;

/// <summary>Stores survival rates loaded at startup; HungerSystem still performs the actual updates.</summary>
public sealed class SurvivalSettings
{
    public int HungerTickMinutes { get; init; } = 20;
    public int HungerLossPerTick { get; init; } = 1;
    public int StarvationDamage { get; init; } = 5;
    public int AppleHunger { get; init; } = 5;
    public int MinimumSoupHunger { get; init; } = 7;
    public int MaximumSoupHunger { get; init; } = 11;
    public int BreadHunger { get; init; } = 30;
    public NeedsSettingsComponent Thresholds { get; init; } = new();
    public SleepSettingsComponent Sleep { get; init; } = new();
}

/// <summary>Groups the existing AI score and work settings for one configuration file.</summary>
public sealed class DecisionSettings
{
    public AISettingsComponent Decision { get; init; } = new();
    public AIWorkSettingsComponent Work { get; init; } = new();
}

/// <summary>Describes initial NPC data; blueprints still create entities and attach their components.</summary>
public sealed class NpcSettings
{
    public string Name { get; init; } = "NPC";
    public float X { get; init; }
    public float Y { get; init; }
    public Job Job { get; init; }
    public LifeStage LifeStage { get; init; } = LifeStage.Adult;
    public IReadOnlyDictionary<string, int> ProcessingSkills { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Tools { get; init; } = Array.Empty<string>();
    public int Gold { get; init; } = 100;
    public int Hunger { get; init; } = 50;
    public int MaximumHunger { get; init; } = 100;
    public int FishingSkill { get; init; } = 1;
    public int CookingSkill { get; init; } = 1;
    public int Bait { get; init; } = 5;
    public int Bread { get; init; } = 5;
    public int FarmPlots { get; init; } = 3;
    public int Spices { get; init; } = 10;
    public int MinimumVitality { get; init; } = 1;
    public int MaximumVitality { get; init; } = 50;
    public int MinimumStrength { get; init; } = 1;
    public int MaximumStrength { get; init; } = 20;
    public int BaseHealth { get; init; } = 50;
    public int HealthPerVitality { get; init; } = 10;
    /// <summary>Optional complete personal overrides; omitted values use the shared settings.</summary>
    public NeedsSettingsComponent? Needs { get; init; }
    public AISettingsComponent? AI { get; init; }
    public AIWorkSettingsComponent? Work { get; init; }
    public SleepSettingsComponent? Sleep { get; init; }
    public CookingSettingsComponent? Cooking { get; init; }
    public string? Schedule { get; init; }
}

/// <summary>Defines named daily templates without storing per-NPC schedule progress in JSON.</summary>
public sealed class ScheduleSettings
{
    public string Name { get; init; } = "Schedule";
    public ScheduleEntry[] Entries { get; init; } = Array.Empty<ScheduleEntry>();
}

/// <summary>Separates shared starting defaults for unnamed visitors from the village's resident spawn list.</summary>
public sealed class NpcFileSettings
{
    public NpcSettings Defaults { get; init; } = new();
    public List<NpcSettings> Residents { get; init; } = new();
}
