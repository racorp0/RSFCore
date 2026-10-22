using System.Diagnostics;
using System.Text;
using RSF.Core.Systems;

namespace RSF.Core.Debugging;

/// <summary>Collects optional timings around existing system updates without changing their execution order or game time.</summary>
public sealed class SimulationProfiler
{
    // Use object identity so two instances stay separate even if their Equals methods consider them equal.
    private readonly Dictionary<ISystem, SystemTiming> _timings = new(ReferenceEqualityComparer.Instance);

    /// <summary>Disabled by default; enabling measures subsequent updates without printing during simulation.</summary>
    public bool Enabled { get; set; }
    /// <summary>Accumulated measurements for each measured system instance.</summary>
    public IReadOnlyCollection<SystemTiming> Timings => _timings.Values;

    /// <summary>Clears measurements so a warm-up or earlier run can be excluded from the next report.</summary>
    public void Reset() => _timings.Clear();

    /// <summary>Accumulates a completed or failed update using the diagnostic stopwatch clock.</summary>
    /// <param name="system">The system instance that was measured.</param>
    /// <param name="elapsedTicks">Elapsed stopwatch ticks, which are separate from game minutes.</param>
    internal void Record(ISystem system, long elapsedTicks)
    {
        if (!_timings.TryGetValue(system, out var timing))
        {
            timing = new SystemTiming { SystemName = system.GetType().Name };
            _timings.Add(system, timing);
        }
        timing.UpdateCount++;
        timing.TotalTicks += elapsedTicks;
        timing.MaximumTicks = Math.Max(timing.MaximumTicks, elapsedTicks);
    }

    /// <summary>Formats the slowest systems first, including calls and total, average, and maximum milliseconds.</summary>
    /// <returns>A diagnostic summary; callers decide when to print or save it.</returns>
    public string Dump()
    {
        StringBuilder report = new();
        double total = _timings.Values.Sum(timing => timing.TotalMilliseconds);
        report.AppendLine($"System update profile: {total:F2} ms measured; {Timings.Count} system instances.");
        report.AppendLine("System | Updates | Total ms | Average ms | Maximum ms | Share");
        foreach (var timing in _timings.Values.OrderByDescending(timing => timing.TotalMilliseconds))
        {
            // Shares describe measured update time, not setup, rendering, or the entire process lifetime.
            double share = total > 0 ? timing.TotalMilliseconds / total * 100 : 0;
            report.AppendLine($"{timing.SystemName} | {timing.UpdateCount} | {timing.TotalMilliseconds:F2} | {timing.AverageMilliseconds:F4} | {timing.MaximumMilliseconds:F4} | {share:F1}%");
        }
        return report.ToString();
    }
}

/// <summary>Stores accumulated diagnostic time for one system instance; it contains no gameplay state.</summary>
public sealed class SystemTiming
{
    public string SystemName { get; internal set; } = "";
    public long UpdateCount { get; internal set; }
    internal long TotalTicks { get; set; }
    internal long MaximumTicks { get; set; }
    /// <summary>Total elapsed real time inside this system's measured updates.</summary>
    public double TotalMilliseconds => TotalTicks * 1000.0 / Stopwatch.Frequency;
    public double AverageMilliseconds => UpdateCount > 0 ? TotalMilliseconds / UpdateCount : 0;
    public double MaximumMilliseconds => MaximumTicks * 1000.0 / Stopwatch.Frequency;
}
