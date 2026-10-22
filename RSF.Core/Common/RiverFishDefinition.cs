namespace RSF.Core.Common;

/// <summary>Describes one possible river catch; these are game settings rather than real fish data.</summary>
/// <param name="PricePer500Grams">The positive gold rate per 500 grams; whole-fish prices round up to whole gold.</param>
/// <param name="MinimumSkillLevel">The lowest fishing skill allowed to catch this species.</param>
/// <param name="Species">The fish name.</param>
/// <param name="CatchChancePercent">The percentage chance per completed fishing attempt.</param>
/// <param name="MinWeightGrams">The smallest possible weight in grams.</param>
/// <param name="MaxWeightGrams">The largest possible weight in grams.</param>
public sealed record RiverFishDefinition(
    string Species,
    int CatchChancePercent,
    int MinWeightGrams,
    int MaxWeightGrams,
    int MinimumSkillLevel = 1,
    int PricePer500Grams = 10)
{
    /// <summary>The ordinary item ID produced when this species is chopped.</summary>
    public string MeatItemId { get; init; } = "";
    /// <summary>The inventory display name of this species meat.</summary>
    public string MeatName { get; init; } = "";
    /// <summary>Percentage retained as usable meat; 80 means a 20-percent chopping loss.</summary>
    public int MeatYieldPercent { get; init; } = 80;
}
