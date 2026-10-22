using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Opts an NPC into scheduling; an optional personal template replaces job defaults.</summary>
/// <param name="PersonalTemplate">A custom preference template, or null to follow job and life stage.</param>
public sealed record ScheduleComponent(ScheduleTemplate? PersonalTemplate = null) : IComponent;
