using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

public sealed class InventorySystem
{
    private readonly World _world;

    public InventorySystem(World world)
    {
        _world = world;
    }

    public bool AddItem(
        Entity player,
        Entity item)
    {
        if (!_world.TryGet<InventoryComponent>(
            player,
            out var inventory))
        {
            return false;
        }

        inventory.Items.Add(item);

        return true;
    }

    public bool HasItem(
        Entity player,
        Entity item)
    {
        if (!_world.TryGet<InventoryComponent>(
            player,
            out var inventory))
        {
            return false;
        }

        return inventory.Items.Contains(item);
    }

    public bool RemoveItem(
        Entity player,
        Entity item)
    {
        if (!_world.TryGet<InventoryComponent>(
            player,
            out var inventory))
        {
            return false;
        }

        return  inventory.Items.Remove(item);
    }
}