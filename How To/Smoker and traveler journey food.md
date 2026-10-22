# Smoker and traveler journey food

Ren is the new resident with `Job.Smoker`. His profile is in `RSF.Console/Settings/npcs.json`; his work and meal-break times are in `schedules.json`. All four residents start with 200 gold. The existing three residents' starting data is unchanged. Ren starts at 80 hunger so adding him does not consume the opening bread needed to start farming and fishing.

## Behavior

During work, the smoker buys whole fish or prepared species meat accepted by an accessible preservation recipe. Existing cooking planning chops whole fish and retains unused meat grams. No special fish or meal entities are created for the AI. The existing `smoked_fish` recipe still needs the operator at the campfire for 120 game minutes and produces food lasting 64,800 minutes (45 days).

The smoker offers each completed meal before starting another batch. This prevents the production score from keeping every finished meal hidden until the stock target is full. Commercial smoking stops outside work or when enough journey-ready food is held. Health, hunger, and sleep retain the existing priority rules.

Other residents no longer make preserved-food reserves while well fed. Their personal cooking fallback still works when they need to eat and cannot obtain ready food.

Aoi cooks fresh food for current affordable customer needs. The decision checks existing hunger, before-work eating, and meal breaks. Customers already holding usable food are excluded. Available fresh sale servings and pending cooking batches reduce demand. A finished meal in Aoi's inventory counts before its offer action, preventing another batch from taking priority over selling the first. Her existing small ingredient buffer remains available so preparing a meal does not require starting every ingredient purchase after everyone becomes hungry. This reduces speculative cooked-food production; it cannot guarantee that no meal ever expires.

Visitors prefer ordinary food during their stay, keeping smoked food for their journey. Existing food remains usable as an emergency fallback when fresh food is unavailable. Before optional commodity shopping, visitors reserve a local-price estimate for their missing journey rations. Survival purchases can still use that money.

When a visit would normally end, it enters `PreparingDeparture`. The NPC buys preserved meals through `BuySupplies` and `BuyItemCommand`, independently of work hours. After reaching its ration target, it follows the existing `LeaveVillage` action and actual route to the entrance. If supplies, funds, or reachable sellers are unavailable, the shopping time limit prevents an endless visit. Successful purchases still transfer finite stock and gold and update the existing market sales history.

## JSON settings

`food.json` contains these controls:

| Setting | Current value | Meaning |
| --- | ---: | --- |
| `preservedFoodStockTarget` | 16 | Smoker's target of meals with enough lifetime for a journey. |
| `preservationReserveBatches` | 0 | Extra fresh batches that must remain after starting smoking. |
| `merchantJourneyFoodTarget` | 12 | Journey meals a departing visitor tries to own. |
| `departureShoppingMinutes` | 360 | Maximum final shopping time, in elapsed game minutes. |
| `minimumJourneyShelfLifeMinutes` | 43200 | Minimum remaining food lifetime for journey purchases: 30 days. |
| `departureShoppingPoints` | 400 | Final shopping preference before purchase-cost and continuation adjustments. |
| `freshFoodPreferencePoints` | 100 | Visitor score penalty for consuming or buying preserved food for ordinary hunger. |

The smoker replenishes food that no longer has the required journey lifetime, even when older smoked meals remain edible. The target counts journey-ready meals; older edible stock remains available to local buyers until sold, eaten, or expired. Recipe duration, outputs, requirements, and shelf life remain in `recipes.json`. Existing prices and ingredient quantities retain their existing configuration files.

## Files and architecture

- `Common/Job.cs` and `Common/ScheduleTemplates.cs`: append the smoker job and provide a manual-world schedule fallback. Existing job values retain their order.
- `Components/MerchantVisitComponent.cs`: add departure-shopping data without moving behavior into the component.
- `Configuration/FoodSettings.cs`: validate configurable production, preference, and journey limits.
- New `Systems/FoodProductionRules.cs`: share demand, preserved-food eligibility, ration counts, and estimated reserved gold.
- `Systems/PersonalCookingAI.cs` and `Systems/VillageAIDecision.cs`: choose smoker work, customer-driven cooking, meal offers, and final shopping through existing candidates.
- `Systems/AIDecisionSystem.cs`: prefer ordinary food for visitors and allow departure transitions to replace obsolete travel goals.
- `Systems/AIWorkDecision.cs` and `Systems/AIWorkExecution.cs`: reuse ingredient purchasing for the smoker. Avoid buying a few meat grams that cannot complete a recipe with held stock.
- `Systems/VillageAIExecution.cs`: verify individual meal transfers when buying journey supplies.
- `Systems/MerchantVisitSystem.cs`: stage and bound departure shopping before the existing leave command.
- `Systems/MarketTrackingSystem.cs`: track affordable smoker inputs and one preserved-food identity for journey demand, retaining a shared budget. Prepared meat and whole fish are alternatives, so absent meat offers do not reserve money needed to buy fish.
- `Debugging/ComponentDebugger.cs`: show whether a visitor is shopping for departure.
- `Settings/food.json`, `Settings/npcs.json`, and `Settings/schedules.json`: configure the behavior and new resident.

`Program.cs`, simulation registration, time conventions, and the command framework are unchanged. Continue using the existing blueprint setup and `simulation.Update(...)` workflow.

## C# notes

`Job.Smoker` is another enum value, so job checks use a named value rather than an NPC's name. Ren's name is only starting data.

`PreparingDeparture` is a Boolean component property. It records which stage the visit is in; the visitor system and existing AI perform the work.

The existing recipe and settings records support `with { ... }`. The ingredient planner uses this to select a worker's relevant recipes without modifying the shared JSON recipe list.

The demand calculation uses a list of `(Price, Servings)` tuples. A tuple groups two related values without needing another class. Each affordable customer uses one available serving from that temporary list, so the same serving is not assigned repeatedly.

## Validation and limits

The console project built with zero errors and 20 existing nullable-reference warnings. Builds used a fresh temporary output directory because a previous console executable had been locked by another process.

562 checks passed: 67 smoker/customer/traveler checks, 54 expiration checks, and 441 market, pricing, map, survival AI, processing, profiling, and self-feeding checks. Existing temporary fixtures were copied and adjusted for the approved behavior: four residents claim four houses, proactive preservation belongs to a working smoker, and journey reserves need an actual month-long remaining lifetime. The original checks were retained.

The new checks include a normal `SimulationSetup.Configure` / `VillageBlueprint` / `simulation.Update` scenario: a visitor buys twelve smoked meals, pays the seller 96 gold, follows map travel, and leaves with smoked food in its recorded cargo. Other checks cover customer-only fresh cooking, counting unfinished and completed batches, ordinary NPC self-feeding, commercial stock limits, incomplete meat purchases, expired journey stock, affordable market requests, and departure shopping timeout.

The existing 50-day console experiment also ran. Fishing, fresh cooking, smoking, and offers occurred, but all four residents eventually died from food shortages before the later visiting merchants could support trade. This is a remaining food-supply and survival-work balance problem. Passing the feature checks does not establish long-term village survival. Opening gold, existing food lifetimes, fish catch rates, and the existing residents' supplies were not changed to hide that result. Prepared meat and meal prices also retain their existing units and may need economic balancing in a later feature.
