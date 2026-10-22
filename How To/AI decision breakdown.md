# AI decision cost breakdown

The primary measured bottleneck is searching sellers' inventories for food.
This phase only measured behavior. No production C#, Program.cs, JSON, scoring,
schedule, command, or gameplay rule was changed.

A temporary copy of RSF.Core was instrumented with disposable stopwatch scopes.
The existing population workflow advanced 400 NPCs for 1,440 game minutes on the
unchanged 150 x 150 map, with regular gameplay console output suppressed.
Simulation time: 67.766 seconds; AI update time: 64.637 seconds (95.4%).
Setup took 3.387 seconds and was excluded. Non-overlapping AI sections:

| Section | Calls | Seconds | Share of AI time |
| --- | ---: | ---: | ---: |
| Food search total | 77,889 | 42.314 | 65.5% |
| Village decisions total | 165,830 | 13.082 | 20.2% |
| Job decisions total | 165,830 | 5.933 | 9.2% |
| Snapshot construction | 1,069 | 1.472 | 2.3% |
| Healing search total | 68 | 0.027 | 0.0% |

Other AI work and timing overhead account for the remaining 1.810 seconds (2.8%).
Nested timings, already INCLUDED in their parent totals:

- Food sellers: 42.131 seconds across 77,889 searches, about 99.6% of food-helper time.
- Village supply sellers: 12.033 seconds inside village decisions.
- Cook job branch: 5.262 seconds inside job decisions.
- Processing planning: 0.581 seconds inside village decisions.
- Farmer job branch: 0.425 seconds inside job decisions.
- Healing sellers: 0.027 seconds inside healing decisions.

Do not add nested timings to parent totals. Timings include probe overhead;
these are elapsed measurements, not precise CPU samples. Calls are searches,
not unique buyers or successful trades. Empty-result searches still cost time.

## Interpretation

AIDecisionSystem.AddCandidates scans snapshot.InventoryOwners and their stock
for every NPC searching for food/healing. Most stock is not useful consumable
food. Repeating that search for many NPCs is expensive. Moving scores into JSON
would leave the repeated searching intact.

This is not an optimized before/after comparison. It differs from the earlier
61.106-second uninstrumented run because probes add overhead and starting stats,
catches, and gameplay are not globally seeded. The crowded map still has four
homes and one usable cook. This run ended with eight deaths and 62 zero-hunger
NPCs, including dead NPCs; those counts must not be added together. It does not
represent a balanced 400-worker economy or 200 additional travelers.

## Proposed next focused change

Optimize survival-offer collection only in AIDecisionSnapshot/AIDecisionSystem,
with a small data record if required by current conventions. Build ordered food
and healing offer lists lazily once per AI update. Preserve seller world order,
inventory order, and duplicates exactly. Reuse AISurvivalRules and component
indexes instead of introducing another stock system.

Each NPC must still apply live seller/item validation, affordability, overflow,
carried-item exclusion, failed targets, urgency, configured scores, continuation
bonus, and target stability. Keep carried-item searches and existing commands.
Discard lists after that AI update so later stock, price, ownership, equipment,
and health changes are visible. Do not alter job/village decisions or timing.

This targets the measured 42-second section first, without promising to remove
all of it or the remaining AI cost. Validate seeded decisions and tie order,
then repeat the same population workload. Village supply searches should be a
separate later change if still expensive. No external AI model is required.

## Validation and artifacts

- Temporary Core/app build: zero errors, 20 existing nullable warnings in copied Core.
- All 2,075 existing AI state and tie-order comparisons passed using that copy.
- The 400-NPC run completed without an exception.
- Production Console build: zero errors, zero warnings in the incremental build.
- File hashes confirmed Program.cs and all Settings JSON files stayed unchanged.
- Only this report and AI decision breakdown profile.txt were added to the repo.
  The probes and alternate app exist only in temporary files.
