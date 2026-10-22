# Local market pricing - phase 3

This phase applies village price multipliers to NPC-generated offers. It uses successful purchases over time, stock coverage, and outstanding affordable shortages. It preserves the existing command pipeline and the normal Program.cs simulation workflow.

## JSON policy

The `market` section in `RSF.Console/Settings/economy.json` now includes:

```json
{
  "updateIntervalMinutes": 360,
  "trackingIntervalMinutes": 15,
  "salesWindowMinutes": 4320,
  "targetStockCoverageMinutes": 2880,
  "coverageTolerancePercent": 25,
  "minimumPriceMultiplier": 0.5,
  "maximumPriceMultiplier": 2.0,
  "maximumChangePercentPerUpdate": 10,
  "initialPriceMultipliers": {
    "item:rice_grain": 1.0
  }
}
```

With RSF's 1,440-minute game day, this means a three-day sales window and a two-day stock target. The first price check waits one full adjustment interval after market observation begins. Later checks run once per interval, without repeatedly catching up on the same snapshot. Stock snapshots still refresh every 15 game minutes.

Coverage and window lengths must be positive. Tolerance must be finite and within `[0, 100)`. Validation reports `economy.json` when file settings are invalid. Older JSON files can omit these new fields and use the defaults.

## How prices move

For one commodity or substitute category:

1. Count completed purchase quantities inside the rolling window.
2. Divide by the observed minutes, capped at the window length, to obtain a purchase rate. During startup this uses the actual observed time, not an imaginary full three-day history.
3. Multiply that rate by `targetStockCoverageMinutes` to calculate target stock.
4. Keep the multiplier unchanged when offered stock is within the tolerance band around the target.
5. Otherwise calculate pressure as `(target stock - offered stock) / max(target stock, offered stock, 1)`.
6. Calculate unmet affordable requests as `max(0, outstanding requests - offered stock)`. If positive, their shortage pressure, `unmet / max(outstanding requests, 1)`, can override a downward history signal.
7. Apply `current multiplier * (1 + pressure * maximum change percent / 100)`, clamped to the configured multiplier bounds.

Example: villagers buy 10 meals per day, so the two-day target is 20 meals. With 25% tolerance, 15-25 meals is stable. Five meals produces upward pressure; 60 produces downward pressure. The presence of stock and absence of waiting buyers alone no longer pushes healthy stock coverage downward.

When there are no recent sales and no unmet affordable requests, the current multiplier stays unchanged. Opening stock alone is not evidence that prices should collapse. Initially this preserves configured starting prices; after earlier adjustments it preserves the last multiplier rather than inventing a new signal.

Current requests are not added to rolling sales as if they were completed purchases. Successful purchases immediately reduce matching outstanding requests. The next normal snapshot then recalculates needs from actual inventory. This prevents a fulfilled request from also remaining unmet demand in the same price check.

## Categories and units

Food uses `category:food`, bait uses `category:bait`, and whole fish use `category:whole_fish`. Individual base prices are preserved: bread and meals can still have different base prices, and each fish retains its species rate per 500 grams.

Category and individual totals are alternative views, not quantities to add together. Buying a 1,000-gram carp records 1,000 grams for `fish:Carp` and one purchase for `category:whole_fish`. That category uses counts consistently for supply, requests, and purchases. Species meat stays in grams. Price checks reject signals with inconsistent quantity units.

The tracker and successful-trade recorder remember observed item/category relationships, so meals supplied by individual NPC recipe overrides also use general food pressure. A meal that restores both hunger and health uses food pressure once for its quote.

Initial multipliers can also name supported categories, for example `category:food`. For category members, a specific starting offset combines with the category factor once, with the final result clamped to the local market bounds. For example, a carp offset of 1.2 and a whole-fish factor of 1.1 give 1.32. Category members do not also receive a separate automatic adjustment from the same sales data.

Food, bait, and fish are grouped deliberately in this phase. Species-specific or meal-specific popularity is not inferred from a general request. Other commodities, such as seeds, spice, vegetables, grain, and meat, keep their own price signals.

## Successful trades and bounded history

`TradeSystem.Buy` still validates funds, ownership, stock, and location, then transfers items and gold through existing mechanisms. Only a successful transfer calls `MarketSales.Record`. Failed purchases do not create sales. A partial purchase of 3 grains from a 10-grain stack records 3, not 10.

`MarketActivityComponent` holds a `Queue<MarketSaleRecord>` and running totals. A queue removes the oldest entries first. Pruning subtracts each expired trade from the totals exactly once. This avoids rescanning lifetime history for every price calculation and bounds retained history to the sales window plus the normal refresh delay. Markets have independent queues and dictionaries.

The history measures completed local trades, not consumption, profit, production, or external demand. These are distinct quantities and are not estimated here.

## Automatic offers and rounding

Existing AI offer paths still dispatch `OfferItemCommand`. They first obtain a local quote, then mark a successful NPC offer with `AutomaticMarketPriceComponent`, which stores its original base price and owner.

After a market multiplier changes, `MarketPriceSystem` refreshes matching valid automatic listings from that original base. It does not multiply the previous rounded quote again. Buying a whole automatic item removes its listing marker; partial purchases leave the remaining seller stack listed.

Explicit manual prices remain explicit. `TradeSystem.Offer` removes an old automatic marker, and an out-of-band direct price change is detected before automatic refresh. A manually offered loaf at 7 gold therefore stays at 7 gold until explicitly changed.

Whole fish calculate `weight grams * species rate / 500`, apply the local multiplier, then round upward once. Other prices also round upward to positive whole gold. Quotes use `decimal` arithmetic for the final multiplication so binary floating-point tails do not add a spurious extra gold. Money remains integer gold: a small multiplier change may not change a cheap item's visible price, and prices cannot fall below one gold per trade unit. Fractional money is outside this phase.

When stock is absent, the demand tracker uses current local quotes for its configured reference prices, including category factors and species-specific starting offsets. Actual posted prices still take precedence. These references estimate affordability; they do not create goods or guarantee that a future fish will have the minimum configured weight.

## Integration and changed files

New files:

- `RSF.Core/Components/AutomaticMarketPriceComponent.cs`: original NPC offer data.
- `RSF.Core/Systems/MarketPricing.cs`: shared identities, categories, local quotes, and automatic-offer marking.
- `RSF.Core/Systems/MarketSales.cs`: successful trade history and expiry.
- `RSF.Core/Systems/MarketPriceSystem.cs`: coverage calculations and automatic listing refresh.

Updated files:

- `Components/MarketActivityComponent.cs`: rolling purchase data and observed category relationships.
- `Components/MarketPriceComponent.cs`: coverage policy and evaluation timestamp.
- `Configuration/MarketSettings.cs` and `GameSettingsLoader.cs`: settings and strict validation.
- `Systems/TradeSystem.cs`: successful-sale recording and automatic-marker cleanup.
- `Systems/MarketTrackingSystem.cs`: shared identities, history pruning, and locally quoted reference prices.
- `Systems/FishPricing.cs`: exact weight price before final rounding.
- `Systems/VillageAIExecution.cs` and `AIWorkExecution.cs`: local NPC offers through existing commands.
- `World/SimulationSetup.cs`: price system runs after market tracking and AI execution.
- `Debugging/ComponentDebugger.cs`: coverage settings, timestamps, and recent purchase totals.
- `RSF.Console/Settings/economy.json`: configurable window, target, and tolerance.

No changes to `Program.cs` or to the ECS, command, or scheduling architecture.

## C# concepts

A record stores related values with meaningful names. The `with` syntax makes a replacement value while preserving unchanged fields, such as the original base price when only the last quoted price changes. Dictionaries provide one running total per commodity. The queue preserves chronological expiry order. `Math.Clamp` enforces price bounds. `decimal` handles final monetary calculations, while the simulation's existing integer gold balances remain unchanged.

## Validation

The console project builds with 0 errors and the same 20 existing nullable warnings. Temporary check projects verify coverage examples, stable boundaries, startup, bounded changes, rolling expiry, partial transfers, fulfilled requests, category deduplication, custom meals, manual price preservation, fish rounding, independent markets, and JSON errors. The archived phase-1 debugger wording assertion was updated in its temporary copy because prices are now active; original check sources were preserved.


Final results: **375 checks passed** (73 new coverage/sales checks plus 302 market, tracking, map, survival AI, profiler, and processing regression checks). The unchanged 50-day console run with `--profile` completed in 17.47 seconds in this sample. `MarketPriceSystem` used 68.72 ms total and `MarketTrackingSystem` used 278.27 ms. Successful-trade recording runs inside the existing command execution path, so its cost is included there. This is a small-village integration sample, not a 400-NPC capacity claim.

## Remaining balance issue

The integration sample reached the 2x price cap for several commodities and all three original residents eventually starved. A separate run of the previous fixed-price build also lost one resident. These runs are random and have different trade/fishing outcomes; they do not establish a controlled causal comparison. They show that long-term village survival remains unresolved, and the new settings should not be presented as a balanced economy.

One concrete configuration tension is that the cook's current `mealStockTarget` is six meals, while a two-day market coverage target can call for more than six meals. Regular sales can therefore keep coverage below target even while the cook follows its existing inventory cap. Adjusting production targets and coverage policy together is the next balancing step. Existing meal recipes, NPC opening supplies, production rules, and AI stock targets were preserved in this implementation.
