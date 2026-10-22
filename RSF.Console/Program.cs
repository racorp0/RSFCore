using RSF.Core.Configuration;
using RSF.Core.Blueprints;
using RSF.Core.Common;
using RSF.Core.Debugging;
using RSF.Core.Simulation;

// Load once before setup. Errors identify the file instead of silently using fallback values.
GameSettings settings;
try
{
    settings = GameSettingsLoader.Load(Path.Combine(AppContext.BaseDirectory, "Settings"));
}
catch (InvalidDataException error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
    return;
}
Simulation simulation = new();
// Opt-in diagnostics print one summary after the normal run; the simulation workflow is unchanged.
simulation.Profiler.Enabled = args.Contains("--profile");
SimulationSetup.Configure(simulation, settings);

// This larger village includes the river, market, inn, entrance, and space for future buildings.
var village = VillageBlueprint.Create(simulation.World);

NpcBlueprint npcs = new();
// Profiles describe starting data only; existing AI systems decide every later action.
var residents = settings.Npcs.Select(profile => npcs.Spawn(simulation.World,
    profile.Name, profile.X, profile.Y, profile.Job, profile.LifeStage)).ToList();

// Advancing the world is the only simulation control here. NPCs choose their own actions.
// Opening bread starts sales; later meals depend on actual fishing, harvests, and ingredients.
// All stocks and gold are finite; longer economic experiments may need larger opening supplies.
// Try 15 days to observe infrequent visiting merchants; one day is a quick initial run.
for (int i = 0; i < 50; i++)
{
    simulation.Update(GameDuration.FromDays(1).Minutes);
}

Console.WriteLine($"World time: {simulation.Clock.CurrentMinute.ToGameDate()}, {simulation.Clock.CurrentMinute.ToGameTime()}");
foreach (var resident in residents)
    Console.WriteLine(WorldDebugger.DumpEntity(simulation.World, resident));
Console.WriteLine(WorldDebugger.DumpEntity(simulation.World, village));

if (simulation.Profiler.Enabled)
    Console.WriteLine(simulation.Profiler.Dump());
