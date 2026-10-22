using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Shares quantity operations for configured product items, plant seeds, bait, and spice stacks.</summary>
public static class InventoryStacks
{
    /// <summary>Reads usable quantities; spoiled food has zero usable units while its stored physical quantity stays inspectable.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The inventory item.</param>
    /// <returns>The stack quantity, or one for an individual item.</returns>
    public static int Count(World world, Entity item)
    {
        if (FoodRules.IsExpired(world, item)) return 0;
        if (world.TryGet<PlantSeedComponent>(item, out var plantSeeds)) return plantSeeds.Quantity;
        if (world.TryGet<PlantProduceComponent>(item, out var produce)) return produce.Quantity;
        if (world.TryGet<BaitComponent>(item, out var bait)) return bait.Quantity;
        if (world.TryGet<SpiceComponent>(item, out var spice)) return spice.Quantity;
        return 1;
    }
    /// <summary>Counts one output item ID across inventory stacks, keeping different products separate.</summary>
    /// <param name="world">The world storing item components.</param>
    /// <param name="items">The inventory or filtered stock to inspect.</param>
    /// <param name="itemId">The output ID to count.</param>
    /// <returns>The total quantity, using long to prevent overflow across large stocks.</returns>
    public static long CountItem(World world, IEnumerable<Entity> items, string itemId) => items
        .Where(e => world.TryGet<PlantProduceComponent>(e, out var product) && product.ItemId == itemId)
        .Sum(e => (long)Count(world, e));

    /// <summary>Updates an existing stack while preserving its seed, output item, or bait identity.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The existing stack.</param>
    /// <param name="quantity">The non-negative quantity to store.</param>
    public static void Set(World world, Entity item, int quantity)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (world.TryGet<PlantSeedComponent>(item, out var seeds)) world.Add(item, seeds with { Quantity = quantity });
        else if (world.TryGet<PlantProduceComponent>(item, out var produce)) world.Add(item, produce with { Quantity = quantity });
        else if (world.TryGet<BaitComponent>(item, out var bait)) world.Add(item, bait with { Quantity = quantity });
        else if (world.Has<SpiceComponent>(item)) world.Add(item, new SpiceComponent(quantity));
        else throw new InvalidOperationException("This item is not a supported stack.");
    }
    /// <summary>Identifies supported stack types.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The proposed stack.</param>
    /// <returns>True for configured output items, plant seeds, bait, or spice.</returns>
    public static bool IsStack(World world, Entity item) => world.Has<PlantSeedComponent>(item) || world.Has<PlantProduceComponent>(item) || world.Has<BaitComponent>(item) || world.Has<SpiceComponent>(item);

    /// <summary>Creates a partial purchase preserving its seed, output item, or bait identity with raw-food serving effects, and original expiration, without copying the seller's listing.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="source">The stack whose type and name are retained.</param>
    /// <param name="quantity">The positive partial purchase quantity.</param>
    /// <returns>The newly registered inventory item.</returns>
    public static Entity Split(World world, Entity source, int quantity)
    {
        if (!IsStack(world, source) || quantity <= 0 || quantity >= Count(world, source))
            throw new ArgumentException("A split must be smaller than the source stack.");
        world.TryGet<ItemComponent>(source, out var name);
        Entity result = world.CreateEntity();
        world.Add(result, name);
        if (world.TryGet<PlantSeedComponent>(source, out var seeds)) world.Add(result, seeds with { Quantity = quantity });
        else if (world.TryGet<PlantProduceComponent>(source, out var produce)) world.Add(result, produce with { Quantity = quantity });
        else if (world.TryGet<BaitComponent>(source, out var bait)) world.Add(result, bait with { Quantity = quantity });
        else world.Add(result, new SpiceComponent(quantity));
        // Edible product stacks keep the same per-serving effects after a partial purchase.
        if (world.TryGet<FoodServingComponent>(source, out var serving))
        {
            world.Add(result, serving);
            world.Add(result, new ConsumableItemComponent());
            if (world.TryGet<RestoreHungerEffectComponent>(source, out var hunger)) world.Add(result, hunger);
            if (world.TryGet<RestoreHealthEffectComponent>(source, out var health)) world.Add(result, health);
        }
        FoodRules.Copy(world, source, result);
        return result;
    }
}
