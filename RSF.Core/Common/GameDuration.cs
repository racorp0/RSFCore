namespace RSF.Core.Common;

public readonly record struct GameDuration(
    long Minutes)
{
    public const long MinutesPerHour = 60;

    public const long MinutesPerDay =
        24 * MinutesPerHour;

    public static GameDuration Zero =>
        new(0);

    public static GameDuration FromMinutes(
        long minutes)
    {
        return new GameDuration(minutes);
    }

    public static GameDuration FromHours(
        long hours)
    {
        return new GameDuration(
            hours * MinutesPerHour);
    }

    public static GameDuration FromDays(
        long days)
    {
        return new GameDuration(
            days * MinutesPerDay);
    }

    public GameDuration Add(long minutes)
    {
        return new GameDuration(
            Minutes + minutes);
    }

    public long TotalHours =>
        Minutes / MinutesPerHour;

    public long TotalDays =>
        Minutes / MinutesPerDay;

    public bool IsZero =>
        Minutes == 0;

    public bool IsPositive =>
        Minutes > 0;

    public bool IsNegative =>
        Minutes < 0;

    public override string ToString()
    {
        return $"{Minutes} minute(s)";
    }

    public static bool operator >=(
    GameDuration left,
    GameDuration right)
    {
        return left.Minutes >= right.Minutes;
    }

    public static bool operator <=(
        GameDuration left,
        GameDuration right)
    {
        return left.Minutes <= right.Minutes;
    }

    public static bool operator >(
        GameDuration left,
        GameDuration right)
    {
        return left.Minutes > right.Minutes;
    }

    public static bool operator <(
        GameDuration left,
        GameDuration right)
    {
        return left.Minutes < right.Minutes;
    }

}