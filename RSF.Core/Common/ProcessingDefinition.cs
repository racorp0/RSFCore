namespace RSF.Core.Common;

/// <summary>Identifies shared input stacks and distinguishes Meal outputs used by cooking from ordinary processing outputs.</summary>
public enum ProcessingItemKind { Item, Seed, Spice, Meal }

/// <summary>Defines one possible input for a required input group.</summary>
public sealed record ProcessingInputOption
{
    public ProcessingItemKind Kind { get; init; } = ProcessingItemKind.Item;
    public string ItemId { get; init; } = "";
    public string PlantId { get; init; } = "";
    public int Quantity { get; init; } = 1;
}

/// <summary>Requires exactly one fully supplied alternative, with quantities reserved across all groups.</summary>
public sealed record ProcessingInputGroup
{
    public IReadOnlyList<ProcessingInputOption> Options { get; init; } = Array.Empty<ProcessingInputOption>();
}

/// <summary>Describes one weighted output option; seed outputs identify a plant instead of an ordinary item.</summary>
public sealed record ProcessingOutputOption : HarvestOutputDefinition
{
    public ProcessingItemKind Kind { get; init; } = ProcessingItemKind.Item;
    public string PlantId { get; init; } = "";
    /// <summary>Existing food effects used only by Meal outputs in cooking recipes.</summary>
    public int Hunger { get; init; }
    public int Health { get; init; }
    public int ChancePercent { get; init; } = 100;
}

/// <summary>Rolls one mutually exclusive result; unused probability produces nothing from this group.</summary>
public sealed record ProcessingOutputGroup
{
    public IReadOnlyList<ProcessingOutputOption> Options { get; init; } = Array.Empty<ProcessingOutputOption>();
}

/// <summary>Separates the skill needed to activate a process from whether it needs an operator.</summary>
public sealed record ProcessingSkillRequirement
{
    public string Id { get; init; } = "";
    public int MinimumLevel { get; init; } = 1;
}

/// <summary>Defines shared processing recipe data; cooking Recipe reuses these fields with food-specific validation.</summary>
public record ProcessingDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<ProcessingInputGroup> Inputs { get; init; } = Array.Empty<ProcessingInputGroup>();
    public IReadOnlyList<string>? Tools { get; init; }
    public string? ProcessingStation { get; init; }
    public int ProcessingMinutes { get; init; } = 1;
    public bool RequiresOperator { get; init; } = true;
    public ProcessingSkillRequirement? RequiredSkill { get; init; }
    public IReadOnlyList<ProcessingOutputGroup> Outputs { get; init; } = Array.Empty<ProcessingOutputGroup>();
}

/// <summary>Places one machinery instance on a map tile; recipes refer to its station type.</summary>
public sealed record ProcessingStationDefinition
{
    public string Id { get; init; } = "";
    public string StationType { get; init; } = "";
    public string Name { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
}

/// <summary>Names a reusable inventory tool separately from world machinery.</summary>
public sealed record ProcessingToolDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

/// <summary>Groups shared processing recipes, tools, machinery placements, and external item definitions.</summary>
public sealed record ProcessingSettings
{
    public IReadOnlyList<ProcessingDefinition> Recipes { get; init; } = Array.Empty<ProcessingDefinition>();
    public IReadOnlyList<ProcessingStationDefinition> Stations { get; init; } = Array.Empty<ProcessingStationDefinition>();
    public IReadOnlyList<ProcessingToolDefinition> Tools { get; init; } = Array.Empty<ProcessingToolDefinition>();
    public IReadOnlyList<HarvestOutputDefinition> Items { get; init; } = Array.Empty<HarvestOutputDefinition>();
}
