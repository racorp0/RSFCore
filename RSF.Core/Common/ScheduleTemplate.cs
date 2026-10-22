namespace RSF.Core.Common;

/// <summary>Shares an immutable daily schedule across NPCs while leaving personal state separate.</summary>
public sealed class ScheduleTemplate
{
    public string Name { get; }
    public IReadOnlyList<ScheduleEntry> Entries { get; }

    /// <summary>Copies entries and rejects invalid or overlapping daily intervals.</summary>
    /// <param name="name">The template's readable name.</param>
    /// <param name="entries">The preferred activity intervals; gaps mean free time.</param>
    public ScheduleTemplate(string name, params ScheduleEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A schedule needs a name.", nameof(name));
        bool[] occupied = new bool[GameDate.MinutesPerDay];
        foreach (ScheduleEntry entry in entries)
        {
            if (entry is null || entry.StartMinute < 0 || entry.StartMinute >= occupied.Length ||
                entry.EndMinute < 0 || entry.EndMinute >= occupied.Length || !Enum.IsDefined(entry.Activity))
            {
                throw new ArgumentException("Schedule minutes must be between zero and 1439.", nameof(entries));
            }
            // Validate once when creating the shared template, rather than for every NPC update.
            for (int minute = 0; minute < occupied.Length; minute++)
            {
                if (!entry.Contains(minute)) continue;
                if (occupied[minute]) throw new ArgumentException("Schedule entries cannot overlap.", nameof(entries));
                occupied[minute] = true;
            }
        }
        Name = name;
        Entries = Array.AsReadOnly((ScheduleEntry[])entries.Clone());
    }
}
