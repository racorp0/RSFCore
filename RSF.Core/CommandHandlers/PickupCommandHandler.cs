using RSF.Core.Commands;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class PickupCommandHandler
    : ICommandHandler<PickupCommand>
{
    private readonly World _world;
    private readonly InventorySystem _inventorySystem;

    public PickupCommandHandler(
        World world,
        InventorySystem inventorySystem)
    {
        _world = world;
        _inventorySystem = inventorySystem;
    }

    public void Handle(
        PickupCommand command)
    {
        if (!_world.TryGet<ItemComponent>(
            command.Item,
            out var item))
        {
            Console.WriteLine(
                "Entity is not an item.");

            return;
        }

        if (_inventorySystem.HasItem(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                $"Player already has {item.Name}.");

            return;
        }

        bool added =
            _inventorySystem.AddItem(
                command.Player,
                command.Item);

        if (!added)
        {
            Console.WriteLine(
                "Player does not have an inventory.");

            return;
        }

        Console.WriteLine(
            $"Player picked up {item.Name}.");
    }
}