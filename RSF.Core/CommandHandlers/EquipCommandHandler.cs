using RSF.Core.Commands;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class EquipCommandHandler
    : ICommandHandler<EquipCommand>
{
    private readonly World _world;
    private readonly InventorySystem _inventorySystem;
    private readonly EquipmentSystem _equipmentSystem;

    public EquipCommandHandler(
        World world,
        InventorySystem inventorySystem,
        EquipmentSystem equipmentSystem)
    {
        _world = world;
        _inventorySystem = inventorySystem;
        _equipmentSystem = equipmentSystem;
    }

    public void Handle(EquipCommand command)
    {
        if (_world.TryGet<DeadComponent>(
            command.Player,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot equip items.");

            return;
        }

        if (!_world.TryGet<ItemComponent>(
            command.Item,
            out var item))
        {
            Console.WriteLine(
                "Entity is not an item.");

            return;
        }

        if (!_inventorySystem.HasItem(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                $"Player does not have {item.Name}.");

            return;
        }

        if (!_world.TryGet<EquipmentItemComponent>(
            command.Item,
            out var equipmentItem))
        {
            Console.WriteLine(
                $"{item.Name} cannot be equipped.");

            return;
        }

        if (equipmentItem.Slot != EquipmentSlot.Weapon)
        {
            Console.WriteLine(
                $"{item.Name} is not a weapon.");

            return;
        }

        if (!_equipmentSystem.EquipWeapon(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                "Player does not have equipment.");

            return;
        }

        Console.WriteLine(
            $"Player equipped {item.Name}.");
    }
}