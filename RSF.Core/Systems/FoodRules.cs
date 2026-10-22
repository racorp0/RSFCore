using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Shares food deadlines across creation, splitting, chopping, consumption, and existing command validation.</summary>
public static class FoodRules
{
    /// <summary>Reads the existing clock, with village time retained for manually assembled worlds.</summary>
    /// <param name="world">The food world.</param>
    /// <returns>The current game minute, or zero before a manual world has a clock.</returns>
    public static GameMinute Now(World world)
    {
        foreach (Entity entity in world.GetEntitiesWith<FoodClockComponent>())
            if (world.TryGet<FoodClockComponent>(entity, out var time) && time is not null) return time.Clock.CurrentMinute;
        foreach (Entity entity in world.GetEntitiesWith<VillageComponent>())
            if (world.TryGet<VillageComponent>(entity, out var village) && village is not null) return village.CurrentMinute;
        return GameMinute.Zero;
    }

    /// <summary>Checks the absolute deadline even before the expiration system has marked the item.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="item">The proposed inventory or ingredient item.</param>
    /// <returns>True for spoiled food or a deadline already reached.</returns>
    public static bool IsExpired(World world, Entity item) => world.Has<SpoiledFoodComponent>(item) ||
        (world.TryGet<FoodExpirationComponent>(item, out var expiration) && Now(world).Value >= expiration.ExpiresAt.Value);

    /// <summary>Attaches a fresh lifetime once; repeated metadata application never refreshes an existing batch.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="item">The newly created food.</param>
    /// <param name="minutes">Positive lifetime, or null for nonperishable and legacy items.</param>
    /// <param name="createdAt">Optional exact completion time from a timed system.</param>
    public static void Start(World world, Entity item, int? minutes, GameMinute? createdAt = null)
    {
        if (minutes is null || world.Has<FoodExpirationComponent>(item) || world.Has<SpoiledFoodComponent>(item)) return;
        if (minutes <= 0) throw new ArgumentOutOfRangeException(nameof(minutes));
        GameMinute now = createdAt ?? Now(world);
        world.Add(item, new FoodExpirationComponent(now, now.Add(minutes.Value)));
    }

    /// <summary>Copies a batch deadline when splitting or chopping, without making old food fresh again.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="source">The original food.</param>
    /// <param name="target">The split stack or unused fish meat.</param>
    public static void Copy(World world, Entity source, Entity target)
    {
        if (world.TryGet<FoodExpirationComponent>(source, out var expiration)) world.Add(target, expiration);
        if (world.TryGet<SpoiledFoodComponent>(source, out var spoiled)) Spoil(world, target, spoiled.ExpiredAt);
    }

    /// <summary>Marks food spoiled and removes sale/use markers, preserving its name, quantity, and inventory entry.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="item">The expired item.</param>
    /// <param name="deadline">Its original expiration minute.</param>
    public static void Spoil(World world, Entity item, GameMinute deadline)
    {
        world.Add(item, new SpoiledFoodComponent(deadline));
        world.Remove<FoodExpirationComponent>(item);
        world.Remove<ConsumableItemComponent>(item);
        world.Remove<TradePriceComponent>(item);
        world.Remove<SaleLocationComponent>(item);
        world.Remove<AutomaticMarketPriceComponent>(item);
    }

    /// <summary>Supplies a stable oldest-first ordering for equally preferred carried food.</summary>
    /// <param name="world">The food world.</param>
    /// <param name="item">The carried item.</param>
    /// <returns>The deadline, or the largest value for items without expiration.</returns>
    public static long Deadline(World world, Entity item) => world.TryGet<FoodExpirationComponent>(item, out var expiration)
        ? expiration.ExpiresAt.Value : long.MaxValue;

    /// <summary>Counts usable preserved meals owned by an NPC, including its own sale stock.</summary>
    /// <param name="world">The NPC world.</param>
    /// <param name="npc">The inventory owner.</param>
    /// <returns>The number of fresh preserved servings.</returns>
    public static int PreservedCount(World world, Entity npc) => world.TryGet<InventoryComponent>(npc, out var bag) && bag is not null
        ? bag.Items.Count(item => world.TryGet<MealComponent>(item, out var meal) && meal.IsPreserved && AISurvivalRules.CanUse(world, item, false)) : 0;
}
