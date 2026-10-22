# Self-feeding: campfire cooking and raw products

NPCs can now use their own food resources when they cannot obtain a ready meal. The fisherman can retrieve his own listed fish, travel to a public campfire, cook, and eat without gold, cooking skill, spices, or utensils. Raw tomatoes can be eaten one at a time. Program.cs is unchanged.

## JSON configuration

`Settings/processing.json` adds a normal processing station named Village Campfire at `(14, 14)` with station type `campfire`. Existing village station creation places it; no separate campfire blueprint or station framework was needed. Unowned stations are public under the existing processing access rules.

`Settings/recipes.json` adds `grilled_fish`:

- `processingStation`: `campfire`.
- `tools`: `null`.
- `requiredSkill`: `null`.
- `processingMinutes`: 15 game minutes.
- One required input group with alternatives for the configured fish meat IDs, each requiring 200 grams.
- One guaranteed Grilled Fish output, restoring 35 hunger before the existing quality adjustment.

The whole-fish input planner still applies the configured chopping loss once and saves unused species meat. A 1,000-gram carp at 80% yield gives 800 grams: cooking spends 200 and stores 600 for later. One recipe alternative must be fully supplied; partial amounts from different species are not mixed. Small catches may need to be combined with more of the same species to supply a full serving.

Cooking quality still uses existing Poor/Normal/Good rolls. A null skill requirement means no skill component is needed to start; this does not add skill XP or skill-dependent quality.

The tomato harvest output in `Settings/farming.json` now includes:

```json
"rawFood": {
  "hunger": 10,
  "health": 0,
  "quantityPerServing": 1
}
```

Omitting `rawFood` leaves an ordinary product inedible. This metadata belongs to the output item, so another raw-edible plant can use it without adding a plant-name check in C#. Repeated definitions of the same product must agree on raw-food metadata as well as names and trade settings. Hunger must be 1-1,000, health 0-1,000, and quantityPerServing positive.

`quantityPerServing` uses the product's ordinary count or gram unit. A serving must be available in one stack. Serving effects apply once per use; a five-tomato stack becomes four tomatoes after one use. Partial purchases preserve serving and restoration components. Products created by farming and shared processing both receive configured raw-food effects.

`Settings/ai.json` adds `decision.foodPreparationPenaltyPoints` (25). Preparation shares the food need's urgency and uses its already configured need score minus this penalty. This keeps normal survival points and HP priority intact.

## Decision flow

The existing needs and schedule rules still decide when food is needed, including pre-work preparation and meal breaks. Ready carried food and affordable ready-food offers remain the first choices. Raw tomatoes count as ready food and can be bought in complete serving quantities.

When no ready-food candidate is usable or affordable, `PersonalCookingAI` looks for a complete feasible recipe and an accessible station. This works during free time as well as work time. It does not require employment as a cook. Babies and toddlers retain existing independent-action restrictions.

If inputs are already carried, the actor chooses PrepareFood, travels through existing travel commands, and starts cooking at the selected station. The station ID is saved in CookingAttemptComponent. An active batch receives a WaitForCooking choice at the food need's urgency, so urgent hunger does not cancel and restart it each minute. Critical-HP healing still outranks food preparation.

If the complete recipe requires owned listed inputs, the AI first chooses WithdrawGoods and travels to their sale counter. The new WithdrawOfferCommand goes through the existing command dispatcher and TradeSystem. Withdrawal checks living ownership and real location, then removes sale, price, and automatic-price markers. It does not transfer gold or record a market sale. The next decision replans cooking using the retrieved inventory. Inputs are never spent remotely from a listing.

A read-only `includeListed` option on the existing input planner supports that look-ahead. Cooking commands still use its default carried-only validation. Missing stations, failed routes, changed ownership, and unavailable ingredients use the existing failure and retry behavior.

## Cooking access and compatibility

StartCookingCommand and CookingSystem.Start accept an optional station while preserving their existing two-argument calls. Private kitchen recipes still require the actor's owned village inn. Public station recipes use the existing station-type and ownership rules. Knowing recipes does not grant cooking skill, utensils, or private kitchen access.

NpcBlueprint attaches configured recipe settings to NPCs. Only the existing eligible cook gains its cooking skill, utensils, and inn ownership. Public stations can be selected from the existing shared AI station list, preferring nearby compatible stations.

Cooking remains attended: leaving the saved station or losing access cancels the batch, using the existing no-refund behavior. Station capacity, fuel consumption, and a cooking queue were not introduced; public campfires can host multiple actors' batches.

## Stack consumption and markets

FoodServingComponent stores units consumed by one use. UseItemCommand applies effects once, reduces the stack, and removes its inventory entry only when the serving consumes the remainder. AI execution recognizes either a reduced quantity or item removal as success, while still requiring an actual improvement to hunger or health.

When a serving is bought from a larger stack, the existing split mechanism creates a buyer item. AI execution now follows that new entity as its next Eat target, rather than incorrectly following the seller's remaining stack.

General food supply and successful food sales count complete servings. Per-item prices are converted to serving cost for affordability checks using long arithmetic. Specific product totals retain their original count/gram units. This keeps a tomato stack from being counted as only one meal or a gram amount from being treated as that many meals.

## Changed files

New files:

- `Common/RawFoodDefinition.cs`: optional JSON product food effects.
- `Components/FoodServingComponent.cs`: per-use stack quantity.
- `Systems/RawFoodRules.cs`: food creation and serving quantities.
- `Systems/PersonalCookingAI.cs`: survival preparation and retrieval choices.
- `Commands/WithdrawOfferCommand.cs` and `CommandHandlers/WithdrawOfferCommandHandler.cs`: existing-pipeline retrieval requests.

Updated groups under RSF.Core:

- `Common/HarvestOutputDefinition.cs`, `Components/AISettingsComponent.cs`, and `Common/AIActivity.cs`: configuration and action data.
- `Components/CookingSettingsComponent.cs`, `CookingAttemptComponent.cs`, `Systems/CookingRules.cs`, and `CookingSystem.cs`: public stations, optional requirements, and saved batch location.
- `Commands/StartCookingCommand.cs` and its handler: optional selected station.
- `Blueprints/NpcBlueprint.cs`: recipe knowledge without granting skill.
- `Systems/FarmingSystem.cs`, `ProcessingSystem.cs`, `ProcessingRules.cs`, `InventoryStacks.cs`, and `CommandHandlers/UseItemCommandHandler.cs`: product food effects, retrieval planning, splits, and exact serving consumption.
- `Systems/AISurvivalRules.cs`, `AIDecisionSystem.cs`, `AIActionExecutionSystem.cs`, `VillageAIDecision.cs`, and `VillageAIExecution.cs`: readiness, fallback choices, public-station travel, and execution feedback.
- `Systems/TradeSystem.cs` and `World/SimulationSetup.cs`: validated retrieval and handler registration.
- `Systems/MarketTrackingSystem.cs`, `MarketSales.cs`, `Configuration/GameSettingsLoader.cs`, and `Debugging/ComponentDebugger.cs`: serving-aware tracking, strict configuration, and inspection.

Updated JSON: `farming.json`, `processing.json`, `recipes.json`, and `ai.json`. Other prices, opening gold, jobs, growth times, and the Program.cs simulation loop were preserved.

## C# learning notes

`null` represents an omitted requirement, so a skill-free recipe needs no special level-zero rule. An optional parameter such as `Entity? Station = null` extends existing commands while preserving callers that omit it. The station ID and serving size remain component data; systems and handlers perform behavior. A nullable raw-food definition lets ordinary products remain unchanged unless their JSON opts in.

## Validation

The console project builds with 0 errors and the same 20 existing nullable warnings. Temporary focused checks cover zero-gold self-cooking, listed-input retrieval, private ownership, distance, saved stations, elapsed cooking time, no-refund cancellation, raw-food consumption, purchase splits, HP priority, baby restrictions, actual tomato harvests, JSON errors, and normal Simulation.Update travel and eating.

63 new self-feeding checks and 378 regression checks passed: **441 total**. The unchanged 50-day console run with profiling completed in 39.02 seconds in this sample. All three original residents survived; Haruto ended with zero gold. The log recorded 74 grilled-fish meals produced and 170 raw-tomato servings consumed across NPCs. These are random integration results, not a guarantee of survival in every run or proof of a balanced economy. Longer-lived actors perform more AI updates than a run where residents die early, so total runtime is not a controlled comparison with the previous starvation run.
