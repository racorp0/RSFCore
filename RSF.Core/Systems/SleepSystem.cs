using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Maintains fatigue and restores it only while sleeping at an owned home or paid inn.</summary>
public sealed class SleepSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly TravelSystem _travel;
    /// <summary>Connects sleep to elapsed time and existing travel.</summary>
    /// <param name="world">The NPC world.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="travel">The system used to stop movement before sleep.</param>
    public SleepSystem(World world, WorldClock clock, TravelSystem travel) { _world = world; _clock = clock; _travel = travel; }

    /// <summary>Begins sleep at an entitled place and pauses manual processing while preserving its progress.</summary>
    /// <param name="npc">The living sleeper.</param>
    /// <param name="place">The owned house or rented inn.</param>
    /// <returns>True if the NPC is now sleeping.</returns>
    public bool Start(Entity npc, Entity place)
    {
        if (!CanSleep(npc, place) || _world.Has<CookingAttemptComponent>(npc) || _world.Has<FishingAttemptComponent>(npc)) return false;
        _world.Remove<OperatingProcessComponent>(npc);
        _travel.Stop(npc);
        _world.Add(npc, new SleepingComponent(place.Id, _clock.CurrentMinute));
        return true;
    }

    /// <summary>Wakes an NPC without granting recovery for awake time.</summary>
    /// <param name="npc">The sleeper to wake.</param>
    public void Wake(Entity npc)
    {
        _world.Remove<SleepingComponent>(npc);
        _world.Add(npc, new FatigueClockComponent(_clock.CurrentMinute));
    }

    /// <summary>Updates configured NPCs using separate timestamps for awake fatigue and sleeping recovery.</summary>
    /// <param name="deltaTime">The simulation step.</param>
    public void Update(float deltaTime)
    {
        // Use the existing component index so terrain does not need sleep checks.
        // Recovery and waking change other components, leaving this settings collection safe to enumerate.
        foreach (Entity npc in _world.GetEntitiesWith<SleepSettingsComponent>())
        {
            if (!_world.TryGet<SleepSettingsComponent>(npc, out var settings) || settings is null ||
                !_world.TryGet<FatigueComponent>(npc, out var tired)) continue;
            if (settings.AwakeMinutesPerPoint <= 0 || settings.SleepMinutesPerPoint <= 0 || settings.WakeFatigue < 0 ||
                settings.PreferredFatigue <= settings.WakeFatigue || settings.UrgentFatigue < settings.PreferredFatigue ||
                settings.UrgentFatigue > 100 || settings.BedtimeMinute < 0 || settings.BedtimeMinute >= 1440 ||
                settings.WakeTimeMinute < 0 || settings.WakeTimeMinute >= 1440 ||
                !double.IsFinite(settings.SleepPoints) || settings.SleepPoints < 0 ||
                !double.IsFinite(settings.SeverityWeight) || settings.SeverityWeight < 0)
                throw new ArgumentException("Invalid sleep thresholds, scores, or intervals.");
            if (_world.Has<DeadComponent>(npc) || !_world.TryGet<HealthComponent>(npc, out var hp) || hp.Current <= 0)
            { _world.Remove<SleepingComponent>(npc); continue; }
            if (_world.TryGet<SleepingComponent>(npc, out var sleep))
            {
                if (!_world.TryGetEntity(sleep.PlaceId, out var place) || place is null || !CanSleep(npc, place)) { Wake(npc); continue; }
                long ticks = _clock.CurrentMinute.ElapsedSince(sleep.LastTickAt).Minutes / settings.SleepMinutesPerPoint;
                if (ticks > 0)
                {
                    int value = (int)Math.Max(0, tired.Value - ticks);
                    _world.Add(npc, new FatigueComponent(value));
                    _world.Add(npc, sleep with { LastTickAt = sleep.LastTickAt.Add(ticks * settings.SleepMinutesPerPoint) });
                    if (value <= settings.WakeFatigue) Wake(npc);
                }
            }
            else
            {
                if (!_world.TryGet<FatigueClockComponent>(npc, out var timer)) timer = new FatigueClockComponent(_clock.CurrentMinute);
                long ticks = _clock.CurrentMinute.ElapsedSince(timer.LastTickAt).Minutes / settings.AwakeMinutesPerPoint;
                _world.Add(npc, new FatigueComponent((int)Math.Min(100, tired.Value + ticks)));
                _world.Add(npc, new FatigueClockComponent(timer.LastTickAt.Add(ticks * settings.AwakeMinutesPerPoint)));
            }
        }
    }

    /// <summary>Checks health, proximity, home ownership, or an unexpired paid bed.</summary>
    /// <param name="npc">The sleeper.</param>
    /// <param name="place">The sleeping place.</param>
    /// <returns>True if sleeping there is allowed.</returns>
    private bool CanSleep(Entity npc, Entity place)
    {
        if (!VillageRules.Near(_world, npc, place.Id) || !_world.TryGet<HealthComponent>(npc, out var hp) || hp.Current <= 0 ||
            !_world.TryGet<SleepSettingsComponent>(npc, out _)) return false;
        bool home = _world.TryGet<HomeComponent>(npc, out var house) && house.HouseId == place.Id &&
            _world.TryGet<OwnershipComponent>(place, out var owner) && owner is not null && owner.OwnerId == npc.Id;
        return home || (_world.TryGet<InnStayComponent>(npc, out var stay) && stay.InnId == place.Id && _clock.CurrentMinute.Value < stay.PaidUntil.Value);
    }
}
