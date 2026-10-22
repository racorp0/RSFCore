# Local market price foundation

Each village's existing market entity now has MarketPriceComponent, containing
its own multiplier dictionary and the configured adjustment policy. This phase
does not change asking prices, transfers, gold, AI scores, or commands. No market
update system is registered yet. Supply/demand tracking and price adjustment are
later phases, followed by applying those prices to sale offers.

Configure the new `market` section of `RSF.Console/Settings/economy.json`:

```json
"market": {
  "updateIntervalMinutes": 360,
  "minimumPriceMultiplier": 0.5,
  "maximumPriceMultiplier": 2.0,
  "maximumChangePercentPerUpdate": 10,
  "initialPriceMultipliers": {
    "item:rice_grain": 1.0,
    "fish:Carp": 1.25,
    "seed:rice": 0.8,
    "bait:worm": 1.1
  }
}
```

The checked-in file starts only rice grain at 1.0. Other commodities also start
conceptually at 1.0; they need no explicit dictionary entry. A multiplier of
1.25 represents 25% above base, while 0.8 represents 20% below base. At this phase
these are stored values only, not actual applied sale prices. LastPriceUpdateAt
remains null until a future system performs an adjustment. The 360-minute interval
and 10% limit are stored settings, not running behavior.

Commodity references use exact existing IDs/names:

| Prefix | Reference | Example |
| --- | --- | --- |
| item | Existing product or configured meal output ID | item:rice_grain |
| item | Existing built-in bread or spice supply | item:bread, item:spice |
| seed | Plant ID from farming.json | seed:rice |
| bait | Bait ID from fishing.json | bait:worm |
| fish | Species name from fishing.json | fish:Carp |

These references qualify existing commodity identities; they do not replace
EntityId or add another item registry. Whole fish and seeds must remain distinct
from processed outputs. Existing base prices and price units stay in their current
definitions, including fish rates per 500 grams and meat stack quantities in grams.
This phase performs no currency rounding or unit conversion.

The loader validates known references, finite positive multipliers, positive
update intervals, and a maximum change percent within (0, 100]. Bounds must
include 1.0 so omitted commodities can have neutral values. Starting values must
lie inside those bounds. Errors identify economy.json. Existing economy files
without a market section retain default neutral market settings; explicit null
market settings are rejected.

## Existing RSF flow

GameSettingsLoader reads and validates EconomySettings.Market. VillageBlueprint
creates a fresh MarketPriceComponent and attaches it to the existing market tile.
Its constructor copies values into a new dictionary, so runtime changes never
change loaded starting values or another market's state. Program.cs does not
need to initialize a new service or control prices.

The existing optional blueprint arguments remain compatible. Manual callers can
supply the new optional marketSettings argument to override local starting data.
Manual settings validate policy/reference syntax; JSON loading additionally checks
references against its loaded definitions. A manual override should use the same
known commodity references. The blueprint still permits one village map per World;
multi-village map generation was not restructured. Independence was demonstrated
with separate worlds using the same loaded settings and a local override.

WorldDebugger's usual village dump includes its related market data automatically.
Dumping the market entity directly also shows that component. No recurring terrain
scan or per-minute price calculation was added.

## C# concepts

MarketSettings is configuration data with init properties and a validation method.
MarketPriceComponent is runtime data attached through World.Add, following existing
component storage. Dictionary<string, double> maps a commodity reference to its
local multiplier. Copying starting entries creates independent dictionaries;
assigning the original dictionary would incorrectly share state between markets.
The optional marketSettings parameter preserves existing blueprint calls.

## Validation and scope

- 41 focused checks passed: settings/ranges, known and unknown references, invalid
  JSON with filenames, defaults for older JSON, independent markets/settings,
  local overrides, failure before map creation, debugger output, and unchanged
  direct trade prices/payments even with a non-neutral multiplier.
- Existing 95 map/gameplay, 27 survival AI, 20 profiler, and 70 shared processing
  checks passed: 253 total checks including the focused checks.
- The actual unchanged Console entry point completed its current 50-day loop and
  printed market data in the normal village dump.
- Console build passed with zero errors and 20 existing nullable warnings on
  compilation. Build output was isolated in a temporary directory. Program.cs
  was not edited, no existing tests were removed, and no packages were added.

Files: new Configuration/MarketSettings.cs and Components/MarketPriceComponent.cs;
updated EconomySettings.cs, GameSettingsLoader.cs, VillageBlueprint.cs,
ComponentDebugger.cs, and economy.json; this guide.

Next phase: measure sale supply and outstanding purchase demand per commodity and
market. An NPC's repeated decisions must update one outstanding request rather
than repeatedly add new demand. Prices should remain unchanged until that tracking
is validated, followed by gradual adjustment and integration into existing offers.
