using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores the resolved preference and invalidates work preparation when its interval changes.</summary>
public sealed class ScheduleStateComponent : IComponent
{
    public ScheduleTemplate? Template { get; set; }
    public ScheduledActivity Activity { get; set; }
    public Job Job { get; set; }
    public LifeStage Stage { get; set; }
    public int EntryIndex { get; set; } = -2;
    public long Day { get; set; } = -1;
    public bool WorkStarted { get; set; }
    public bool PreparingFood { get; set; }
    public int Revision { get; set; }
}
