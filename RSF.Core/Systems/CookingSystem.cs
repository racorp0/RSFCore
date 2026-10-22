using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Consumes validated recipe ingredients at start and creates individually rolled meals after elapsed cooking time.</summary>
public sealed class CookingSystem : ISystem
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly TravelSystem _travel;
    private readonly Random _random;
    // Preserve world order so the shared random generator assigns the same rolls to each cook.
    private List<Entity> _cooks = new();
    private readonly HashSet<EntityId> _cookIds = new();
    /// <summary>Connects cooking to existing inventory, time, and movement.</summary>
    /// <param name="world">The kitchen world.</param>
    /// <param name="clock">The shared clock.</param>
    /// <param name="travel">The system stopping movement during cooking.</param>
    /// <param name="random">Optional repeatable random generator for quality checks.</param>
    public CookingSystem(World world, WorldClock clock, TravelSystem travel, Random? random = null)
    { _world = world; _clock = clock; _travel = travel; _random = random ?? new Random(); }

    /// <summary>Validates the complete recipe before chopping selected fish, spending exact meat grams, and starting timed cooking.</summary>
    /// <param name="cook">The living actor meeting this recipe's optional skill and tool requirements.</param>
    /// <param name="recipeIndex">The recipe index in the cook's settings.</param>
    /// <returns>True only after a timed batch has started and its ingredients were consumed.</returns>
    /// <param name="station">Optional explicit station; omitted resolves an accessible kitchen or public station.</param>
    public bool Start(Entity cook, int recipeIndex, Entity? station = null)
    {
        if (!_world.TryGet<CookingSettingsComponent>(cook, out var settings) || settings is null ||
            recipeIndex < 0 || recipeIndex >= settings.Recipes.Count) return false;
        settings.Validate();
        Recipe recipe = settings.Recipes[recipeIndex];
        station ??= CookingRules.FindStation(_world, cook, recipe);
        if (station is null || !CookingRules.CanStart(_world, cook, recipe, station: station) ||
            !ProcessingRules.Plan(_world, cook, recipe, out var spending)) return false;
        // Reserve the full combination first. Failed alternatives never chop fish or spend another ingredient.
        ProcessingRules.Spend(_world, cook, spending);
        _world.Remove<OperatingProcessComponent>(cook);
        _travel.Stop(cook);
        _world.Add(cook, new CookingAttemptComponent(recipe, _clock.CurrentMinute) { StationId = station.Id });
        return true;
    }

    /// <summary>Cancels a batch without refunding already consumed ingredients.</summary>
    /// <param name="cook">The cook stopping work.</param>
    public void Cancel(Entity cook) { _world.Remove<CookingAttemptComponent>(cook); }

    /// <summary>Completes attended batches with fresh output deadlines and preservation metadata, retaining world-order quality rolls.</summary>
    /// <param name="deltaTime">The simulation step.</param>
    public void Update(float deltaTime)
    {
        // The stable cook list remains safe when Cancel removes attempts and completed batches create meal entities.
        foreach (Entity cook in GetCooks())
        {
            if (!_world.TryGet<CookingAttemptComponent>(cook, out var batch) || batch is null) continue;
            Entity? station = null;
            if (!batch.StationId.IsEmpty && !_world.TryGetEntity(batch.StationId, out station)) { Cancel(cook); continue; }
            if (!CookingRules.KitchenAccess(_world, cook, batch.Recipe, station: station)) { Cancel(cook); continue; }
            if (_clock.CurrentMinute.ElapsedSince(batch.StartedAt).Minutes < batch.Recipe.ProcessingMinutes) continue;
            Cancel(cook);
            if (!_world.TryGet<CookingSettingsComponent>(cook, out var settings) || settings is null ||
                !_world.TryGet<InventoryComponent>(cook, out var bag) || bag is null) continue;
            settings.Validate();
            // As with processing, each output group selects at most one result; groups roll independently.
            foreach (var group in batch.Recipe.Outputs)
            {
                int chance = _random.Next(100), cumulative = 0;
                foreach (var output in group.Options)
                {
                    cumulative += output.ChancePercent;
                    if (chance >= cumulative) continue;
                    int count = _random.Next(output.MinimumQuantity, output.MaximumQuantity + 1);
                    for (int serving = 0; serving < count; serving++)
                    {
                        // Keep the existing small quality roll per meal. Skill-dependent taste and XP are later phases.
                        int roll = _random.Next(100);
                        string quality = roll < settings.PoorChancePercent ? "Poor" : roll >= 100 - settings.GoodChancePercent ? "Good" : "Normal";
                        double multiplier = quality == "Poor" ? settings.PoorMultiplier : quality == "Good" ? settings.GoodMultiplier : 1;
                        Entity meal = _world.CreateEntity();
                        _world.Add(meal, new ItemComponent(output.Name));
                        _world.Add(meal, new MealComponent(batch.Recipe.Name, quality) { ItemId = output.ItemId, IsPreserved = batch.Recipe.PreservesFood });
                        _world.Add(meal, new ConsumableItemComponent());
                        _world.Add(meal, new RestoreHungerEffectComponent(Math.Max(1, (int)Math.Round(output.Hunger * multiplier))));
                        if (output.Health > 0) _world.Add(meal, new RestoreHealthEffectComponent((int)Math.Round(output.Health * multiplier)));
                        int? lifetime = output.ShelfLifeMinutes ?? RSF.Core.Configuration.GameSettings.Find(_world)?.Food.OrdinaryMealShelfLifeMinutes;
                        FoodRules.Start(_world, meal, lifetime, _clock.CurrentMinute);
                        bag.Items.Add(meal);
                        Console.WriteLine($"Cook {cook.Id} made {output.Name} ({quality}).");
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Finds active batches through the component index, rebuilding world order only when cooks change.</summary>
    /// <returns>A stable list of cooks that preserves the existing order of random quality rolls.</returns>
    private List<Entity> GetCooks()
    {
        List<Entity> current = _world.GetEntitiesWith<CookingAttemptComponent>().ToList();
        if (current.Count != _cookIds.Count || current.Any(cook => !_cookIds.Contains(cook.Id)))
        {
            _cookIds.Clear();
            foreach (Entity cook in current) _cookIds.Add(cook.Id);
            // Attempt attachment order can differ from world order, changing which cook receives each random roll.
            _cooks = current.Count == 0 ? current
                : _world.GetEntities().Where(entity => _cookIds.Contains(entity.Id)).ToList();
        }
        return _cooks;
    }
}
