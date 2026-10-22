using RSF.Core.Commands;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class DropCommandHandler
    : ICommandHandler<DropCommand>
{
    private readonly InventorySystem _inventorySystem;
    private readonly EquipmentSystem _equipmentSystem;

    public DropCommandHandler(
        InventorySystem inventorySystem,
        EquipmentSystem equipmentSystem)
    {
        _inventorySystem = inventorySystem;
        _equipmentSystem = equipmentSystem;
    }

    public void Handle(DropCommand command)
    {
        if (!_inventorySystem.HasItem(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                "Player does not have this item.");

            return;
        }

        if (_equipmentSystem.IsEquipped(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                "Cannot drop an equipped item.");

            return;
        }

        bool removed =
            _inventorySystem.RemoveItem(
                command.Player,
                command.Item);

        if (!removed)
        {
            Console.WriteLine(
                "Failed to remove item.");

            return;
        }

        Console.WriteLine(
            "Player dropped the item.");
    }
}