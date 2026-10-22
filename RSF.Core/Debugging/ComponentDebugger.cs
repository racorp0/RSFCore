using System.Text;

using RSF.Core.Configuration;
using RSF.Core.Components;
using RSF.Core.Simulation;

namespace RSF.Core.Debugging;

public static class ComponentDebugger
{
    /// <summary>Formats inventory quantities, food deadlines, and village market data using existing world lookups.</summary>
    /// <param name="component">The component to describe.</param>
    /// <param name="world">The world used to look up inventory items.</param>
    /// <returns>Named inventory entries, or the usual description for other components.</returns>
    public static string Dump(object component, World world)
    {
        // Include the related market data in the usual village dump, keeping Program.cs unchanged.
        if (component is VillageComponent village && world.TryGetEntity(village.MarketId, out var market) && market is not null &&
            world.TryGet<MarketPriceComponent>(market, out var prices) && prices is not null)
        {
            string result = Dump(component) + Dump(prices);
            if (world.TryGet<MarketActivityComponent>(market, out var activity) && activity is not null)
                result += Dump(activity);
            return result;
        }

        // Keep the original overload usable when callers do not have a World.
        if (component is not InventoryComponent inventory)
        {
            return Dump(component);
        }

        StringBuilder sb = new();
        sb.AppendLine("InventoryComponent");
        sb.AppendLine($"    Items: {inventory.Items.Count}");

        foreach (var itemEntity in inventory.Items)
        {
            string name = "Unknown Item";
            int quantity = 1;

            if (world.TryGet<ItemComponent>(itemEntity, out var item))
            {
                name = item.Name;
            }

            // All plant seeds and outputs use the shared stack conventions.
            if (world.TryGet<BaitComponent>(itemEntity, out var bait))
            {
                quantity = bait.Quantity;
            }

            else if (world.TryGet<PlantSeedComponent>(itemEntity, out var plantSeed))
            {
                quantity = plantSeed.Quantity;
            }
            else if (world.TryGet<PlantProduceComponent>(itemEntity, out var produce))
            {
                quantity = produce.Quantity;
            }
            else if (world.TryGet<SpiceComponent>(itemEntity, out var spice))
            {
                quantity = spice.Quantity;
            }

            // Fish are individual items, so show their weight alongside the species name.
            string weight = world.TryGet<FishComponent>(itemEntity, out var fish)
                ? $", {fish.WeightGrams} g"
                : string.Empty;
            string quality = world.TryGet<MealComponent>(itemEntity, out var meal) ? $", {meal.Quality}" : string.Empty;
            string effects = world.TryGet<RestoreHungerEffectComponent>(itemEntity, out var foodEffect) ? $", food +{foodEffect.Amount}" : string.Empty;
            string healing = world.TryGet<RestoreHealthEffectComponent>(itemEntity, out var healthEffect) ? $", health +{healthEffect.Amount}" : string.Empty;
            bool meatGrams = world.TryGet<PlantProduceComponent>(itemEntity, out var meat) &&
                GameSettings.Find(world)?.Fishing.FishTypes.Any(fish => fish.MeatItemId == meat.ItemId) == true;
            string amount = meatGrams ? $" {quantity} g" : $" x{quantity}";
            string freshness = world.TryGet<SpoiledFoodComponent>(itemEntity, out var spoiled) ? $", SPOILED at minute {spoiled.ExpiredAt.Value}"
                : world.TryGet<FoodExpirationComponent>(itemEntity, out var expiration)
                    ? $", expires at minute {expiration.ExpiresAt.Value} ({Math.Max(0, expiration.ExpiresAt.Value - RSF.Core.Systems.FoodRules.Now(world).Value)} minutes left)" : string.Empty;
            sb.AppendLine($"        - {name}{amount}{weight}{quality}{effects}{healing}{freshness} ({itemEntity.Id})");
        }

        return sb.ToString();
    }

    /// <summary>Formats gameplay components and saved transaction histories through the existing entity dump.</summary>
    /// <param name="component">The component instance to describe.</param>
    /// <returns>A readable description of the component's current state.</returns>
    public static string Dump(object component)
    {
        StringBuilder sb = new();

        switch (component)
        {
            case TransactionHistoryComponent history:
                return TransactionHistoryDebugger.Dump(history);
            case FoodExpirationComponent expiration:
                sb.AppendLine($"Food created at minute {expiration.CreatedAt.Value}; expires at minute {expiration.ExpiresAt.Value}");
                break;
            case SpoiledFoodComponent spoiled:
                sb.AppendLine($"SPOILED food, original deadline minute {spoiled.ExpiredAt.Value}");
                break;
            case FoodClockComponent time:
                sb.AppendLine($"Food uses simulation clock at minute {time.Clock.CurrentMinute.Value}");
                break;
            case MarketActivityComponent activity:
                sb.AppendLine($"Local market activity: last snapshot {activity.LastTrackedAt?.Value.ToString() ?? "none"}");
                sb.AppendLine($"    Outstanding buyer requests: {activity.OutstandingRequests.Count}");
                sb.AppendLine("    Categories are substitute views; do not add them to specific item totals.");
                foreach (string key in activity.Supply.Keys.Union(activity.Demand.Keys).Union(activity.RollingSales.Keys).OrderBy(key => key, StringComparer.Ordinal))
                {
                    activity.Supply.TryGetValue(key, out var supply);
                    activity.Demand.TryGetValue(key, out var demand);
                    activity.RollingSales.TryGetValue(key, out var sales);
                    sb.AppendLine($"    {key}: supply {supply.Quantity}, demand {demand.Quantity}, recent purchases {sales.Quantity} {supply.Unit ?? demand.Unit ?? sales.Unit}");
                }
                break;
            case MarketPriceComponent prices:
                sb.AppendLine("Local market prices (applied to NPC-generated offers)");
                sb.AppendLine($"    Update interval: {prices.UpdateIntervalMinutes} minutes; multiplier limits: {prices.MinimumPriceMultiplier}-{prices.MaximumPriceMultiplier}");
                sb.AppendLine($"    Maximum change per update: {prices.MaximumChangePercentPerUpdate}%; last adjustment: {prices.LastPriceUpdateAt?.Value.ToString() ?? "none"}");
                sb.AppendLine($"    Sales window: {prices.SalesWindowMinutes} minutes; stock target: {prices.TargetStockCoverageMinutes} minutes; stable range: +/-{prices.CoverageTolerancePercent}%");
                sb.AppendLine($"    Last price check: {prices.LastEvaluatedAt?.Value.ToString() ?? "none"}");
                sb.AppendLine("    Unlisted commodities: multiplier 1; category factors apply to their members");
                foreach (var pair in prices.PriceMultipliers.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    sb.AppendLine($"    {pair.Key}: multiplier {pair.Value}");
                break;
            case ProcessingStationComponent station:
                sb.AppendLine($"Processing station: {station.StationType}");
                break;
            case ProcessingToolComponent tool:
                sb.AppendLine($"Held tool: {tool.ToolId}");
                break;
            case ProcessingSkillsComponent skills:
                sb.AppendLine("Processing skills: " + string.Join(", ", skills.Levels.Select(p => p.Key + " level " + p.Value)));
                break;
            case OperatingProcessComponent operating:
                sb.AppendLine($"Operating process at {operating.HostId}");
                break;
            case ProcessingJobComponent job:
                sb.AppendLine($"Process: {job.Recipe.Name}, progress {job.ProgressMinutes}/{job.Recipe.ProcessingMinutes} minutes, operator required: {job.Recipe.RequiresOperator}, completed: {job.Completed}, stored outputs: {job.Outputs.Count}");
                break;
            case PlantCropComponent crop:
                sb.AppendLine($"Crop: {crop.Definition.Name} ({crop.Definition.Type}), growth {crop.GrowthDays}/{crop.RequiredGrowthDays}, harvests {crop.HarvestCount}, remaining {crop.RemainingHarvests}, dry: {crop.IsDry}");
                break;
            case PlantSeedComponent seed:
                sb.AppendLine($"Plant seeds: {seed.PlantId}, quantity {seed.Quantity}");
                break;
            case FoodServingComponent serving:
                sb.AppendLine($"Food serving: {serving.Quantity} stack unit(s) per use");
                break;
            case PlantProduceComponent produce:
                sb.AppendLine($"Plant produce: {produce.ItemId}, quantity {produce.Quantity}");
                break;
            case MapPlotComponent plot:
                sb.AppendLine($"Map parcel: {plot.Id}, {plot.Kind}, {plot.Width} x {plot.Height} tiles, origin ({plot.X}, {plot.Y}), floors: {plot.Floors}");
                break;
            case BuildingComponent building:
                sb.AppendLine($"Building: {building.Kind}, capacity: {building.Capacity}");
                break;
            case HomeComponent home:
                sb.AppendLine($"Home: {home.HouseId}");
                break;
            case VillageResidentComponent:
                sb.AppendLine("Village resident or visitor");
                break;
            case VillageComponent village:
                sb.AppendLine($"    Map size: {village.Width} x {village.Height} tiles, {village.MetresPerTile} metres per tile");
                sb.AppendLine($"Village: {village.Cells.Count} grid tiles, market: {village.MarketId}, inn: {village.InnId}");
                sb.AppendLine($"    Merchants arrived: {village.MerchantsArrived}, next arrival minute: {village.NextMerchantAt}");
                foreach (string departure in village.DepartedMerchants) sb.AppendLine($"    {departure}");
                break;
            case TravelComponent travel:
                sb.AppendLine($"Travel: destination {travel.DestinationId}, {travel.Steps.Count} remaining tiles");
                break;
            case FatigueComponent fatigue:
                sb.AppendLine($"Fatigue: {fatigue.Value}/100");
                break;
            case FatigueClockComponent fatigueClock:
                sb.AppendLine($"Fatigue tick minute: {fatigueClock.LastTickAt}");
                break;
            case SleepSettingsComponent sleepSettings:
                sb.AppendLine($"Sleep settings: bedtime {sleepSettings.BedtimeMinute}, urgent fatigue {sleepSettings.UrgentFatigue}");
                break;
            case SleepingComponent sleeping:
                sb.AppendLine($"Sleeping at: {sleeping.PlaceId}");
                break;
            case InnStayComponent stay:
                sb.AppendLine($"Inn stay: {stay.InnId}, paid until minute {stay.PaidUntil}");
                break;
            case SaleLocationComponent sale:
                sb.AppendLine($"Sale counter: {sale.PlaceId}");
                break;
            case SpiceComponent spice:
                sb.AppendLine($"Spices: {spice.Quantity}");
                break;
            case CookingUtensilsComponent:
                sb.AppendLine("Reusable cooking utensils");
                break;
            case CookingSkillComponent cookingSkill:
                sb.AppendLine($"Cooking skill: {cookingSkill.Level}");
                break;
            case GameSettings configuration:
                sb.AppendLine($"Loaded JSON settings: {configuration.Npcs.Count} resident profiles, {configuration.Schedules.Count} schedule templates");
                break;
            case FarmingSettingsComponent farmingSettings:
                sb.AppendLine($"Farming: {farmingSettings.MinimumWorms}-{farmingSettings.MaximumWorms} worms per successful plow");
                break;
            case CookingSettingsComponent cookingSettings:
                sb.AppendLine($"Cooking: {cookingSettings.Recipes.Count} recipes, meal stock target {cookingSettings.MealStockTarget}");
                break;
            case CookingAttemptComponent batch:
                sb.AppendLine($"Cooking: {batch.Recipe.Name}, station {batch.StationId}, started minute {batch.StartedAt}");
                break;
            case MealComponent meal:
                sb.AppendLine($"Meal: {meal.RecipeName}, output: {meal.ItemId}, quality: {meal.Quality}");
                break;
            case MerchantVisitComponent visit:
                sb.AppendLine($"Merchant: arrived {visit.ArrivedAt}, last trade {visit.LastTradeAt}, shopping for departure: {visit.PreparingDeparture}, departing: {visit.Departing}");
                break;

            case JobComponent job:
                sb.AppendLine("JobComponent");
                sb.AppendLine($"    Job: {job.Job}");
                break;

            case LifeStageComponent life:
                sb.AppendLine("LifeStageComponent");
                sb.AppendLine($"    Stage: {life.Stage}");
                break;

            case ScheduleComponent schedule:
                sb.AppendLine("ScheduleComponent");
                sb.AppendLine($"    Template: {schedule.PersonalTemplate?.Name ?? "Follow job and life stage"}");
                break;

            case ScheduleStateComponent planned:
                sb.AppendLine("ScheduleStateComponent");
                sb.AppendLine($"    Template: {planned.Template?.Name}, preference: {planned.Activity}");
                sb.AppendLine($"    Work started: {planned.WorkStarted}, preparing food: {planned.PreparingFood}");
                break;

            case AIWorkSettingsComponent work:
                sb.AppendLine("AIWorkSettingsComponent");
                sb.AppendLine($"    Before-work hunger target: {work.BeforeWorkHungerPercent}%");
                sb.AppendLine($"    Break-time hunger target: {work.BreakHungerPercent}%");
                break;

            case NeedsSettingsComponent needsSettings:
                sb.AppendLine("NeedsSettingsComponent");
                sb.AppendLine($"    Critical HP: {needsSettings.CriticalHealthPercent}%, recovery target: {needsSettings.HealthyPercent}%");
                sb.AppendLine($"    Urgent hunger: {needsSettings.UrgentHungerPercent}%, food target: {needsSettings.SatisfiedHungerPercent}%");
                break;

            case NeedsStateComponent needs:
                sb.AppendLine("NeedsStateComponent");
                sb.AppendLine($"    HP: {needs.HealthPercent:F1}%, hunger: {needs.HungerPercent:F1}%");
                sb.AppendLine($"    Seek healing: {needs.SeekHealing}, seek food: {needs.SeekFood}");
                break;

            case AISettingsComponent aiSettings:
                sb.AppendLine("AISettingsComponent");
                sb.AppendLine($"    Healing points: {aiSettings.HealingPoints}, eating points: {aiSettings.EatingPoints}");
                sb.AppendLine($"    Decision interval: {aiSettings.DecisionInterval}, retry delay: {aiSettings.RetryDelay}");
                break;

            case AIStateComponent ai:
                sb.AppendLine("AIStateComponent");
                sb.AppendLine($"    Activity: {ai.Activity}, score: {ai.Score:F1}, urgency: {ai.Urgency}");
                sb.AppendLine($"    Target: {ai.TargetId}, next review: {ai.NextDecisionAt}");
                sb.AppendLine($"    Result: {ai.LastResult}");
                break;

            case GoldComponent gold:
                sb.AppendLine("GoldComponent");
                sb.AppendLine($"    Gold: {gold.Amount}");
                break;

            case TradePriceComponent price:
                sb.AppendLine("TradePriceComponent");
                sb.AppendLine($"    Price per unit: {price.GoldPerUnit} gold");
                break;

            case NameComponent name:
                sb.AppendLine("NameComponent");
                sb.AppendLine($"    Name: {name.Value}");
                break;

            case PositionComponent position:
                sb.AppendLine("PositionComponent");
                sb.AppendLine($"    X: {position.X}");
                sb.AppendLine($"    Y: {position.Y}");
                break;

            case VelocityComponent velocity:
                sb.AppendLine("VelocityComponent");
                sb.AppendLine($"    X: {velocity.X}");
                sb.AppendLine($"    Y: {velocity.Y}");
                break;

            case HealthComponent health:
                sb.AppendLine("HealthComponent");
                sb.AppendLine($"    Current: {health.Current}");
                sb.AppendLine($"    Max: {health.Max}");
                break;

            case ManaComponent health:
                sb.AppendLine("ManaComponent");
                sb.AppendLine($"    Current: {health.Current}");
                sb.AppendLine($"    Max: {health.Max}");
                break;

            case HungerComponent health:
                sb.AppendLine("HungerComponent");
                sb.AppendLine($"    Current: {health.Current}");
                sb.AppendLine($"    Max: {health.Max}");
                break;

            case PoisonStatusComponent poison:
                sb.AppendLine("PoisonStatusComponent");
                sb.AppendLine(
                    $"    DamagePerTick: {poison.DamagePerTick}");
                sb.AppendLine(
                    $"    TickInterval: {poison.TickInterval}");
                sb.AppendLine(
                    $"    Duration: {poison.Duration}");
                sb.AppendLine(
                    $"    StartedAt: {poison.StartedAt}");
                sb.AppendLine(
                    $"    LastTickAt: {poison.LastTickAt}");
                break;

            case ApplyPoisonEffectComponent poison:
                sb.AppendLine("ApplyPoisonEffectComponent");
                sb.AppendLine(
                    $"    DamagePerTick: {poison.DamagePerTick}");
                sb.AppendLine(
                    $"    TickInterval: {poison.TickInterval}");
                sb.AppendLine(
                    $"    Duration: {poison.Duration}");
                break;

            case ConsumableItemComponent:
                sb.AppendLine("ConsumableItemComponent");
                break;

            case RestoreHealthEffectComponent effect:
                sb.AppendLine("RestoreHealthEffectComponent");
                sb.AppendLine($"    Amount: {effect.Amount}");
                break;

            case CooldownComponent cooldown:
                sb.AppendLine("CooldownComponent");
                sb.AppendLine(
                    $"    StartedAt: {cooldown.StartedAt}");
                sb.AppendLine(
                    $"    Duration: {cooldown.Duration}");
                break;

            case ItemComponent item:
                sb.AppendLine("ItemComponent");
                sb.AppendLine(
                    $"    Name: {item.Name}");
                break;

            case EquipmentItemComponent equipmentItem:
                sb.AppendLine("EquipmentItemComponent");
                sb.AppendLine($"    Slot: {equipmentItem.Slot}");
                break;

            case InventoryComponent inventory:
                sb.AppendLine("InventoryComponent");
                sb.AppendLine(
                    $"    Items: {inventory.Items.Count}");

                foreach (var itemEntity in inventory.Items)
                {
                    sb.AppendLine(
                        $"        - {itemEntity.Id}");
                }

                break;

            case EquipmentComponent equipment:
                sb.AppendLine("EquipmentComponent");

                if (equipment.Weapon is null)
                {
                    sb.AppendLine(
                        "    Weapon: None");
                }
                else
                {
                    sb.AppendLine(
                        $"    Weapon: {equipment.Weapon.Id}");
                }

                break;

            case StrengthComponent str:
                sb.AppendLine("StrengthComponent");
                sb.AppendLine($"    STR: {str.Value}");
                break;

            case VitalityComponent vit:
                sb.AppendLine("VitalityComponent");
                sb.AppendLine($"    VIT: {vit.Value}");
                break;

            case ManaRegenerationComponent manaregen:
                sb.AppendLine("ManaRegenerationComponent");
                sb.AppendLine($"    {manaregen.ManaPerTick} / {manaregen.TickInterval}");
                break;

            case LandComponent land:
                sb.AppendLine("LandComponent");
                sb.AppendLine($"    name: {land.Name}");
                break;

            case OwnershipComponent owner:
                sb.AppendLine("Owner");
                sb.AppendLine($"    ID: {owner.OwnerId}");
                break;

            case LandLeaseComponent landlease:
                sb.AppendLine("LandLeaseComponent");
                sb.AppendLine($"    renter: {landlease.TenantId}");
                sb.AppendLine($"    start: {landlease.StartMinute}");
                sb.AppendLine($"    expired: {landlease.ExpiryMinute}");
                break;

            case FarmPlotComponent plot:
                sb.AppendLine("FarmPlotComponent");
                sb.AppendLine($"    Last watered: {plot.LastWateredAt?.ToString() ?? "Never"}");
                break;





            case FishingSkillComponent skill:
                sb.AppendLine("FishingSkillComponent");
                sb.AppendLine($"    Level: {skill.Level}");
                break;

            case FishingRodComponent:
                sb.AppendLine("FishingRodComponent");
                break;

            case BaitComponent bait:
                sb.AppendLine("BaitComponent");
                sb.AppendLine($"    Bait ID: {bait.BaitId}");
                sb.AppendLine($"    Quantity: {bait.Quantity}");
                break;

            case FishComponent fish:
                sb.AppendLine("FishComponent");
                sb.AppendLine($"    Species: {fish.Species}");
                sb.AppendLine($"    Weight: {fish.WeightGrams} g");
                break;

            case FishingAttemptComponent attempt:
                sb.AppendLine("FishingAttemptComponent");
                sb.AppendLine($"    River: {attempt.RiverId}");
                sb.AppendLine($"    Bait ID: {attempt.BaitId}");
                sb.AppendLine($"    Started at: {attempt.StartedAt}");
                sb.AppendLine($"    Duration: {attempt.Duration.Minutes} minutes");
                break;

            case RiverComponent river:
                sb.AppendLine("RiverComponent");
                sb.AppendLine($"    Location: {river.LocationId}, water type: {river.WaterType}");
                foreach (var fishType in river.FishTypes)
                {
                    sb.AppendLine($"    {fishType.Species}: {fishType.CatchChancePercent}%, " +
                        $"{fishType.MinWeightGrams}-{fishType.MaxWeightGrams} g");
                }
                foreach (var baitType in river.Baits)
                    sb.AppendLine($"    Bait {baitType.Id}: {string.Join(", ", baitType.CatchChances.Select(chance => $"{chance.Key} {chance.Value}%"))}");
                break;

            case WateringCanComponent:
                sb.AppendLine("WateringCanComponent");
                break;

            default:
                sb.AppendLine(
                    $"Unknown Component: {component.GetType().Name}");
                break;
        }

        return sb.ToString();
    }
}
