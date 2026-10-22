# NPC transaction history

The usual `WorldDebugger.DumpEntity` output now includes each NPC's completed purchases, sales, and inn payments. `Program.cs` is unchanged. Run the console simulation as usual and inspect the history beneath each resident. The village dump includes an archive with both participants named, including merchants that have already left.

Example output:

```text
Transaction history: 1 retained / 1 recorded
    Recorded income: 0 gold; spending: 20 gold; net: -20 gold
    [Year 1, Spring, Day 2, 01:00; minute 1500] Bought from Haruto
        Carp: 1 unit(s) (1000 g); unit price: 20 gold/unit(s); total: 20 gold
        Gold: Ren 200 -> 180; Haruto 100 -> 120
```

The seller's copy says `Sold to Ren`. Rent uses `Paid rent to` and `Received rent from`. A rent entry represents one daily payment, including automatic renewals.

## Settings

In `RSF.Console/Settings/economy.json`:

```json
"transactionHistory": {
  "enabled": true,
  "maximumEntries": 2000
}
```

`enabled` controls recording. It does not change prices, gold transfers, stock, AI choices, or the existing rolling market-sales history. JSON settings are loaded at startup, as before.

`maximumEntries` must be positive. It limits the newest records kept in each NPC history and in each village archive. Old records are removed first, and the output explicitly reports the number removed. Recorded income and spending totals still include those older entries. The same immutable transaction record is shared by the two participants and their village, avoiding three copies of its names and item details.

Omitting this settings object uses enabled recording with a 2,000-entry limit. An explicitly null object or nonpositive limit is rejected with an error naming `economy.json`.

## What is recorded

- Exact game date, time, and minute when a payment completes.
- Both existing entity IDs and names at payment time.
- Item name, including meal quality when applicable.
- Actual transferred quantity. Partial stack purchases record the purchased part, not the seller's entire stack.
- Whole fish weight separately from its quantity of one fish. Prepared fish meat uses grams as the price/quantity unit.
- Actual unit price and total payment.
- Both balances immediately before and after payment.
- Initial inn rent and successful automatic rent renewals.

Names, quality, weight, and amounts are snapshots. Consuming an item, changing a name, splitting another stack, or removing a departing merchant cannot rewrite the recorded information. Death leaves an NPC's existing components intact, including its history.

The village archive keeps its own bounded set of records after a merchant's personal history is destroyed on departure. Manually assembled worlds without a registered village still get participant histories. Worlds without an existing food clock or village time use the existing `FoodRules.Now` fallback of minute zero; normal `SimulationSetup.Configure` uses the shared simulation clock.

## How to investigate a death

Compare the NPC's current `GoldComponent`, hunger, inventory freshness, and AI result with its last food purchases and recorded income/spending.

In the validation run, the 50-day simulation ended with all four residents dead, but their balances remained positive:

| NPC | Recorded income | Recorded spending | Final gold |
| --- | ---: | ---: | ---: |
| Minami | 0 | 87 | 113 |
| Haruto | 237 | 66 | 371 |
| Aoi | 145 | 196 | 149 |
| Ren | 57 | 90 | 167 |

Each started with 200 gold, so these totals reconcile with the final balances. This run does not support depleted gold as the direct explanation for their deaths. The existing debugger also reported spoiled ingredients and blocked needs. Availability, food production, and action eligibility still need investigation; transaction history alone cannot distinguish those causes. Fishing, meal quality, visitor timing, and health initialization remain random, so another run can produce different totals.

This feature records completed payments only. It does not record failed purchase attempts, item use, spoilage events, hunger ticks, or an explicit cause of death. Starting gold and direct setup changes to `GoldComponent` are not payments and are not logged. No additional per-minute notifications or logging systems were added. Histories are in-memory diagnostics printed at the end, not an automatically saved transaction database.

## Changes and C# concepts

New files:

- `Configuration/TransactionHistorySettings.cs`: diagnostic settings and retention validation.
- `Components/TransactionHistoryComponent.cs`: payment kind, immutable `TransactionRecord`, and history data.
- `Systems/TransactionHistory.cs`: copies completed payment details into participant histories and the village archive.
- `Debugging/TransactionHistoryDebugger.cs`: formats each participant's viewpoint and both gold balances.

Updated files:

- `TradeSystem.cs`: calls the history helper after the existing successful stock and gold transfer.
- `InnSystem.cs`: calls the helper after an actual daily rent transfer. Already-paid stays and failed renewals create no payment record.
- `EconomySettings.cs`, `GameSettingsLoader.cs`, and `economy.json`: configure and validate history retention.
- `ComponentDebugger.cs`: displays the new component through the existing debugger path.

A `record` groups the saved payment values and prevents ordinary property assignments from altering them later. Saving strings and numbers rather than live item references keeps historical facts readable after those entities disappear.

A `Queue<TransactionRecord>` stores entries in completion order. `Enqueue` adds a new record; `Dequeue` removes the oldest when the configured limit is exceeded. This bounds retained diagnostic data without changing gameplay.

The history component holds data. Trade and inn systems remain responsible for validating and transferring money. Their existing early `return false` paths occur before history recording, so rejected payments do not create misleading entries.

## Validation

`dotnet build RSF.Console/RSF.Console.csproj --no-restore` completed with zero errors and 20 existing nullable-reference warnings. A temporary output directory avoided the previously locked console executable.

52 focused transaction checks passed, covering successful and rejected purchases, both balances, weight and quantity units, partial stacks, saved names and meal quality, rent and renewal, retention and lifetime totals, disabled recording, departure archive persistence, history after death, and invalid JSON limits. All 562 existing checks also passed: 614 checks in total.

The existing 50-day console simulation completed with history enabled. The example balances above come from that run. `Program.cs`, starting supplies and gold, food lifetimes, prices, and AI decisions were not changed by this feature.
