using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
namespace RSF.Core.Systems;

/// <summary>Shares processing requirement checks and non-consuming input planning between AI and command execution.</summary>
public static class ProcessingRules
{
    /// <summary>Checks the actor's reusable tool and activation skill requirements.</summary>
    /// <param name="world">The actor's world.</param>
    /// <param name="npc">The actor to validate.</param>
    /// <param name="recipe">The shared processing recipe.</param>
    /// <returns>True if the actor is alive, has the tools, and meets the optional skill requirement.</returns>
    public static bool Requirements(World world, Entity npc, ProcessingDefinition recipe)
    {
        if (!world.TryGetEntity(npc.Id, out var registered) || registered != npc || world.Has<DeadComponent>(npc) ||
            !world.TryGet<HealthComponent>(npc, out var health) || health.Current <= 0 ||
            !world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) return false;
        if (world.TryGet<LifeStageComponent>(npc, out var life) && (life.Stage == LifeStage.Baby || life.Stage == LifeStage.Toddler)) return false;
        if (recipe.Tools is not null && recipe.Tools.Any(id => !bag.Items.Any(item => !world.Has<SaleLocationComponent>(item) &&
            world.TryGet<ProcessingToolComponent>(item, out var tool) && tool.ToolId == id))) return false;
        if (recipe.RequiredSkill is null) return true;
        string skill = recipe.RequiredSkill.Id;
        int level = 0;
        if (world.TryGet<ProcessingSkillsComponent>(npc, out var skills) && skills is not null) skills.Levels.TryGetValue(skill, out level);
        if (skill == "cooking" && world.TryGet<CookingSkillComponent>(npc, out var cooking)) level = Math.Max(level, cooking.Level);
        if (skill == "fishing" && world.TryGet<FishingSkillComponent>(npc, out var fishing)) level = Math.Max(level, fishing.Level);
        return level >= recipe.RequiredSkill.MinimumLevel;
    }

    /// <summary>Checks a machinery type and real interaction distance; station-free recipes must be hosted by the actor.</summary>
    /// <param name="world">The world containing both entities.</param>
    /// <param name="npc">The actor.</param>
    /// <param name="host">The proposed machinery or actor entity.</param>
    /// <param name="recipe">The processing recipe.</param>
    /// <param name="requireNear">Whether physical range is required now, rather than after planned travel.</param>
    /// <returns>True if this host can perform the recipe.</returns>
    public static bool Host(World world, Entity npc, Entity host, ProcessingDefinition recipe, bool requireNear = true)
    {
        if (!world.TryGetEntity(host.Id, out var registered) || registered != host) return false;
        if (recipe.ProcessingStation is null) return recipe.RequiresOperator && host == npc;
        if (!world.TryGet<ProcessingStationComponent>(host, out var station) || station.StationType != recipe.ProcessingStation) return false;
        if (world.TryGet<OwnershipComponent>(host, out var owner) && owner is not null && owner.OwnerId != npc.Id) return false;
        return !requireNear || (world.TryGet<PositionComponent>(npc, out var actorPosition) && world.TryGet<PositionComponent>(host, out var position) &&
            Math.Abs(actorPosition.X - position.X) + Math.Abs(actorPosition.Y - position.Y) <= 1);
    }

    /// <summary>Finds the first complete combination in JSON order, reserving fresh typed inputs and meat grams across groups while ignoring expired food.</summary>
    /// <param name="world">The inventory world.</param>
    /// <param name="npc">The input owner.</param>
    /// <param name="recipe">The input groups to satisfy.</param>
    /// <param name="spending">The exact inventory entity quantities to spend only after all validation passes.</param>
    /// <returns>True if the whole recipe can be supplied without mixing partial alternatives.</returns>
    /// <param name="includeListed">AI planning only: include owned listed inputs so the actor can plan retrieval before cooking.</param>
    public static bool Plan(World world, Entity npc, ProcessingDefinition recipe, out Dictionary<Entity, int> spending, bool includeListed = false)
    {
        spending = new();
        if (!world.TryGet<InventoryComponent>(npc, out var bag) || bag is null) return false;
        var available = new Dictionary<string, long>();
        foreach (var item in bag.Items)
        {
            if (FoodRules.IsExpired(world, item) || (!includeListed && world.Has<SaleLocationComponent>(item)) || world.Has<ProcessingToolComponent>(item)) continue;
            string? key = Key(world, item);
            if (key is null) continue;
            available.TryGetValue(key, out long count); available[key] = count + Units(world, item);
        }
        Dictionary<string, long> chosen = new();
        HashSet<string> failed = new();
        if (!Choose(0)) return false;
        // Spend prepared meat first so an existing remainder does not trigger another whole-fish chop.
        foreach (var item in bag.Items.OrderBy(item => world.Has<FishComponent>(item)))
        {
            if (FoodRules.IsExpired(world, item) || (!includeListed && world.Has<SaleLocationComponent>(item)) || world.Has<ProcessingToolComponent>(item)) continue;
            string? key = Key(world, item);
            if (key is null || !chosen.TryGetValue(key, out long remaining) || remaining <= 0) continue;
            int amount = (int)Math.Min(remaining, Units(world, item));
            if (amount > 0) spending[item] = amount;
            chosen[key] = remaining - amount;
        }
        return true;

        /// <summary>Backtracks when an early option would prevent a later required group from being supplied.</summary>
        /// <param name="group">The next required input group.</param>
        /// <returns>True if all remaining groups can be supplied.</returns>
        bool Choose(int group)
        {
            if (group == recipe.Inputs.Count) return true;
            // Memoize failed remaining-stock states so overlapping alternatives do not repeat the same search.
            string state = group + ":" + string.Join(";", available.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value));
            if (failed.Contains(state)) return false;
            foreach (var option in recipe.Inputs[group].Options)
            {
                string key = option.Kind + ":" + (option.Kind == ProcessingItemKind.Seed ? option.PlantId : option.ItemId);
                if (!available.TryGetValue(key, out long count) || count < option.Quantity) continue;
                available[key] -= option.Quantity;
                chosen.TryGetValue(key, out long reserved); chosen[key] = reserved + option.Quantity;
                if (Choose(group + 1)) return true;
                available[key] += option.Quantity; chosen[key] = reserved;
            }
            failed.Add(state); return false;
        }
    }

    /// <summary>Consumes a validated reservation, chopping only selected whole fish and storing unused meat grams without extending their freshness.</summary>
    /// <param name="world">The inventory world.</param>
    /// <param name="npc">The input owner.</param>
    /// <param name="spending">The complete plan validated before mutation.</param>
    public static void Spend(World world, Entity npc, Dictionary<Entity, int> spending)
    {
        world.TryGet<InventoryComponent>(npc, out var bag);
        foreach (var entry in spending)
        {
            if (world.TryGet<FishComponent>(entry.Key, out var fish))
            {
                // Apply chopping loss once to this whole fish, then preserve all unspent meat.
                int remaining = FishMeat.Grams(world, entry.Key) - entry.Value;
                if (remaining > 0)
                {
                    var definition = FishMeat.Definition(world, fish.Species)!;
                    var meat = world.CreateEntity();
                    world.Add(meat, new ItemComponent(definition.MeatName));
                    world.Add(meat, new PlantProduceComponent(definition.MeatItemId, remaining));
                    FoodRules.Start(world, meat, RSF.Core.Configuration.GameSettings.Find(world)?.Food.FreshMeatShelfLifeMinutes);
                    if (world.TryGet<FoodExpirationComponent>(entry.Key, out var original) &&
                        (!world.TryGet<FoodExpirationComponent>(meat, out var newExpiry) || original.ExpiresAt.Value < newExpiry.ExpiresAt.Value))
                        FoodRules.Copy(world, entry.Key, meat);
                    bag!.Items.Add(meat);
                }
            }
            else
            {
                int remaining = InventoryStacks.Count(world, entry.Key) - entry.Value;
                if (remaining > 0) { InventoryStacks.Set(world, entry.Key, remaining); continue; }
            }
            bag!.Items.Remove(entry.Key); world.DestroyEntity(entry.Key.Id);
        }
    }

    /// <summary>Counts usable meat grams for whole fish and normal stack units for prepared ingredients.</summary>
    /// <param name="world">The inventory world.</param>
    /// <param name="item">The input entity.</param>
    /// <returns>The number of available recipe units; meat units are grams.</returns>
    private static int Units(World world, Entity item) => world.Has<FishComponent>(item)
        ? FishMeat.Grams(world, item) : InventoryStacks.Count(world, item);

    /// <summary>Reads a typed identity so seed and item stacks never substitute for each other accidentally.</summary>
    /// <param name="world">The item's world.</param>
    /// <param name="item">The inventory entity.</param>
    /// <returns>A typed ID, or null for unsupported input objects.</returns>
    private static string? Key(World world, Entity item)
    {
        if (world.TryGet<FishComponent>(item, out var fish))
        {
            var definition = FishMeat.Definition(world, fish.Species);
            return definition is null ? null : "Item:" + definition.MeatItemId;
        }
        if (world.Has<SpiceComponent>(item)) return "Spice:";
        if (world.TryGet<PlantProduceComponent>(item, out var product)) return "Item:" + product.ItemId;
        if (world.TryGet<PlantSeedComponent>(item, out var seed)) return "Seed:" + seed.PlantId;
        return null;
    }
}
