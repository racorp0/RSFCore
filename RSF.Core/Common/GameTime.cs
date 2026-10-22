namespace RSF.Core.Common;

public readonly record struct GameTime(
    int Hour,
    int Minute)
{
    public bool IsDay =>
        Hour >= 6 && Hour < 18;

    public bool IsNight =>
        !IsDay;

    public override string ToString()
    {
        return $"{Hour:00}:{Minute:00}";
    }
}