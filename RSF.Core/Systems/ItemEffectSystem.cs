using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class ItemEffectSystem
{
    private readonly World _world;
    private readonly WorldClock _clock;

    public ItemEffectSystem(
    World world,
    WorldClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <summary>Applies implemented effects only while the item remains fresh.</summary>
    /// <param name="player">The actor receiving effects.</param>
    /// <param name="item">The source item.</param>
    /// <returns>True when at least one supported effect applied; false for expired items.</returns>
    public bool ApplyEffects(
    Entity player,
    Entity item)
    {
        if (FoodRules.IsExpired(_world, item)) return false;
        bool appliedAnyEffect = false;

        if (_world.TryGet<RestoreHealthEffectComponent>(
            item,
            out var healthEffect))
        {
            if (ApplyHealthEffect(
                player,
                healthEffect))
            {
                appliedAnyEffect = true;
            }
        }

        if (_world.TryGet<RestoreManaEffectComponent>(
            item,
            out var manaEffect))
        {
            if (ApplyManaEffect(
                player,
                manaEffect))
            {
                appliedAnyEffect = true;
            }
        }

        if (_world.TryGet<RestoreHungerEffectComponent>(
            item,
            out var hungerEffect))
        {
            if (ApplyHungerEffect(
                player,
                hungerEffect))
            {
                appliedAnyEffect = true;
            }
        }

        if (_world.TryGet<ApplyPoisonEffectComponent>(
            item,
            out var poisonEffect))
        {
            if (ApplyPoisonEffect(
                player,
                poisonEffect))
            {
                appliedAnyEffect = true;
            }
        }

        if (_world.TryGet<ApplyRegenerationEffectComponent>(
            item,
            out var regenerationEffect))
        {
            if (ApplyRegenerationEffect(
                player,
                regenerationEffect))
            {
                appliedAnyEffect = true;
            }
        }

        return appliedAnyEffect;
    }

    private bool ApplyHealthEffect(
        Entity player,
        RestoreHealthEffectComponent effect)
    {
        if (!_world.TryGet<HealthComponent>(
            player,
            out var health))
        {
            return false;
        }

        int newHealth =
            Math.Min(
                health.Current + effect.Amount,
                health.Max);

        _world.Add(
            player,
            new HealthComponent(
                newHealth,
                health.Max));

        return true;
    }

    private bool ApplyManaEffect(
    Entity player,
    RestoreManaEffectComponent effect)
    {
        if (!_world.TryGet<ManaComponent>(
            player,
            out var mana))
        {
            return false;
        }

        int newMana =
            Math.Min(
                mana.Current + effect.Amount,
                mana.Max);

        _world.Add(
            player,
            new ManaComponent(
                newMana,
                mana.Max));

        return true;
    }

    private bool ApplyHungerEffect(
    Entity player,
    RestoreHungerEffectComponent effect)
    {
        if (!_world.TryGet<HungerComponent>(
            player,
            out var hunger))
        {
            return false;
        }

        int newHunger =
            Math.Min(
                hunger.Current + effect.Amount,
                hunger.Max);

        _world.Add(
            player,
            new HungerComponent(
                newHunger,
                hunger.Max));

        return true;
    }

    private bool ApplyPoisonEffect(
    Entity player,
    ApplyPoisonEffectComponent effect)
    {
        PoisonStatusComponent poison =
        new(
            effect.DamagePerTick,
            effect.TickInterval,
            effect.Duration,
            _clock.CurrentMinute,
            _clock.CurrentMinute);
        
        _world.Add(
            player,
            poison);

        return true;
    }

    private bool ApplyRegenerationEffect(
    Entity player,
    ApplyRegenerationEffectComponent effect)
    {

        if (_world.TryGet<DeadComponent>(
        player,
        out _))
        {
            return false;
        }

        RegenerationStatusComponent regeneration =
            new(
                effect.HealPerTick,
                effect.TickInterval,
                effect.Duration,
                _clock.CurrentMinute,
                _clock.CurrentMinute);

        _world.Add(
            player,
            regeneration);

        return true;
    }
}