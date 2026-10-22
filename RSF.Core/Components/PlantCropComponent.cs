using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores a planted crop's progress and remaining harvests on an existing farm plot.</summary>
/// <param name="Definition">The immutable settings copied by reference when planted.</param>
/// <param name="GrowthDays">Completed watered growth intervals.</param>
/// <param name="RequiredGrowthDays">The initial or regrowth target for this cycle.</param>
/// <param name="LastGrowthAt">The last elapsed growth boundary.</param>
/// <param name="WasWatered">Whether this cycle's next interval has been watered.</param>
/// <param name="RemainingHarvests">The randomized harvest budget, reduced only on successful harvest.</param>
/// <param name="HarvestCount">Successful harvests already performed.</param>
/// <param name="IsDry">Whether the plant has exhausted its harvest budget and needs clearing.</param>
public readonly record struct PlantCropComponent(PlantDefinition Definition, int GrowthDays, int RequiredGrowthDays,
    GameMinute LastGrowthAt, bool WasWatered, int RemainingHarvests, int HarvestCount = 0, bool IsDry = false) : IComponent
{
    /// <summary>Whether produce can be harvested without spending another seed.</summary>
    public bool IsMature => !IsDry && GrowthDays >= RequiredGrowthDays;
}
