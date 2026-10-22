namespace RSF.Core.Common;

public readonly record struct GameDate(
    int Year,
    int Day)
{
    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 24;
    public const int MinutesPerDay =
        MinutesPerHour * HoursPerDay;

    public const int DaysPerSeason = 30;
    public const int SeasonsPerYear = 4;
    public const int DaysPerYear =
        DaysPerSeason * SeasonsPerYear;

    public Season CurrentSeason =>
        (Season)((Day - 1) / DaysPerSeason);

    public int DayOfSeason =>
        ((Day - 1) % DaysPerSeason) + 1;

    public override string ToString()
    {
        return
            $"Year {Year}, " +
            $"{CurrentSeason}, " +
            $"Day {DayOfSeason}";
    }
}