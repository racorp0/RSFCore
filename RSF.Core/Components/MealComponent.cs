namespace RSF.Core.Components;

/// <summary>Records a meal's recipe, output identity, and existing quality; effect components hold its actual restoration values.</summary>
/// <param name="RecipeName">The shared recipe name.</param>
/// <param name="Quality">The quality of this individual meal.</param>
public readonly record struct MealComponent(string RecipeName, string Quality) : IComponent
{
    /// <summary>True for intentionally preserved recipe output rather than ordinary cooked meals.</summary>
    public bool IsPreserved { get; init; }
    /// <summary>The stable output ID selected from the cooking recipe, separate from its display name.</summary>
    public string ItemId { get; init; } = "";
}
