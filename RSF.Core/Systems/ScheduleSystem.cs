using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Resolves daily preferences without issuing gameplay commands or overriding needs.</summary>
public sealed class ScheduleSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly GameSettings? _settings;
    /// <summary>Connects schedule evaluation to the shared world and clock.</summary>
    /// <param name="world">The world containing scheduled NPCs.</param>
    /// <param name="clock">The current game clock.</param>
    public ScheduleSystem(World world, WorldClock clock) { _world = world; _clock = clock; _settings = GameSettings.Find(world); }

    /// <summary>Updates scheduled entities' preferences when time, job, life stage, or personal template changes.</summary>
    /// <param name="deltaTime">The simulation step; schedules use the shared game clock.</param>
    public void Update(float deltaTime)
    {
        int minute = (int)(_clock.CurrentMinute.Value % GameDate.MinutesPerDay);
        long day = _clock.CurrentMinute.Value / GameDate.MinutesPerDay;
        // The existing schedule index skips terrain and entities without planned activities.
        // Updating ScheduleState leaves the Schedule collection unchanged, so this live query is safe.
        foreach (Entity npc in _world.GetEntitiesWith<ScheduleComponent>())
        {
            if (!_world.TryGet<ScheduleComponent>(npc, out var schedule) || schedule is null) continue;
            _world.TryGet<JobComponent>(npc, out var job);
            // Legacy NPCs without a life-stage component retain adult behavior.
            LifeStage stage = _world.TryGet<LifeStageComponent>(npc, out var life) ? life.Stage : LifeStage.Adult;
            ScheduleTemplate template = stage == LifeStage.Adult
                ? schedule.PersonalTemplate ?? (_settings?.ScheduleFor(job.Job, stage) ?? ScheduleTemplates.For(job.Job, stage))
                : (_settings?.ScheduleFor(job.Job, stage) ?? ScheduleTemplates.For(job.Job, stage));
            int index = -1;
            for (int i = 0; i < template.Entries.Count; i++)
                if (template.Entries[i].Contains(minute)) { index = i; break; }
            // An overnight work interval belongs to the day it started, so midnight does not restart preparation.
            long intervalDay = day;
            if (index >= 0 && template.Entries[index].StartMinute >= template.Entries[index].EndMinute &&
                minute < template.Entries[index].StartMinute)
                intervalDay--;
            if (!_world.TryGet<ScheduleStateComponent>(npc, out var state) || state is null)
            { state = new ScheduleStateComponent(); _world.Add(npc, state); }
            if (state.Template == template && state.EntryIndex == index && state.Day == intervalDay &&
                state.Job == job.Job && state.Stage == stage) continue;
            state.Template = template; state.EntryIndex = index; state.Day = intervalDay;
            state.Job = job.Job; state.Stage = stage;
            state.Activity = index < 0 ? ScheduledActivity.FreeTime : template.Entries[index].Activity;
            state.WorkStarted = false; state.PreparingFood = false; state.Revision++;
        }
    }
}
