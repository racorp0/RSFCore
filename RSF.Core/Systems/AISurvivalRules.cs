using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Shares survival-item eligibility checks between AI selection and execution.</summary>
internal static class AISurvivalRules
{
    /// <summary>Rejects expired, missing, incomplete, poisonous, or harmful servings before considering their benefit.</summary>
    /// <param name="world">The world containing the item.</param>
    /// <param name="item">The proposed healing or food item.</param>
    /// <param name="healing">True for instant healing; false for hunger restoration.</param>
    /// <returns>True when the item has a positive supported effect without known harmful effects.</returns>
    public static bool CanUse(World world, Entity item, bool healing)
    {
        if (FoodRules.IsExpired(world, item) || !world.TryGetEntity(item.Id, out var registered) || registered != item ||
            !world.Has<ItemComponent>(item) || !world.Has<ConsumableItemComponent>(item) ||
            world.Has<ApplyPoisonEffectComponent>(item))
        {
            return false;
        }

        if ((world.TryGet<RestoreHealthEffectComponent>(item, out var hp) && hp.Amount < 0) ||
            (world.TryGet<RestoreHungerEffectComponent>(item, out var food) && food.Amount < 0) ||
            (world.TryGet<RestoreManaEffectComponent>(item, out var mana) && mana.Amount < 0) ||
            (world.TryGet<ApplyRegenerationEffectComponent>(item, out var regen) && regen.HealPerTick < 0))
        {
            return false;
        }

        if (RawFoodRules.Servings(world, item) <= 0) return false;

        return healing
            ? world.TryGet<RestoreHealthEffectComponent>(item, out var healthEffect) && healthEffect.Amount > 0
            : world.TryGet<RestoreHungerEffectComponent>(item, out var hungerEffect) && hungerEffect.Amount > 0;
    }
}
