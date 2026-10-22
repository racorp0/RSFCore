using RSF.Core.Common;
using RSF.Core.Entities;
namespace RSF.Core.Components;

/// <summary>Marks world machinery that can host one processing job.</summary>
/// <param name="StationType">The JSON machinery type, separate from an inventory tool ID.</param>
public readonly record struct ProcessingStationComponent(string StationType) : IComponent;

/// <summary>Marks a reusable carried tool without making it a processing station.</summary>
/// <param name="ToolId">The tool ID referenced by recipes.</param>
public readonly record struct ProcessingToolComponent(string ToolId) : IComponent;

/// <summary>Stores reusable skill IDs for processing; existing cooking and fishing skill components are also recognized.</summary>
public sealed record ProcessingSkillsComponent : IComponent
{
    public IReadOnlyDictionary<string, int> Levels { get; init; } = new Dictionary<string, int>();
}

/// <summary>Records an NPC actively operating one manual job; pausing removes this marker, not its progress.</summary>
/// <param name="HostId">The machinery or NPC entity holding the job.</param>
public readonly record struct OperatingProcessComponent(EntityId HostId) : IComponent;

/// <summary>Stores a persistent processing job on machinery, or on its NPC when no station is required.</summary>
public sealed class ProcessingJobComponent : IComponent
{
    public ProcessingDefinition Recipe { get; }
    public EntityId OwnerId { get; }
    public GameMinute LastUpdatedAt { get; set; }
    public long ProgressMinutes { get; set; }
    public bool Completed { get; set; }
    /// <summary>Rolled output entities stay here until collection, reserving the station even for an empty result.</summary>
    public List<Entity> Outputs { get; } = new();
    /// <summary>Captures the recipe and owner after every requirement has been validated.</summary>
    /// <param name="recipe">The immutable recipe snapshot.</param>
    /// <param name="ownerId">The NPC starting and collecting this job.</param>
    /// <param name="now">The shared game-minute timestamp.</param>
    public ProcessingJobComponent(ProcessingDefinition recipe, EntityId ownerId, GameMinute now)
    { Recipe = recipe; OwnerId = ownerId; LastUpdatedAt = now; }
}
