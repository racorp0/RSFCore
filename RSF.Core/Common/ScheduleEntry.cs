namespace RSF.Core.Common;

/// <summary>Defines a daily interval, including intervals crossing midnight.</summary>
/// <param name="StartMinute">Inclusive minute of day, from zero to 1439.</param>
/// <param name="EndMinute">Exclusive minute of day; equal to start means a full day.</param>
/// <param name="Activity">The activity preferred during this interval.</param>
public sealed record ScheduleEntry(int StartMinute, int EndMinute, ScheduledActivity Activity)
{
    /// <summary>Checks whether a minute belongs to this daily interval.</summary>
    /// <param name="minute">The minute of day.</param>
    /// <returns>True when this interval includes the minute.</returns>
    public bool Contains(int minute)
    {
        return StartMinute == EndMinute || (StartMinute < EndMinute
            ? minute >= StartMinute && minute < EndMinute
            : minute >= StartMinute || minute < EndMinute);
    }
}
