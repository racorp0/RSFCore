using System.Collections.Generic;
using System.Diagnostics;
using RSF.Core.Debugging;

using RSF.Core.Systems;

namespace RSF.Core.Simulation;

/// <summary>Owns the world and clock and advances registered systems in their existing one-minute order.</summary>
public sealed class Simulation
{
    private readonly List<ISystem> _systems = new();

    public World World { get; }

    public WorldClock Clock { get; }

    /// <summary>Optional diagnostic timing, disabled unless explicitly enabled by the caller.</summary>
    public SimulationProfiler Profiler { get; } = new();

    /// <summary>Creates the existing world and game clock without enabling diagnostics.</summary>
    public Simulation()
    {
        World = new World();
        Clock = new WorldClock();
    }

    /// <summary>Registers a system in the order it will run each game minute.</summary>
    /// <param name="system">The gameplay system to register.</param>
    public void AddSystem(ISystem system)
    {
        _systems.Add(system);
    }

    /// <summary>Advances whole game minutes, optionally measuring each update without changing simulation behavior.</summary>
    /// <param name="deltaTime">The number of game minutes to advance, using the existing whole-step convention.</param>
    public void Update(float deltaTime)
    {
        const float simulationStep = 1.0f;

        while (deltaTime >= simulationStep)
        {
            foreach (ISystem system in _systems)
            {
                if (!Profiler.Enabled)
                {
                    system.Update(simulationStep);
                    continue;
                }
                long startedAt = Stopwatch.GetTimestamp();
                try
                {
                    system.Update(simulationStep);
                }
                finally
                {
                    // Finally records failed updates too, while preserving the original exception.
                    Profiler.Record(system, Stopwatch.GetTimestamp() - startedAt);
                }
            }

            deltaTime -= simulationStep;
        }
    }
}