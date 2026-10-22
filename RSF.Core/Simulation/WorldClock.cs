using RSF.Core.Common;

namespace RSF.Core.Simulation;

public sealed class WorldClock
{
    public GameMinute CurrentMinute { get; private set; }

    public WorldClock()
    {
        CurrentMinute = GameMinute.Zero;
    }

    public void Advance(long minutes)
    {
        CurrentMinute =
            CurrentMinute.Add(minutes);
    }
}