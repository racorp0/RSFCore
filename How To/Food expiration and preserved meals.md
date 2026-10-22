# Food expiration and preserved meals

Food expiration now uses elapsed game minutes, not midnight. Ordinary cooked meals, including soup, last at most twelve game hours. Smoked fish lasts forty-five game days. Existing Program.cs simulation setup and the three resident profiles starting at 200 gold were preserved.

## Settings

`Settings/food.json` contains the shared policy:

```json
{
  "ordinaryMealShelfLifeMinutes": 720,
  "breadShelfLifeMinutes": 10080,
  "rawProduceShelfLifeMinutes": 7200,
  "freshFishShelfLifeMinutes": 1440,
  "freshMeatShelfLifeMinutes": 1440,
  "preservedFoodStockTarget": 2,
  "preservationReserveBatches": 2
}
```

These are game-balance values. Ordinary cooked food can use a shorter output lifetime, but neither the shared ordinary-meal policy nor an ordinary recipe output can exceed 720 minutes. Bread has its own seven-day policy; raw fish and meat last one day. Raw tomatoes last five days, and cabbage has a seven-day explicit product lifetime in farming.json.

`HarvestOutputDefinition.shelfLifeMinutes` is an optional per-product lifetime. It also applies to shared processing output. Products with no lifetime and no raw-food definition remain untimed: this phase leaves seeds, bait, spices, grain, and non-food materials unchanged. A future product can opt in with JSON instead of a new item-name condition in C#.

Raw-edible products without a lifetime override use `rawProduceShelfLifeMinutes`. Repeated definitions of the same product must agree on lifetime metadata. Invalid lifetimes report their source JSON filename. The new food.json is loaded through the same strict settings loader as the existing files.

## Smoked fish

recipes.json adds `smoked_fish` using the existing campfire:

- `preservesFood`: true.
- `processingMinutes`: 120.
- `tools`: null and `requiredSkill`: null.
- One complete alternative of 200 grams of configured species meat.
- One guaranteed Smoked Fish serving with 35 hunger before the existing quality adjustment.
- Output `shelfLifeMinutes`: 64800, which is 45 * 1440 game minutes.

A preserved recipe must explicitly provide a longer output lifetime. An ordinary recipe cannot gain long storage merely by setting a lifetime over twelve hours. The output MealComponent records IsPreserved, independently of the item name or quality.

## Data and time

FoodExpirationComponent stores CreatedAt and ExpiresAt as existing GameMinute values. FoodRules attaches a lifetime once; applying product metadata again cannot refresh it. FoodClockComponent refers to the existing WorldClock, without creating a second clock or changing World/Simulation ownership. A second food system using a different clock for the same world is rejected.

SimulationSetup registers FoodExpirationSystem immediately after TimeSystem. The system reads the fresh-food component index, rather than scanning map tiles. Once food spoils, its deadline moves to SpoiledFoodComponent and it leaves the fresh-food index. It is not repeatedly processed every later minute.

In a manually assembled world, create FoodExpirationSystem(world, clock) to expose that clock to blueprint creation and command validation. FoodRules retains village time/zero as a compatibility fallback. Existing manual worlds without loaded settings do not automatically receive all JSON default lifetimes; explicitly supplied output lifetimes still work.

## What happens at the deadline

Food is unusable when current minute >= ExpiresAt, even if a caller invokes a command before the regular expiration update. It cannot be eaten, have its effects applied directly, be offered, be bought, or supply cooking/processing inputs.

The expiration system removes consumable and sale markers, including automatic market pricing. It preserves the entity, item name, physical quantity, and inventory membership, marking the item SPOILED. Inventory debugging shows its original deadline. Spoiled food contributes zero usable quantity, meat grams, or ready servings, so it cannot block stock replenishment by appearing to be fresh inventory.

Spoiled items are not deleted, sold, automatically discarded, or converted into compost in this phase. They remain inspectable; inventory cleanup and recipes explicitly accepting spoiled materials would be separate features. Retaining those entities can increase long-running inventory sizes even though they leave the expiration update index.

## Splitting, trading, and cooking

A partial purchase copies the original batch timestamps. A full purchase transfers the same entity. Neither transaction restarts its lifetime. Eating one serving also leaves the remaining stack's timestamp unchanged. Batches are not merged by this feature.

When a whole fish is chopped, leftover meat expires at the earlier of the fish's original deadline and the configured fresh-meat deadline. Chopping just before expiry therefore cannot create another full day of fresh meat.

Cooking validates fresh inputs when it starts and consumes them through the existing planner. A successful cooked output receives a new lifetime from its completion minute, including a full 45 days for smoked fish. Inputs have already been consumed during the batch; they are not still inventory objects that spoil halfway through cooking. Existing attended-station and no-refund cancellation behavior remains unchanged.

## AI behavior

Carried food is ordered by deadline before equal-score choices are added. Existing urgency, cost, continuation points, and switch margin still matter, so this is an oldest-first tie-break rather than a replacement need priority.

Hungry actors retain existing ready-food and self-feeding behavior. `foodPreparationTimeWeight` in ai.json (0.1) applies a small preparation-time penalty, favoring faster feasible recipes. Critical HP retains priority. A newly urgent need can replace a planned slow preservation action before it starts; cooking recipe indices are included when identifying the current recipe choice. Already active batches keep the existing wait/interruption rules.

Well-fed adults can preserve surplus using existing PrepareFood, WithdrawGoods, travel, and cooking commands. `foodPreservationPoints` in ai.json (350) competes with normal job points. The goal checks existing needs, private/public station access, tools, skill requirements, and failed targets. It stops when the actor owns two fresh preserved servings; this includes its own listed preserved stock. Set preservedFoodStockTarget to zero to disable the automatic reserve goal.

Before starting, a read-only reservation checks enough inputs for the preservation batch plus two reserve batches. The reserve setting is measured in recipe batches, not a fixed fish weight or item ID. The command spends only the one actual batch. Listed inputs must be retrieved at their real counter first. No inventory is silently taken from another NPC or a remote listing.

Preservation is a personal stock goal, not a village-wide production plan or redistribution policy. It can change how much raw fish reaches the market. It does not promise a steady supply of regular meals for every villager.

## Changed files

New:

- `Configuration/FoodSettings.cs` and `RSF.Console/Settings/food.json`.
- `Components/FoodExpirationComponent.cs` (fresh deadlines, spoilage marker, and shared-clock reference).
- `Systems/FoodRules.cs` and `FoodExpirationSystem.cs`.

Updated:

- GameSettings, GameSettingsLoader, HarvestOutputDefinition, Recipe, CookingSettingsComponent, MealComponent, and AISettingsComponent: lifetime/preservation data and validation.
- BreadBlueprint, FishBlueprint, AppleBlueprint, SoupBlueprint, RawFoodRules, and CookingSystem: food creation timestamps.
- InventoryStacks, ProcessingRules, FishMeat, AISurvivalRules, UseItemCommandHandler, ItemEffectSystem, TradeSystem, MarketPricing, and FishPricing: freshness checks and deadline preservation.
- AIDecisionSystem, PersonalCookingAI, AIWorkDecision, VillageAIDecision, and MerchantVisitSystem: expired-stock exclusion, equal-score food ordering, urgent recipe changes, and bounded preservation.
- SimulationSetup and ComponentDebugger: update registration and readable deadlines/spoilage.
- farming.json, recipes.json, and ai.json: explicit food lifetimes, smoked fish, and preparation/preservation scores.

Program.cs, npcs.json, initial gold, jobs, crop growth, fishing probabilities, and base commodity prices were not changed.

## C# learning notes

A nullable integer such as `int? ShelfLifeMinutes` lets a product opt into a lifetime while other products remain unchanged. Small record structs keep time and spoilage as component data. The `with` expression copies a recipe for a read-only surplus calculation without changing its original JSON definition. Systems and command validation reuse FoodRules so UI, AI, trading, and processing agree on whether food is fresh.

## Validation and remaining balance issue

The console project builds with 0 errors and 20 existing nullable warnings. Focused checks cover exact expiry, twelve-hour soup, 45-day smoked fish, no refresh on splitting/trading/chopping/reapplying metadata, retained spoiled inventory, command guards, recipe validation, oldest-first choices, hunger priority, reserve bounds, and normal AI travel/smoking through Simulation.Update. Existing market, map, survival AI, profiler, processing, pricing, and self-feeding checks also passed: **495 total checks** (54 new expiration/preservation checks plus 441 regression checks).

The unchanged 50-day console run completed, produced and consumed smoked fish, and showed expired food in inventories. All three original residents died in this sample despite retaining gold. The shorter food lifetime and personal preservation goals introduce a production/trading balance problem; passing implementation checks does not establish a sustainable village economy. No starting-gold increase, lifetime relaxation, or unrelated job/production changes were made to hide this result. FoodExpirationSystem took about 120 ms across the 72,000 game-minute updates in this sample; the early deaths make the overall runtime unsuitable for comparison with an all-survivors run.
