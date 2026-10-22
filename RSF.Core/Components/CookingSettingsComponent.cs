using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Configures recipes, meal stock, prices, and random quality effects.</summary>
public sealed record CookingSettingsComponent : IComponent
{
    public IReadOnlyList<Recipe> Recipes { get; init; } = Array.Empty<Recipe>();
    /// <summary>The station type attached to the existing inn kitchen.</summary>
    public string KitchenStationType { get; init; } = "kitchen";
    /// <summary>The shared tool ID added to the cook's existing utensils item.</summary>
    public string UtensilToolId { get; init; } = "cooking_utensils";
    public int MealStockTarget { get; init; } = 6;
    public int MealPrice { get; init; } = 8;
    public int SpiceStockTarget { get; init; } = 10;
    public int PoorChancePercent { get; init; } = 25;
    public int GoodChancePercent { get; init; } = 25;
    public double PoorMultiplier { get; init; } = 0.75;
    public double GoodMultiplier { get; init; } = 1.25;
    public double CookingPoints { get; init; } = 250;
    /// <summary>Rejects invalid item/gram-based cooking input/output groups, station requirements, food lifetimes, effects, and quality settings.</summary>
    public void Validate()
    {
        if (Recipes is null || Recipes.Count == 0 || string.IsNullOrWhiteSpace(KitchenStationType) || string.IsNullOrWhiteSpace(UtensilToolId) || MealStockTarget <= 0 || MealPrice <= 0 || SpiceStockTarget < 0 ||
            PoorChancePercent < 0 || GoodChancePercent < 0 || (long)PoorChancePercent + GoodChancePercent > 100 ||
            !double.IsFinite(PoorMultiplier) || PoorMultiplier <= 0 || PoorMultiplier > 10 ||
            !double.IsFinite(GoodMultiplier) || GoodMultiplier <= 0 || GoodMultiplier > 10 ||
            !double.IsFinite(CookingPoints) || CookingPoints < 0)
            throw new ArgumentException("Invalid cooking stock, quality, or scoring settings.");
        HashSet<string> recipeIds = new();
        foreach (Recipe recipe in Recipes)
        {
            if (recipe is null || string.IsNullOrWhiteSpace(recipe.Id) || !recipeIds.Add(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Name) ||
                recipe.Inputs is null || recipe.Inputs.Count == 0 || recipe.Inputs.Count > 32 || recipe.Outputs is null || recipe.Outputs.Count == 0 ||
                recipe.ProcessingMinutes <= 0 || !recipe.RequiresOperator || string.IsNullOrWhiteSpace(recipe.ProcessingStation))
                throw new ArgumentException("Cooking recipes need unique ids, inputs, outputs, positive processingMinutes, and a manual cooking station.");
            foreach (var group in recipe.Inputs)
            {
                if (group is null || group.Options is null || group.Options.Count == 0) throw new ArgumentException("Cooking input groups need alternatives.");
                foreach (var option in group.Options)
                    if (option is null || option.Quantity <= 0 || option.Kind is not (ProcessingItemKind.Item or ProcessingItemKind.Spice) ||
                        (option.Kind == ProcessingItemKind.Item ? string.IsNullOrWhiteSpace(option.ItemId) : !string.IsNullOrEmpty(option.ItemId)) || !string.IsNullOrEmpty(option.PlantId))
                        throw new ArgumentException("Cooking inputs support Item IDs (including meat grams) and Spice with positive quantities.");
            }
            if (recipe.RequiredSkill is not null && (string.IsNullOrWhiteSpace(recipe.RequiredSkill.Id) || recipe.RequiredSkill.MinimumLevel <= 0))
                throw new ArgumentException("Cooking requiredSkill needs a valid id and positive minimumLevel.");
            foreach (var group in recipe.Outputs)
            {
                if (group is null || group.Options is null || group.Options.Count == 0) throw new ArgumentException("Cooking output groups need options.");
                long chance = 0;
                foreach (var output in group.Options)
                {
                    if (output is null || output.Kind != ProcessingItemKind.Meal || string.IsNullOrWhiteSpace(output.ItemId) || !string.IsNullOrEmpty(output.PlantId) ||
                        string.IsNullOrWhiteSpace(output.Name) || output.ChancePercent < 0 || output.ChancePercent > 100 || output.MinimumQuantity <= 0 ||
                        output.MaximumQuantity < output.MinimumQuantity || output.MaximumQuantity > 100 || output.Hunger <= 0 || output.Hunger > 1000 || output.Health < 0 || output.Health > 1000)
                        throw new ArgumentException("Cooking Meal outputs need names, ids, valid chances, 1-100 quantity ranges, and bounded hunger/health effects.");
                    if ((output.ShelfLifeMinutes is not null && output.ShelfLifeMinutes <= 0) ||
                        (!recipe.PreservesFood && output.ShelfLifeMinutes > 12 * GameDate.MinutesPerHour) ||
                        (recipe.PreservesFood && output.ShelfLifeMinutes is null))
                        throw new ArgumentException("Ordinary meals cannot last over 720 minutes; preserved outputs need an explicit positive lifetime.");
                    chance += output.ChancePercent;
                }
                if (chance > 100) throw new ArgumentException("Cooking chances in one output group cannot exceed 100 percent.");
            }
        }
    }
}
