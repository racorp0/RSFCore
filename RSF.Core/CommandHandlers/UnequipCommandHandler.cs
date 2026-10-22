using RSF.Core.Commands;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class UnequipCommandHandler
    : ICommandHandler<UnequipCommand>
{
    private readonly EquipmentSystem _equipmentSystem;
    private readonly World _world;

    public UnequipCommandHandler(
        EquipmentSystem equipmentSystem,
        World world)
    {
        _equipmentSystem = equipmentSystem;
        _world = world;
    }

    public void Handle(UnequipCommand command)
    {
        if (_world.TryGet<DeadComponent>(
            command.Player,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot equip items.");

            return;
        }

        if (!_world.TryGet<EquipmentComponent>(
            command.Player,
            out var equipment))
        {
            Console.WriteLine(
                "Player does not have equipment.");

            return;
        }

        if (equipment.Weapon is null)
        {
            Console.WriteLine(
                "Player has no weapon equipped.");

            return;
        }

        Entity weapon = equipment.Weapon;

        if (!_world.TryGet<ItemComponent>(
            weapon,
            out var item))
        {
            Console.WriteLine(
                "Equipped entity is not a valid item.");

            return;
        }

        if (!_equipmentSystem.UnequipWeapon(
            command.Player))
        {
            Console.WriteLine(
                "Failed to unequip weapon.");

            return;
        }

        Console.WriteLine(
            $"Player unequipped {item.Name}.");
    }
}