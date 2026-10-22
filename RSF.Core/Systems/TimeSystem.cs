using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class TimeSystem : ISystem
{
    private readonly WorldClock _clock;

    public TimeSystem(WorldClock clock)
    {
        _clock = clock;
    }

    public void Update(float deltaTime)
    {
        _clock.Advance(1);
    }
}