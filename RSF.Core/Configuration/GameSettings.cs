using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Simulation;
namespace RSF.Core.Configuration;

/// <summary>Holds one simulation's loaded settings in existing component storage; systems retain their responsibilities.</summary>
public sealed class GameSettings : IComponent
{
    /// <summary>Validated item metadata indexed once from harvest and processing outputs; runtime stock stays on entities.</summary>
    public Dictionary<string, HarvestOutputDefinition> Products { get; } = new();
    /// <summary>Optional configured map; null preserves manually assembled legacy worlds.</summary>
    public MapSettings? Map { get; set; }
    public FoodSettings Food { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
    public FarmingSettingsComponent Farming { get; set; } = new();
    public RiverComponent Fishing { get; set; } = new(Array.Empty<RiverFishDefinition>());
    public CookingSettingsComponent Cooking { get; set; } = new();
    public SurvivalSettings Needs { get; set; } = new();
    public DecisionSettings AI { get; set; } = new();
    public EconomySettings Economy { get; set; } = new();
    public Dictionary<string, ScheduleTemplate> Schedules { get; } = new();
    public NpcSettings NpcDefaults { get; set; } = new();
    public List<NpcSettings> Npcs { get; set; } = new();

    /// <summary>Finds settings registered before setup; legacy manual worlds can continue without configuration files.</summary>
    /// <param name="world">The simulation world.</param>
    /// <returns>The loaded configuration, or null for a legacy world.</returns>
    public static GameSettings? Find(World world)
    {
        foreach (var entity in world.GetEntities())
            if (world.TryGet<GameSettings>(entity, out var settings)) return settings;
        return null;
    }

    /// <summary>Selects a stable shared template after a job or life stage changes.</summary>
    /// <param name="job">The current job.</param>
    /// <param name="stage">The current life stage.</param>
    /// <returns>The configured template for that situation.</returns>
    public ScheduleTemplate ScheduleFor(Job job, LifeStage stage) => Schedules[stage == LifeStage.Adult
        ? (job == Job.None ? "Unemployed" : job.ToString()) : stage.ToString()];
}
