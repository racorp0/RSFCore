# Shared AI survival offers

Food and healing searches now share ordered offer lists within the existing
AIDecisionSnapshot. Lists are built lazily only when a due NPC searches that
category, and discarded when the AI update ends. No JSON, scores, schedules,
command handlers, or Program.cs changed.

## Code and data flow

- AIDecisionSnapshot.GetSurvivalOffers visits sellers in the snapshot's existing
  world order and items in inventory order. It retains positive-price items
  accepted by AISurvivalRules for the requested benefit. Duplicates are retained.
- AISurvivalOffer is a small readonly record struct pairing one seller with its
  ordered usable entries. Grouping avoids repeating seller checks for every item.
- AIDecisionSystem still scores carried items first. Sale candidates retain the
  existing buyer funds, seller health/death/gold, overflow, inventory presence,
  carried-item exclusion, typed-stack exclusions, equipment, failed-target,
  continuation, urgency, and point calculations. Prices remain live components.
- Item usability is checked once per category/update for sale entries. Carried
  items still use the existing direct checks. The optional knownUsable argument
  only bypasses a redundant item-only check for entries validated by this snapshot.
  It does not bypass buyer checks or change scoring.
- Current AI decisions do not spend inventory or change item effects. Commands
  execute after decision evaluation, so those item-only results stay valid for
  this pass. Execution still calls AISurvivalRules.CanUse and follows the existing
  command validation. The next update rebuilds lists from new stock/effects.

The C# record struct contains data, not another trading system. IReadOnlyList
exposes ordered entries without offering mutation through that interface. A null
cached list means it has not been built; an empty list means it was built and no
offers exist, so later NPCs do not repeat that empty search.

This is a per-update optimization for the existing single-thread lifecycle.
Future decision code that changes inventories, offer membership, or item effects
inside the AI pass would require revisiting this assumption. It is not a global
stock cache, a new event mechanism, or an external AI model.

## Validation

- 2,967 checks against the exact previous implementation passed: the existing
  2,075 AI state/tie-order checks, 160 complete ordered candidate-list comparisons,
  and additional state comparisons before/after changes to prices, food effects,
  inventory membership, equipment, sale markers, and destroyed items.
- Candidate-list cases include duplicates, reverse inventory attachment, missing
  consumables, destroyed entries, carried-item aliases, zero/negative prices,
  insufficient funds, seller overflow/negative gold, dead/zero-health sellers,
  equipment exclusions, and typed seed/bait/produce exclusions.
- Existing 95 map/gameplay, 27 survival AI, and 20 profiler checks passed: 3,109
  total checks across the comparison and regression runs.
- Console build passed with zero errors and 20 existing nullable warnings on
  compilation. Temporary OutputPath avoids disturbing running Console processes.
- Settings JSON hashes matched the earlier population baseline. Program.cs now
  contains the user's 50-day loop rather than the earlier five-day loop; that edit
  was preserved. No agent edit was made to Program.cs. The separate profiling app
  still measured one day. No tests were removed, and comparison/benchmark programs
  remained temporary.

## Measurements

Controlled Debug fixture: 400 hungry AI actors, 22,500 terrain entities, 8,000
non-food inventory items, and one food offer. Eight due decision updates per run,
three alternating original/updated pairs after warm-up, resetting decision state
outside timing. No commands or random gameplay ran. Median:

| Version | Milliseconds |
| --- | ---: |
| Previous stock scans | 2,417.793 |
| Shared survival offers | 41.593 |

That is about 58 times faster for this deliberately sparse-food search fixture.
Targets/sellers matched. It is not a 58-times full-village speedup or a benchmark
of many available food offers.

The existing 400-NPC, 150 x 150 village workflow was also repeated for 1,440 game
minutes, with gameplay Console output suppressed and setup measured separately:

| Sample | Whole simulation seconds | AI decision seconds |
| --- | ---: | ---: |
| Previous production code | 61.106 | 58.258 |
| Final shared-offer code | 37.762 | 34.845 |

Setup for the final run took 3.089 seconds. These are separate unseeded samples,
including runtime/gameplay variation, not a controlled causal speedup estimate.
The final sample was about 38% shorter, but AI still consumed 92.3% of measured
system time. The crowded map still had four homes and one usable cook: four NPCs
were dead and 62 had zero hunger at the end, including dead NPCs. This does not
establish that the current economy can support 400 people.

An intermediate flat-offer version was measured and then refined before completion.
Its temporary diagnostic run showed food searches still spending 32.824 seconds,
while list construction cost only 0.214 seconds. The final grouped version also
reuses item-only usability checks. Those intermediate diagnostic numbers are not
the final code's subsection profile.

## Remaining work

Village supply seller scans and cook ingredient searches are unchanged. Earlier
diagnostics measured about 12 seconds and 5 seconds in those nested sections.
Food-heavy stock can also still generate many individual candidates. A fresh
breakdown is needed before deciding the next largest target. Optimize one search
at a time while preserving seller/item order and configured scores.

Code files changed: AIDecisionSnapshot.cs, AIDecisionSystem.cs, and new
AISurvivalOffer.cs. Documentation: this report and AI survival offers profile.txt.
