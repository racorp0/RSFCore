using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class EquipmentSystem
{
    private readonly World _world;

    private readonly StatSystem _statSystem;

    public EquipmentSystem(
    World world,
    StatSystem statSystem)
    {
        _world = world;
        _statSystem = statSystem;
    }

    public EquipmentSystem(World world)
    {
        _world = world;
    }

    public bool EquipWeapon(Entity player, Entity item)
    {
        if (!_world.TryGet<EquipmentComponent>(
            player,
            out var equipment))
        {
            return false;
        }

        equipment.Weapon = item;

        _statSystem.RecalculateHealth(player);

        return true;
    }

    public bool IsEquipped(Entity player, Entity item)
    {
        if (!_world.TryGet<EquipmentComponent>(
            player,
            out var equipment))
        {
            return false;
        }

        return equipment.Weapon == item;
    }

    public bool UnequipWeapon(Entity player)
    {
        if (!_world.TryGet<EquipmentComponent>(
            player,
            out var equipment))
        {
            return false;
        }

        if (equipment.Weapon is null)
        {
            return false;
        }

        equipment.Weapon = null;

        _statSystem.RecalculateHealth(player);

        return true;
    }
}