using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Applies JSON raw-food effects to ordinary product stacks without changing their item identity.</summary>
public static class RawFoodRules
{
    /// <summary>Attaches configured food lifetime and optional raw-food effects when products are created.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The registered product stack.</param>
    public static void Apply(World world, Entity item)
    {
        if (FoodRules.IsExpired(world, item) || !world.TryGet<PlantProduceComponent>(item, out var produce) || GameSettings.Find(world) is not GameSettings settings ||
            !settings.Products.TryGetValue(produce.ItemId, out var definition)) return;
        FoodRules.Start(world, item, definition.ShelfLifeMinutes ?? (definition.RawFood is not null ? settings.Food.RawProduceShelfLifeMinutes : null));
        if (definition.RawFood is null) return;
        var food = definition.RawFood;
        world.Add(item, new FoodServingComponent(food.QuantityPerServing));
        world.Add(item, new ConsumableItemComponent());
        world.Add(item, new RestoreHungerEffectComponent(food.Hunger));
        if (food.Health > 0) world.Add(item, new RestoreHealthEffectComponent(food.Health));
    }

    /// <summary>Reads how many stack units one use or food purchase needs.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The proposed food.</param>
    /// <returns>Serving units for raw food, or one for ordinary individual meals.</returns>
    public static int Units(World world, Entity item) => world.TryGet<FoodServingComponent>(item, out var serving) ? serving.Quantity : 1;

    /// <summary>Counts complete servings rather than treating an entire tomato stack as one meal.</summary>
    /// <param name="world">The item world.</param>
    /// <param name="item">The food inventory entity.</param>
    /// <returns>The number of usable servings represented by this item.</returns>
    public static int Servings(World world, Entity item)
    {
        int units = Units(world, item);
        return units <= 0 ? 0 : InventoryStacks.Count(world, item) / units;
    }
}
