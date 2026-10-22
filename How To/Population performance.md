# NPC population profiling

Each run advances 1,440 game minutes on the existing 150 x 150 map using the
current optimized systems. These are full simulation runs, not isolated loops.
Production C# code, Program.cs, and JSON files were not changed. Only this report
and the raw profile report were added to the repository.

| NPCs | Setup seconds | Simulation seconds | AI decisions seconds | AI share | Dead | Zero hunger | No home |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 0.318 | 1.409 | 0.839 | 59.8% | 0 | 0 | 0 |
| 50 | 0.743 | 2.998 | 2.157 | 72.1% | 0 | 0 | 46 |
| 100 | 1.099 | 5.062 | 3.921 | 77.5% | 0 | 0 | 96 |
| 200 | 1.967 | 13.689 | 11.931 | 87.2% | 0 | 0 | 196 |
| 400 | 2.926 | 61.106 | 58.258 | 95.4% | 6 | 62 | 396 |

The 400-NPC run took 61.106 seconds, with 58.258 seconds (95.4%) inside
AIDecisionSystem. Average AI update time was 40.457 ms and the maximum observed
AI update was 329.963 ms. These are per-game-minute simulation updates, not
rendering frame rates. Doubling 200 to 400 NPCs increased total measured runtime
about 4.46 times in these samples; performance is not scaling linearly under
this workload.

## Method

- Debug build, .NET 10, fresh process/world for each population, run sequentially.
- Same repository JSON loaded for every run. Additional NPC profiles were cloned
  in memory from the three existing profiles in rotation; only names changed.
  Original profile values and per-profile settings were retained. Files stayed
  untouched. Jobs at 400: 134 farmers, 133 fishers, 133 cooks.
- Existing SimulationSetup.Configure, VillageBlueprint, NpcBlueprint, and
  simulation.Update were used. Updates were called in 60-minute chunks, with
  progress output every 240 minutes.
- Setup time includes settings loading, map creation, and spawning; simulation
  time excludes setup. Profiler was enabled only for updates. Normal gameplay
  Console output was redirected to TextWriter.Null; messages were still formatted.
- Each population has one sample, including cold/JIT effects, random starting
  stats, catches, and cooking. Gameplay random sources were not globally seeded.
  These are diagnostic observations, not statistically controlled capacity limits.
- The temporary profiling app and referenced Core DLL were built successfully,
  with zero errors and zero warnings reported by those incremental builds.
  No gameplay changes required a new regression run. All five population runs
  completed without an exception.
- File hashes confirmed Program.cs and every Settings JSON file stayed unchanged.

## Resource limits and interpretation

The map was not expanded to feed or house the added population. It still has
four whole houses, 216 farmland tiles, and one inn kitchen. Only the first cook
owns the inn and receives cooking capability; assigning Cook to more NPCs does
not create additional kitchens. At 400 NPCs there was one cook with CookingSkill,
396 NPCs without HomeComponent, 216 assigned farmland tiles, and 214 planted crops.
This is a crowded village stress scenario, not 400 fully equipped independent
workers or a balanced future settlement. It also excludes the proposed additional
200 travelers. No-home counts do not measure available inn entitlement.

At 400 NPCs, six were dead and 62 had zero hunger at the end. The zero-hunger count
includes dead NPCs, so these columns must not be added together. There were 57 NPCs
with SeekFood set. At 200 NPCs there were no deaths or zero-hunger NPCs, but 59
still had SeekFood set. These endpoint values do not prove long-term food security.
The run lasted only one day; normal infrequent merchant visits were not observed.
No extra food, gold, buildings, or survival overrides were injected.

Resource shortages change decisions, retries, and available work. They contribute
to this workload, so the timings cannot be extrapolated to a well-provisioned
400-person village or used to derive a universal maximum population.

## Next focused development step

Profile the expensive parts inside AIDecisionSystem before changing its rules.
The measured bottleneck is the AI system as a whole; this run does not isolate
individual decision helpers. Source inspection shows candidate searches over
inventory owners, sellers' items, owned plots, and processing hosts repeated for
individual NPCs. These are likely scaling contributors, not separately measured
conclusions.

A small optional diagnostic breakdown should first distinguish shared snapshot
construction from survival food searches and job decisions. Then optimize the
largest measured search using ordered candidate lists grouped for the current
AI update. Preserve existing tie order, score calculations, urgent overrides,
retry times, live validation, and command execution. Reuse existing component
indexes and per-update snapshot; avoid a new AI framework or global stale stock
cache. Repeat this same 400-NPC workload and seeded behavior comparisons before
claiming improvement. Housing and food capacity should remain separate features.
