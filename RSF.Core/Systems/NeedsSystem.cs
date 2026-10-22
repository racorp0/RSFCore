using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Evaluates configured survival needs without duplicating HungerSystem's decay or damage.</summary>
public sealed class NeedsSystem : ISystem
{
    private readonly World _world;

    /// <summary>Connects need evaluation to the existing world.</summary>
    /// <param name="world">The simulation world containing opt-in NPC settings.</param>
    public NeedsSystem(World world)
    {
        _world = world;
    }

    /// <summary>Updates configured NPCs' need flags, keeping each need active until its recovery target is met.</summary>
    /// <param name="deltaTime">The simulation step; existing systems maintain the actual survival values.</param>
    public void Update(float deltaTime)
    {
        // The existing component index skips terrain and other entities without needs settings.
        // Updating NeedsState is safe here because the NeedsSettings collection stays unchanged.
        foreach (Entity npc in _world.GetEntitiesWith<NeedsSettingsComponent>())
        {
            if (!_world.TryGet<NeedsSettingsComponent>(npc, out var settings) || settings is null)
            {
                continue;
            }
            settings.Validate();
            if (_world.Has<DeadComponent>(npc) ||
                !_world.TryGet<HealthComponent>(npc, out var health) || health.Current <= 0 || health.Max <= 0)
            {
                _world.Remove<NeedsStateComponent>(npc);
                continue;
            }

            _world.TryGet<NeedsStateComponent>(npc, out var previous);
            double hp = Math.Clamp(100.0 * health.Current / health.Max, 0, 100);
            bool hasHunger = _world.TryGet<HungerComponent>(npc, out var hunger) && hunger.Max > 0;
            double food = hasHunger ? Math.Clamp(100.0 * hunger.Current / hunger.Max, 0, 100) : 100;
            bool critical = hp <= settings.CriticalHealthPercent;
            bool urgent = hasHunger && food <= settings.UrgentHungerPercent;

            // Separate start and stop thresholds keep NPCs from stopping after one insufficient meal.
            bool heal = critical || (previous.SeekHealing && hp < settings.HealthyPercent);
            bool eat = hasHunger && (urgent || (previous.SeekFood && food < settings.SatisfiedHungerPercent));
            _world.Add(npc, new NeedsStateComponent(hp, food, heal, eat, critical, urgent));
        }
    }
}
