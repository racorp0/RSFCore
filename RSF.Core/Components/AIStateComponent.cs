using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Stores one NPC's selected action, score, and execution feedback between simulation updates.</summary>
public sealed class AIStateComponent : IComponent
{
    public AIActivity Activity { get; set; } = AIActivity.Wait;
    public EntityId TargetId { get; set; } = EntityId.Empty;
    public EntityId SellerId { get; set; } = EntityId.Empty;
    public double Score { get; set; }
    /// <summary>Critical health outranks urgent hunger; points rank activities within the same urgency.</summary>
    public int Urgency { get; set; }
    public GameMinute NextDecisionAt { get; set; } = GameMinute.Zero;
    public GameMinute NextActionAt { get; set; } = GameMinute.Zero;
    public bool Pending { get; set; }
    public string LastResult { get; set; } = "Not evaluated";
    /// <summary>Remembers failed targets individually so two unavailable items cannot cause an endless retry loop.</summary>
    public Dictionary<EntityId, GameMinute> FailedTargets { get; } = new();
    public bool PreviousHealthCritical { get; set; }
    public bool PreviousHungerUrgent { get; set; }
    /// <summary>Last observed schedule revision; a changed preference requests a fresh decision.</summary>
    public int ScheduleRevision { get; set; } = -1;
    public int Quantity { get; set; } = 1;
}
