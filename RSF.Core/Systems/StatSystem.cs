using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class StatSystem
{
    private readonly World _world;

    public StatSystem(World world)
    {
        _world = world;
    }

    public int GetStat(
        Entity entity,
        string statName)
    {
        int value =
            GetBaseStat(
                entity,
                statName);

        value += GetEquipmentModifier(
            entity,
            statName);

        return value;
    }

    private int GetBaseStat(
        Entity entity,
        string statName)
    {
        if (statName == StatNames.Strength)
        {
            if (_world.TryGet<StrengthComponent>(
                entity,
                out var strength))
            {
                return strength.Value;
            }
        }

        if (statName == StatNames.Vitality)
        {
            if (_world.TryGet<VitalityComponent>(
                entity,
                out var vitality))
            {
                return vitality.Value;
            }
        }

        return 0;
    }

    private int GetEquipmentModifier(
        Entity entity,
        string statName)
    {
        if (!_world.TryGet<EquipmentComponent>(
            entity,
            out var equipment))
        {
            return 0;
        }

        if (equipment.Weapon is null)
        {
            return 0;
        }

        if (!_world.TryGet<StatModifierComponent>(
            equipment.Weapon,
            out var modifier))
        {
            return 0;
        }

        return modifier.Get(statName);
    }

    public int GetMaxHealth(Entity entity)
    {
        const int baseHealth = 50;
        const int healthPerVitality = 10;

        int vitality =
            GetStat(
                entity,
                StatNames.Vitality);

        return baseHealth +
               vitality * healthPerVitality;
    }

    public void RecalculateHealth(Entity entity)
    {
        if (!_world.TryGet<HealthComponent>(
            entity,
            out var health))
        {
            return;
        }

        int oldMaxHealth =
            health.Max;

        int newMaxHealth =
            GetMaxHealth(entity);

        int maxHealthDifference =
            newMaxHealth - oldMaxHealth;

        int newCurrentHealth =
            health.Current +
            maxHealthDifference;

        newCurrentHealth =
            Math.Max(
                newCurrentHealth,
                1);

        newCurrentHealth =
            Math.Min(
                newCurrentHealth,
                newMaxHealth);

        _world.Set(
            entity,
            new HealthComponent(
                newCurrentHealth,
                newMaxHealth));
    }
}