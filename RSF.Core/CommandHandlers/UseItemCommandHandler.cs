using RSF.Core.Commands;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class UseItemCommandHandler
    : ICommandHandler<UseItemCommand>
{
    private readonly World _world;
    private readonly InventorySystem _inventorySystem;
    private readonly ItemEffectSystem _itemEffectSystem;

    public UseItemCommandHandler(
        World world,
        InventorySystem inventorySystem,
        ItemEffectSystem itemEffectSystem)
    {
        _world = world;
        _inventorySystem = inventorySystem;
        _itemEffectSystem = itemEffectSystem;
    }

    /// <summary>Uses one fresh complete food serving or individual consumable, preserving the rest of an edible stack.</summary>
    /// <param name="command">The actor and item to use.</param>
    public void Handle(UseItemCommand command)
    {
        if (_world.TryGet<DeadComponent>(
            command.Player,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot use items.");

            return;
        }

        if (!_inventorySystem.HasItem(
            command.Player,
            command.Item))
        {
            Console.WriteLine(
                "Player does not have this item.");

            return;
        }

        if (!_world.TryGet<ConsumableItemComponent>(
            command.Item,
            out _))
        {
            Console.WriteLine(
                "This item is not consumable.");

            return;
        }

        if (!_world.TryGet<ItemComponent>(
            command.Item,
            out var item))
        {
            Console.WriteLine(
                "Entity is not a valid item.");

            return;
        }

        if (FoodRules.IsExpired(_world, command.Item)) return;
        if (!VillageRules.CanConsume(_world, command.Player, command.Item)) return;

        int units = RawFoodRules.Units(_world, command.Item);
        int available = InventoryStacks.Count(_world, command.Item);
        if (units <= 0 || available < units) return;

        bool applied =
            _itemEffectSystem.ApplyEffects(
                command.Player,
                command.Item);

        if (!applied)
        {
            Console.WriteLine(
                $"This item has no implemented effect: {item.Name}");

            return;
        }

        if (_world.Has<FoodServingComponent>(command.Item) && available > units)
            InventoryStacks.Set(_world, command.Item, available - units);
        else _inventorySystem.RemoveItem(command.Player, command.Item);

        Console.WriteLine(
            $"Player consumed {item.Name}.");
    }
}