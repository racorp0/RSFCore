namespace RSF.Core.Common;

/// <summary>Provides reusable schedules; callers can assign a personal template instead.</summary>
public static class ScheduleTemplates
{
    public static ScheduleTemplate Farmer { get; } = new("Farmer",
        new(6 * 60, 12 * 60, ScheduledActivity.Work),
        new(12 * 60, 13 * 60, ScheduledActivity.MealBreak),
        new(13 * 60, 18 * 60, ScheduledActivity.Work));
    public static ScheduleTemplate Fisher { get; } = new("Fisher",
        new(7 * 60, 11 * 60, ScheduledActivity.Work),
        new(11 * 60, 12 * 60, ScheduledActivity.MealBreak),
        new(12 * 60, 17 * 60, ScheduledActivity.Work));
    public static ScheduleTemplate Cook { get; } = new("Cook and trading",
        new(5 * 60, 11 * 60, ScheduledActivity.Work),
        new(11 * 60, 12 * 60, ScheduledActivity.MealBreak),
        new(12 * 60, 19 * 60, ScheduledActivity.Work));
    public static ScheduleTemplate Merchant { get; } = new("Traveling merchant",
        new ScheduleEntry(6 * 60, 18 * 60, ScheduledActivity.Work));
    /// <summary>Provides manual worlds with a smoking shift and a meal break; configured worlds use schedules.json.</summary>
    public static ScheduleTemplate Smoker { get; } = new("Smoker",
        new(6 * 60, 12 * 60, ScheduledActivity.Work),
        new(12 * 60, 13 * 60, ScheduledActivity.MealBreak),
        new(13 * 60, 18 * 60, ScheduledActivity.Work));
    public static ScheduleTemplate Unemployed { get; } = new("Unemployed",
        new ScheduleEntry(12 * 60, 13 * 60, ScheduledActivity.MealBreak));
    public static ScheduleTemplate Baby { get; } = new("Baby");
    public static ScheduleTemplate Toddler { get; } = new("Toddler");
    public static ScheduleTemplate Teenager { get; } = new("Teenager",
        new ScheduleEntry(12 * 60, 13 * 60, ScheduledActivity.MealBreak));

    /// <summary>Selects a compatible template, including smoker work, when a job or life stage changes.</summary>
    /// <param name="job">The assigned job.</param>
    /// <param name="stage">The current life stage.</param>
    /// <returns>The shared schedule for this situation.</returns>
    public static ScheduleTemplate For(Job job, LifeStage stage)
    {
        if (stage == LifeStage.Baby) return Baby;
        if (stage == LifeStage.Toddler) return Toddler;
        if (stage == LifeStage.Teenager) return Teenager;
        return job switch { Job.Farmer => Farmer, Job.Fisher => Fisher, Job.Cook => Cook, Job.Merchant => Merchant, Job.Smoker => Smoker, _ => Unemployed };
    }
}
