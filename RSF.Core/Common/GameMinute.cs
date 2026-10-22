namespace RSF.Core.Common;

public readonly record struct GameMinute(long Value)
{
    public GameTime ToGameTime()
    {
        long minutesPerDay =
            GameDate.MinutesPerDay;

        long minuteOfDay =
            Value % minutesPerDay;

        int hour =
            (int)(minuteOfDay / 60);

        int minute =
            (int)(minuteOfDay % 60);

        return new GameTime(
            hour,
            minute);
    }

    public GameDate ToGameDate()
    {
        long minutesPerDay =
            GameDate.MinutesPerDay;

        long totalDays =
            Value / minutesPerDay;

        int year =
            (int)(totalDays / GameDate.DaysPerYear) + 1;

        int dayOfYear =
            (int)(totalDays % GameDate.DaysPerYear) + 1;

        return new GameDate(
            year,
            dayOfYear);
    }

    public GameMinute Add(long minutes)
    {
        return new GameMinute(
            Value + minutes);
    }

    public GameDuration ElapsedSince(
        GameMinute earlier)
    {
        return new GameDuration(
            Value - earlier.Value);
    }

    public static GameMinute Zero =>
        new(0);

    public override string ToString()
    {
        return Value.ToString();
    }
}