using System.Text.Json;
using System.Text.Json.Serialization;
using RSF.Core.Common;
using RSF.Core.Components;
namespace RSF.Core.Configuration;

/// <summary>Reads commented JSON once at startup and reports invalid settings with their source filename.</summary>
public static class GameSettingsLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    /// <summary>Loads and validates gameplay, local market references, and diagnostic history limits before simulation setup.</summary>
    /// <param name="directory">The directory containing the JSON files.</param>
    /// <returns>Validated settings belonging to one simulation.</returns>
    public static GameSettings Load(string directory)
    {
        GameSettings settings = new();
        MapSettings map = Read<MapSettings>("map.json", layout => layout.Validate());
        settings.Map = map;
        settings.Food = Read<FoodSettings>("food.json", food => food.Validate());
        settings.Farming = Read<FarmingSettingsComponent>("farming.json", f =>
        {
            Require(f.Plants is not null && f.Plants.Count > 0, "plants must not be empty");
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (var plant in f.Plants!)
            {
                Require(plant is not null && ValidId(plant.Id) && !string.IsNullOrWhiteSpace(plant.Name) && !string.IsNullOrWhiteSpace(plant.Type), "each plant needs a valid id, name, and type");
                Require(ids.Add(plant!.Id), $"duplicate plant id: {plant.Id}");
                Require(Enum.IsDefined(plant.HarvestMode), $"{plant.Id}: invalid harvestMode");
                Require(plant.GrowthDays > 0 && plant.RegrowthDays > 0 && plant.GrowthIntervalMinutes > 0 && plant.WateringIntervalMinutes > 0, $"{plant.Id}: growth and watering intervals must be positive");
                Require(plant.MinimumHarvests > 0 && plant.MaximumHarvests >= plant.MinimumHarvests && plant.MaximumHarvests < int.MaxValue, $"{plant.Id}: invalid harvest range");
                Require(plant.HarvestMode != PlantHarvestMode.Single || (plant.MinimumHarvests == 1 && plant.MaximumHarvests == 1), $"{plant.Id}: Single requires exactly one harvest");
                Require(plant.SeedsPerPlanting > 0 && plant.StartingSeedsPerFarmer >= 0 && plant.MerchantSeeds >= 0 && plant.SeedStockTarget >= 0 && plant.PreferredPlotsPerFarmer >= 0 && plant.SeedPrice > 0, $"{plant.Id}: invalid seed stock, cost, or plot preference");
                RegisterOutputs(plant.HarvestOutputs);
            }
            Require(f.MinimumWorms >= 0 && f.MaximumWorms >= f.MinimumWorms && f.MaximumWorms < int.MaxValue, "invalid worm reward range");
        });
        // Species meat IDs must be indexed before processing and cooking validate their item inputs.
        settings.Fishing = Read<RiverComponent>("fishing.json", f =>
        {
            Require(f.MinimumCatchMinutes > 0 && f.MaximumCatchMinutes >= f.MinimumCatchMinutes && f.MaximumCatchMinutes < int.MaxValue, "invalid catch minute range");
            Require(f.FishTypes is not null && f.FishTypes.Count > 0, "fishTypes cannot be empty");
            HashSet<string> meatIds = new(StringComparer.OrdinalIgnoreCase);
            long chance = 0;
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (var fish in f.FishTypes!)
            {
                Require(fish is not null && !string.IsNullOrWhiteSpace(fish.Species), "each fish needs a species");
                Require(names.Add(fish!.Species), $"duplicate species {fish.Species}");
                Require(ValidId(fish.MeatItemId) && !string.IsNullOrWhiteSpace(fish.MeatName) && meatIds.Add(fish.MeatItemId), $"{fish.Species}: meatItemId and meatName must be valid and meat IDs unique");
                Require(fish.MeatYieldPercent >= 1 && fish.MeatYieldPercent <= 100, $"{fish.Species}: meatYieldPercent must be between 1 and 100");
                Require(!settings.Products.ContainsKey(fish.MeatItemId), $"{fish.Species}: meat item conflicts with another product");
                RegisterOutputs(new[] { new HarvestOutputDefinition { ItemId = fish.MeatItemId, Name = fish.MeatName, MerchantStockTarget = 0 } });
                Require(fish.MinWeightGrams > 0 && fish.MaxWeightGrams >= fish.MinWeightGrams && fish.MaxWeightGrams < int.MaxValue, $"{fish.Species}: invalid weight range");
                // Reject prices whose largest configured fish would exceed the gold component limit.
                Require(fish.PricePer500Grams > 0 && ((long)fish.MaxWeightGrams * fish.PricePer500Grams + 499) / 500 <= int.MaxValue, $"{fish.Species}: pricePer500Grams must be positive and the maximum fish price must fit in whole gold");
                Require(fish.MinimumSkillLevel >= 1, $"{fish.Species}: minimumSkillLevel must be at least one");
                Require(fish.CatchChancePercent >= 0 && fish.CatchChancePercent <= 100, $"{fish.Species}: invalid catchChancePercent");
                chance += fish.CatchChancePercent;
            }
            Require(chance <= 100, "fish catch chances must total at most 100 percent");
            Require(f.Baits is not null && f.Baits.Count > 0, "baits must not be empty");
            HashSet<string> baitIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (var bait in f.Baits!)
            {
                Require(bait is not null && ValidId(bait.Id) && !string.IsNullOrWhiteSpace(bait.Name), "each bait needs a valid id and name");
                Require(baitIds.Add(bait!.Id), $"duplicate bait id {bait.Id}");
                Require(bait.Price > 0 && bait.MerchantStock >= 0, $"{bait.Id}: price must be positive and merchantStock cannot be negative");
                Require(bait.CatchChances is not null && bait.CatchChances.Count > 0, $"{bait.Id}: catchChances cannot be empty");
                long baitChance = 0;
                foreach (var entry in bait.CatchChances!)
                {
                    Require(f.FishTypes!.Any(fish => fish.Species == entry.Key), $"{bait.Id}: unknown species {entry.Key}");
                    Require(entry.Value >= 0 && entry.Value <= 100, $"{bait.Id}: catch chances must be between zero and 100");
                    baitChance += entry.Value;
                }
                Require(baitChance <= 100, $"{bait.Id}: catch chances must total at most 100 percent");
            }
            // Existing plowing rewards and fisher starting supplies are worms.
            Require(f.Baits!.Any(bait => bait.Id == "worm"), "baits must include worm for plowing and starting supplies");
            Require(f.Locations is not null, "locations cannot be null");
            Require(f.Locations!.Count > 0 || map.ResourceTileFree(4, 15), "default river position conflicts with map parcels");
            HashSet<string> locationIds = new(StringComparer.OrdinalIgnoreCase);
            HashSet<(int X, int Y)> fishingPositions = new();
            foreach (var location in f.Locations!)
            {
                Require(location is not null && ValidId(location.Id) && !string.IsNullOrWhiteSpace(location.Name), "each location needs a valid id and name");
                Require(locationIds.Add(location!.Id), $"duplicate location id {location.Id}");
                Require(location.WaterType is "river" or "lake" or "sea", $"{location.Id}: waterType must be river, lake, or sea");
                // Keep fishing spots on free interior road/water tiles, preserving farmland, homes, and workshops.
                Require(map.ResourceTileFree(location.X, location.Y), $"{location.Id}: position must be a free interior road or water tile outside parcels");
                Require(fishingPositions.Add((location.X, location.Y)), $"{location.Id}: duplicate fishing position");
                Require(location.MinimumCatchMinutes > 0 && location.MaximumCatchMinutes >= location.MinimumCatchMinutes && location.MaximumCatchMinutes < int.MaxValue, $"{location.Id}: invalid catch minute range");
                Require(location.Species is not null && location.Species.Count > 0 && location.Species.Distinct().Count() == location.Species.Count &&
                    location.Species.All(species => f.FishTypes!.Any(fish => fish.Species == species)), $"{location.Id}: species must be unique known fish names");
                Require(location.BaitCatchChances is not null && location.BaitCatchChances.Count > 0, $"{location.Id}: baitCatchChances cannot be empty");
                foreach (var table in location.BaitCatchChances!)
                {
                    Require(f.Baits.Any(bait => bait.Id == table.Key), $"{location.Id}: unknown bait {table.Key}");
                    Require(table.Value is not null && table.Value.Count > 0, $"{location.Id}/{table.Key}: catch table cannot be empty");
                    long localChance = 0;
                    foreach (var entry in table.Value!)
                    {
                        Require(location.Species!.Contains(entry.Key), $"{location.Id}/{table.Key}: species {entry.Key} is unavailable here");
                        Require(entry.Value >= 0 && entry.Value <= 100, $"{location.Id}/{table.Key}: invalid catch chance");
                        localChance += entry.Value;
                    }
                    Require(localChance <= 100, $"{location.Id}/{table.Key}: catch chances must total at most 100 percent");
                }
            }
        });
        settings.Processing = Read<ProcessingSettings>("processing.json", p =>
        {
            Require(p.Recipes is not null && p.Stations is not null && p.Tools is not null && p.Items is not null, "recipes, stations, tools, and items cannot be null");
            HashSet<string> ids = new();
            HashSet<(int X, int Y)> positions = new();
            foreach (var station in p.Stations!)
            {
                Require(station is not null && ValidId(station.Id) && ValidId(station.StationType) && !string.IsNullOrWhiteSpace(station.Name), "invalid station identity");
                Require(ids.Add(station!.Id) && positions.Add((station.X, station.Y)), $"duplicate station id or position {station.Id}");
                Require(map.ResourceTileFree(station.X, station.Y) && !settings.Fishing.Locations.Any(location => location.X == station.X && location.Y == station.Y), $"{station.Id}: station must occupy a free map tile outside parcels and fishing spots");
            }
            ids.Clear();
            foreach (var tool in p.Tools!)
                Require(tool is not null && ValidId(tool.Id) && !string.IsNullOrWhiteSpace(tool.Name) && ids.Add(tool.Id), "invalid or duplicate tool definition");
            if (p.Items!.Count > 0) RegisterOutputs(p.Items);
            ids.Clear();
            foreach (var recipe in p.Recipes!)
            {
                Require(recipe is not null && ValidId(recipe.Id) && !string.IsNullOrWhiteSpace(recipe.Name) && ids.Add(recipe.Id), "invalid or duplicate recipe id/name");
                Require(recipe!.ProcessingMinutes > 0 && (recipe.RequiresOperator || recipe.ProcessingStation is not null), $"{recipe.Id}: automatic jobs need a station and all jobs need positive processingMinutes");
                Require(recipe.ProcessingStation is null || p.Stations!.Any(x => x.StationType == recipe.ProcessingStation), $"{recipe.Id}: unknown processingStation");
                Require(recipe.Tools is null || (recipe.Tools.Distinct().Count() == recipe.Tools.Count && recipe.Tools.All(id => p.Tools!.Any(t => t.Id == id))), $"{recipe.Id}: unknown or repeated held tool");
                Require(recipe.RequiredSkill is null || (ValidId(recipe.RequiredSkill.Id) && recipe.RequiredSkill.MinimumLevel > 0), $"{recipe.Id}: invalid requiredSkill");
                Require(recipe.Inputs is not null && recipe.Inputs.Count > 0 && recipe.Inputs.Count <= 32 && recipe.Outputs is not null && recipe.Outputs.Count > 0, $"{recipe.Id}: inputs/outputs must be nonempty; at most 32 input groups");
                foreach (var group in recipe.Inputs!)
                {
                    Require(group is not null && group.Options is not null && group.Options.Count > 0, $"{recipe.Id}: each input needs options");
                    foreach (var option in group!.Options!)
                        Require(option is not null && Enum.IsDefined(option.Kind) && option.Quantity > 0 &&
                            (option.Kind == ProcessingItemKind.Seed ? settings.Farming.Plants.Any(x => x.Id == option.PlantId) && string.IsNullOrEmpty(option.ItemId)
                                : option.Kind == ProcessingItemKind.Item ? ValidId(option.ItemId) && string.IsNullOrEmpty(option.PlantId)
                                : option.Kind == ProcessingItemKind.Spice && string.IsNullOrEmpty(option.ItemId) && string.IsNullOrEmpty(option.PlantId)), $"{recipe.Id}: invalid input option");
                }
                foreach (var group in recipe.Outputs!)
                {
                    Require(group is not null && group.Options is not null && group.Options.Count > 0, $"{recipe.Id}: each output needs options");
                    long probability = 0;
                    foreach (var option in group!.Options!)
                    {
                        Require(option is not null && option.Kind is ProcessingItemKind.Item or ProcessingItemKind.Seed && option.ChancePercent >= 0 && option.ChancePercent <= 100 && option.MinimumQuantity > 0 && option.MaximumQuantity >= option.MinimumQuantity && option.MaximumQuantity < int.MaxValue, $"{recipe.Id}: invalid output chance/quantity");
                        probability += option!.ChancePercent;
                        if (option.Kind == ProcessingItemKind.Seed)
                            Require(settings.Farming.Plants.Any(x => x.Id == option.PlantId) && string.IsNullOrEmpty(option.ItemId), $"{recipe.Id}: unknown seed plant or ambiguous output ID");
                        else { Require(string.IsNullOrEmpty(option.PlantId), $"{recipe.Id}: Item output cannot also identify a seed plant"); RegisterOutputs(new[] { option }); }
                    }
                    Require(probability <= 100, $"{recipe.Id}: chances in one output group must total at most 100");
                }
            }
            // All outputs have been indexed, so inputs may reference outputs from later recipes too.
            foreach (var recipe in p.Recipes!)
                foreach (var option in recipe.Inputs.SelectMany(g => g.Options))
                    Require(option.Kind != ProcessingItemKind.Item || settings.Products.ContainsKey(option.ItemId), $"{recipe.Id}: unknown input item {option.ItemId}");
        });
        settings.Cooking = Read<CookingSettingsComponent>("recipes.json", ValidateCooking);
        settings.Needs = Read<SurvivalSettings>("needs.json", n =>
        {
            Require(n.HungerTickMinutes > 0 && n.HungerLossPerTick >= 0 && n.StarvationDamage >= 0 && n.BreadHunger > 0 && n.AppleHunger > 0 && n.MinimumSoupHunger > 0 && n.MaximumSoupHunger >= n.MinimumSoupHunger && n.MaximumSoupHunger < int.MaxValue, "invalid hunger rates, starvationDamage, or breadHunger");
            Require(n.Thresholds is not null, "thresholds cannot be null"); n.Thresholds!.Validate();
            ValidateSleep(n.Sleep);
        });
        settings.AI = Read<DecisionSettings>("ai.json", a =>
        {
            Require(a.Decision is not null && a.Work is not null, "decision and work cannot be null");
            a.Decision!.Validate(); a.Work!.Validate();
        });
        settings.Economy = Read<EconomySettings>("economy.json", e =>
        {
            Require(e.TransactionHistory is not null, "transactionHistory cannot be null");
            e.TransactionHistory!.Validate();
            Require(e.Market is not null, "market cannot be null");
            e.Market!.Validate();
            foreach (string key in e.Market.InitialPriceMultipliers.Keys)
            {
                // Reference existing definitions instead of maintaining a second commodity/base-price catalogue.
                string[] reference = key.Split(':', 2);
                string id = reference[1];
                bool known = reference[0] switch
                {
                    "category" => id is "food" or "bait" or "whole_fish" or "healing",
                    "item" => id is "bread" or "spice" || settings.Products.ContainsKey(id) ||
                        settings.Cooking.Recipes.SelectMany(recipe => recipe.Outputs).SelectMany(group => group.Options).Any(output => output.ItemId == id),
                    "seed" => settings.Farming.Plants.Any(plant => plant.Id == id),
                    "bait" => settings.Fishing.Baits.Any(bait => bait.Id == id),
                    "fish" => settings.Fishing.FishTypes.Any(fish => fish.Species == id),
                    _ => false
                };
                Require(known, $"unknown market commodity: {key}");
            }
            Require(e.InnBeds > 0 && e.MinutesPerTravelTile > 0 && e.RentPerDay > 0, "invalid beds, travel time, or rent");
            Require(e.MinMerchantDays >= 1 && e.MaxMerchantDays >= e.MinMerchantDays && e.MaxMerchantDays <= 3650, "invalid merchant arrival range");
            Require(e.MaxMerchants >= 0 && e.MerchantIdleMinutes > 0 && e.MaxVisitMinutes > 0, "invalid merchant capacity or duration");
            Require(e.MerchantBread >= 0 && e.MerchantFishTarget >= 0 && e.MerchantGold >= 0 && e.MerchantBait >= 0 && e.MerchantSpice >= 0, "merchant stock and gold cannot be negative");
            Require(e.BaitPrice > 0 && e.SpicePrice > 0, "prices must be positive");
        });
        var schedules = Read<Dictionary<string, ScheduleSettings>>("schedules.json", all =>
        {
            foreach (string name in new[] { "Farmer", "Fisher", "Cook", "Merchant", "Unemployed", "Baby", "Toddler", "Teenager" })
                Require(all.ContainsKey(name), $"missing schedule {name}");
            foreach (var pair in all)
            {
                Require(pair.Value is not null, $"{pair.Key} cannot be null");
                settings.Schedules.Add(pair.Key, new ScheduleTemplate(pair.Value!.Name, pair.Value.Entries));
            }
        });
        var npcFile = Read<NpcFileSettings>("npcs.json", file =>
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            Require(file.Defaults is not null && file.Residents is not null, "defaults and residents cannot be null");
            // Validate defaults separately; its name is not part of the resident uniqueness rule.
            foreach (var n in file.Residents!.Prepend(file.Defaults!))
            {
                Require(n is not null && !string.IsNullOrWhiteSpace(n.Name), "each NPC needs a name");
                Require(ReferenceEquals(n, file.Defaults) || names.Add(n!.Name), $"duplicate NPC name {n!.Name}");
                Require(float.IsFinite(n.X) && float.IsFinite(n.Y), $"{n.Name}: coordinates must be finite");
                Require(Enum.IsDefined(n.Job) && Enum.IsDefined(n.LifeStage), $"{n.Name}: invalid job or lifeStage");
                Require(n.Gold >= 0 && n.MaximumHunger > 0 && n.Hunger >= 0 && n.Hunger <= n.MaximumHunger, $"{n.Name}: invalid gold or hunger");
                Require(n.Bait >= 0 && n.Bread >= 0 && n.FarmPlots >= 0 && n.Spices >= 0, $"{n.Name}: opening supplies cannot be negative");
                Require(n.FishingSkill >= 1 && n.CookingSkill >= 1, $"{n.Name}: job skill levels must be at least one");
                Require(n.MinimumVitality >= 1 && n.MaximumVitality >= n.MinimumVitality && n.MaximumVitality < int.MaxValue && n.MinimumStrength >= 1 && n.MaximumStrength >= n.MinimumStrength && n.MaximumStrength < int.MaxValue, $"{n.Name}: invalid attribute range");
                Require(n.BaseHealth > 0 && n.HealthPerVitality >= 0 && n.BaseHealth + (long)n.MaximumVitality * n.HealthPerVitality <= int.MaxValue, $"{n.Name}: invalid health calculation");
                if (n.Schedule is not null) Require(settings.Schedules.ContainsKey(n.Schedule), $"{n.Name}: unknown schedule {n.Schedule}");
                Require(n.ProcessingSkills is not null && n.ProcessingSkills.All(x => ValidId(x.Key) && x.Value >= 0), $"{n.Name}: invalid processingSkills");
                Require(n.Tools is not null && n.Tools.All(id => settings.Processing.Tools.Any(t => t.Id == id)), $"{n.Name}: unknown held tool");
                n.Needs?.Validate(); n.AI?.Validate(); n.Work?.Validate(); if (n.Cooking is not null) ValidateCooking(n.Cooking);
                if (n.Sleep is not null) ValidateSleep(n.Sleep);
            }
        });
        settings.Npcs = npcFile.Residents;
        settings.NpcDefaults = npcFile.Defaults;
        return settings;

        /// <summary>Validates product quantities, raw-food servings, and shared metadata once at startup.</summary>
        /// <param name="outputs">One harvest or processing batch's item definitions.</param>
        void RegisterOutputs(IReadOnlyList<HarvestOutputDefinition>? outputs)
        {
            Require(outputs is not null && outputs.Count > 0, "harvestOutputs/outputs must contain at least one item");
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            foreach (var output in outputs!)
            {
                Require(output is not null && ValidId(output.ItemId) && !string.IsNullOrWhiteSpace(output.Name), "each output needs itemId and name");
                Require(ids.Add(output!.ItemId), $"duplicate output item {output.ItemId} within one batch");
                Require(output.MinimumQuantity > 0 && output.MaximumQuantity >= output.MinimumQuantity && output.MaximumQuantity < int.MaxValue, $"{output.ItemId}: invalid output quantity range");
                Require(output.SellPrice > 0 && output.MerchantStockTarget >= 0 && output.CookStockTarget >= 0, $"{output.ItemId}: invalid output trade settings");
                Require(output.ShelfLifeMinutes is null || output.ShelfLifeMinutes > 0, $"{output.ItemId}: shelfLifeMinutes must be positive when supplied");
                if (output.RawFood is not null)
                    Require(output.RawFood.Hunger > 0 && output.RawFood.Hunger <= 1000 && output.RawFood.Health >= 0 &&
                        output.RawFood.Health <= 1000 && output.RawFood.QuantityPerServing > 0, $"{output.ItemId}: invalid raw food effects or serving quantity");
                if (settings.Products.TryGetValue(output.ItemId, out var existing))
                    Require(existing.Name == output.Name && existing.SellPrice == output.SellPrice && existing.MerchantStockTarget == output.MerchantStockTarget && existing.CookStockTarget == output.CookStockTarget && existing.RawFood == output.RawFood && existing.ShelfLifeMinutes == output.ShelfLifeMinutes, $"{output.ItemId}: repeated item definitions need identical names, trade settings, and food metadata");
                else settings.Products.Add(output.ItemId, output);
            }
        }

        /// <summary>Validates shared-style cooking recipes and rejects unknown ingredient IDs, held tools, and meal identifiers.</summary>
        /// <param name="cooking">The shared or personal cooking settings.</param>
        void ValidateCooking(CookingSettingsComponent cooking)
        {
            cooking.Validate();
            Require(ValidId(cooking.KitchenStationType) && settings.Processing.Tools.Any(t => t.Id == cooking.UtensilToolId), "unknown cooking utensilToolId or invalid kitchenStationType");
            foreach (var recipe in cooking.Recipes)
            {
                Require(ValidId(recipe.Id), $"{recipe.Name}: invalid recipe id");
                Require(recipe.ProcessingStation == cooking.KitchenStationType || settings.Processing.Stations.Any(station => station.StationType == recipe.ProcessingStation),
                    $"{recipe.Id}: unknown cooking station type");
                Require(recipe.Tools is null || (recipe.Tools.Distinct().Count() == recipe.Tools.Count && recipe.Tools.All(id => settings.Processing.Tools.Any(t => t.Id == id))), $"{recipe.Id}: unknown or repeated held tool");
                Require(recipe.RequiredSkill is null || ValidId(recipe.RequiredSkill.Id), $"{recipe.Id}: invalid skill id");
                foreach (var ingredient in recipe.Inputs.SelectMany(g => g.Options))
                    Require(ingredient.Kind != ProcessingItemKind.Item || settings.Products.ContainsKey(ingredient.ItemId), $"{recipe.Name}: unknown ingredient item {ingredient.ItemId}");
                foreach (var output in recipe.Outputs.SelectMany(g => g.Options))
                {
                    Require(ValidId(output.ItemId), $"{recipe.Id}: invalid meal itemId");
                    Require(output.ShelfLifeMinutes is null || output.ShelfLifeMinutes > 0, $"{recipe.Id}: invalid food lifetime");
                    Require(recipe.PreservesFood ? output.ShelfLifeMinutes > settings.Food.OrdinaryMealShelfLifeMinutes :
                        (output.ShelfLifeMinutes ?? settings.Food.OrdinaryMealShelfLifeMinutes) <= settings.Food.OrdinaryMealShelfLifeMinutes,
                        $"{recipe.Id}: ordinary food must respect the meal lifetime limit; preserved outputs need an explicit longer lifetime");
                }
            }
        }

        /// <summary>Reads and validates one file, keeping its name in every error message.</summary>
        /// <typeparam name="T">The existing settings type or simple file data type.</typeparam>
        /// <param name="filename">The file within the settings directory.</param>
        /// <param name="validate">Checks performed before gameplay uses its values.</param>
        /// <returns>The loaded, validated data.</returns>
        T Read<T>(string filename, Action<T> validate) where T : class
        {
            string path = Path.Combine(directory, filename);
            try
            {
                T value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
                    ?? throw new ArgumentException("the file cannot contain null");
                validate(value);
                return value;
            }
            catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new InvalidDataException($"{path}: {error.Message}", error);
            }
        }
    }

    /// <summary>Rejects invalid sleep intervals, thresholds, times, or score weights before simulation starts.</summary>
    /// <param name="sleep">The shared or personal sleep settings.</param>
    private static void ValidateSleep(SleepSettingsComponent? sleep)
    {
        Require(sleep is not null, "sleep cannot be null");
        Require(sleep!.AwakeMinutesPerPoint > 0 && sleep.SleepMinutesPerPoint > 0, "sleep intervals must be positive");
        Require(sleep.WakeFatigue >= 0 && sleep.WakeFatigue < sleep.PreferredFatigue && sleep.PreferredFatigue <= sleep.UrgentFatigue && sleep.UrgentFatigue <= 100, "invalid sleep fatigue thresholds");
        Require(sleep.BedtimeMinute >= 0 && sleep.BedtimeMinute < 1440 && sleep.WakeTimeMinute >= 0 && sleep.WakeTimeMinute < 1440, "bedtime/wake minutes must be between zero and 1439");
        Require(double.IsFinite(sleep.SleepPoints) && sleep.SleepPoints >= 0 && double.IsFinite(sleep.SeverityWeight) && sleep.SeverityWeight >= 0, "sleep scores must be finite and non-negative");
    }

    /// <summary>Checks stable JSON IDs without binding them to any crop name.</summary>
    /// <param name="id">The plant, item, or processing ID.</param>
    /// <returns>True for nonempty IDs containing letters, digits, underscores, or hyphens.</returns>
    private static bool ValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-');

    /// <summary>Stops loading when a setting violates an explained gameplay rule.</summary>
    /// <param name="condition">Whether the setting is valid.</param>
    /// <param name="message">The explanation included with its filename.</param>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new ArgumentException(message);
    }
}
