using RSF.Core.Components;
using RSF.Core.Configuration;
using RSF.Core.Entities;
using RSF.Core.Simulation;

namespace RSF.Core.Systems;

/// <summary>Calculates whole-fish sale offers for RSF's existing trade commands.</summary>
public static class FishPricing
{
    /// <summary>Uses the species rate and actual weight, rounding upward to whole gold. Invalid or unknown configured fish cannot be offered.</summary>
    /// <param name="world">The world containing the fish and optional loaded settings.</param>
    /// <param name="item">The whole fish to price.</param>
    /// <param name="fallbackPricePer500Grams">The rate used only in manual worlds without loaded settings.</param>
    /// <returns>The whole-fish gold price, or zero when no valid price exists.</returns>
    public static int SalePrice(World world, Entity item, int fallbackPricePer500Grams)
    {
        double price = Math.Ceiling(UnroundedSalePrice(world, item, fallbackPricePer500Grams));
        return price <= 0 || price > int.MaxValue ? 0 : (int)price;
    }

    /// <summary>Preserves fractional weight pricing so a local multiplier can be applied before rounding once.</summary>
    /// <param name="world">The fish world.</param>
    /// <param name="item">The existing whole fish.</param>
    /// <param name="fallbackPricePer500Grams">The legacy manual-world rate.</param>
    /// <returns>Exact base gold for this weight, or zero for expired or invalid fish and rates.</returns>
    public static double UnroundedSalePrice(World world, Entity item, int fallbackPricePer500Grams)
    {
        if (FoodRules.IsExpired(world, item) || !world.TryGet<FishComponent>(item, out var fish) || fish.WeightGrams <= 0) return 0;
        var settings = GameSettings.Find(world);
        int rate = settings is null ? fallbackPricePer500Grams
            : settings.Fishing.FishTypes.FirstOrDefault(definition =>
                string.Equals(definition.Species, fish.Species, StringComparison.OrdinalIgnoreCase))?.PricePer500Grams ?? 0;
        // Cast before multiplying so large weights cannot overflow integer arithmetic.
        return rate <= 0 ? 0 : (double)fish.WeightGrams * rate / 500;
    }
}
