# Local market activity ? phase 2

Markets now observe offered supply and affordable outstanding purchase requests. Prices and gold transfers still use the existing trade system unchanged.

## Data flow

`VillageBlueprint` adds `MarketActivityComponent` alongside `MarketPriceComponent`. `SimulationSetup` registers `MarketTrackingSystem` after AI execution, so a due snapshot sees the results of that minute's commands. The regular village debugger includes the snapshot; `Program.cs` needs no extra calls.

In `Settings/economy.json`, `market.trackingIntervalMinutes` controls observation frequency (currently 15 game minutes). This is separate from `updateIntervalMinutes`, which is reserved for future price adjustments. Snapshots can therefore be up to 15 minutes behind current inventory. The tracker checks indexed markets each minute and only reads indexed residents/inventories when a snapshot is due; it never scans map tiles.

## Supply

Supply includes registered, positive-quantity goods posted by living local sellers at their expected market stall or inn counter. Equipped goods, unlisted goods, and repeated references to the same inventory entity are excluded.

Specific references reuse existing identities: `item:rice_grain`, `seed:rice`, `bait:worm`, and `fish:Carp`. Whole-fish species totals and species meat quantities are grams. Seeds, bait, spice, vegetables, grains, and meals are counts.

Categories are alternative views of the same stock. `category:food` counts usable meal servings, `category:bait` counts usable bait, and `category:whole_fish` counts whole fish available to buy. **Do not add a category total to its specific item totals.** A 1,000-gram carp is 1,000 grams of `fish:Carp` and one whole-fish purchase, not 1,001 units.

## Demand

Demand is a current request snapshot, not a history of attempts or a count of completed sales. Food is one general category, as requested: a hungry buyer requests one next serving, rather than requesting every possible meal. Carried usable food already satisfies that purchase need.

Work requests use existing schedules, needs, inventory quantities, and stock targets: farmer seeds, fisher bait, cook spices/ingredients/fish, and visitor purchases. Survival interruptions prevent work shopping requests. Baby/toddler NPCs and departing visitors are excluded. Existing AI and command behavior are not changed.

Only affordable quantities count. Existing eligible posted prices take precedence. When no eligible offer exists, existing JSON base prices provide an estimate: the cheaper configured bread/meal price for food, bait definitions for bait, seed definitions for seeds, product sell prices, and the configured minimum fish weight and per-500-gram price for whole fish. A minimum-weight fish reference describes potential demand, not a guarantee that the next caught fish will be affordable. Healing demand uses actual usable healing offers; there is no invented fallback healing price.

Each NPC's wallet is reserved across requests, so the same gold cannot fund every stock target simultaneously. Critical healing comes first, then food, then other healing, then work supplies. Within work supplies, existing definition order provides a stable allocation; this is observation policy, not a replacement AI scoring system. Offered substitutes are priced at the cheapest eligible offer. Cook fish offers must supply meat accepted by that cook's recipes.

`MarketRequestKey` is a C# record struct combining the existing buyer `EntityId` and commodity key. A dictionary stores one entry for each pair. Rebuilding the snapshot replaces old requests rather than accumulating repeated searches. Purchases, job changes, fulfilled stock targets, lost gold, death, and departure are reflected at the next refresh. A `long` quantity keeps summed stock and cost calculations safe beyond ordinary `int` inventory totals.

## Scope

This phase does not adjust prices, match orders, reserve actual stock, or make NPCs buy different items. Different buyers can legitimately request the same last available item. Category demand is intentionally not distributed across individual meal, bait, or fish species prices; that allocation belongs to the later pricing phase. Multi-village map creation remains outside this feature, although market activity dictionaries are independent.

## Validation

Built the console project successfully: 0 errors and 20 existing nullable warnings. 49 focused tracking checks cover deduplication, affordable-only missing-stock demand, posted-price precedence, shared budgets, fulfilled/dead/departing buyers, stock validity, weight units, market isolation, and unchanged prices. Existing market, map, survival AI, profiler, and shared processing checks also pass. All 302 focused and regression checks passed. The unchanged 50-day console simulation completed and printed the market activity snapshot.
