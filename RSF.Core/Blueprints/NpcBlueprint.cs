using RSF.Core.Configuration;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
using RSF.Core.Systems;
namespace RSF.Core.Blueprints;

/// <summary>Composes an AI NPC from HumanBlueprint, starting job supplies, and configurable schedules.</summary>
public sealed class NpcBlueprint
{
    /// <summary>Spawns configured supplies, recipe knowledge, and housing while keeping skill and private kitchen access job-specific.</summary>
    /// <param name="world">The world containing the map and river.</param>
    /// <param name="name">The NPC's name.</param>
    /// <param name="x">The starting horizontal position.</param>
    /// <param name="y">The starting vertical position.</param>
    /// <param name="job">The initially assigned job.</param>
    /// <param name="lifeStage">The manually assigned life stage.</param>
    /// <param name="needs">Optional personal survival thresholds.</param>
    /// <param name="ai">Optional personal decision settings.</param>
    /// <param name="work">Optional personal work scores and trading targets.</param>
    /// <param name="schedule">Optional personal daily preferences.</param>
    /// <param name="initialBait">Bait quantity for a fisher.</param>
    /// <param name="initialBread">Initial bread stock for the future cook.</param>
    /// <param name="farmPlots">Maximum unowned farmland tiles to assign to a farmer.</param>
    /// <param name="initialSpices">The cook's opening spice supply.</param>
    /// <param name="cooking">Optional recipes and meal-quality settings.</param>
    /// <param name="sleep">Optional fatigue and bedtime settings.</param>
    /// <returns>The configured NPC; its AI decides all later gameplay actions.</returns>
    public Entity Spawn(World world, string name, float x, float y, Job job,
        LifeStage lifeStage = LifeStage.Adult, NeedsSettingsComponent? needs = null,
        AISettingsComponent? ai = null, AIWorkSettingsComponent? work = null,
        ScheduleTemplate? schedule = null, int? initialBait = null,
        int? initialBread = null, int? farmPlots = null, int? initialSpices = null,
        CookingSettingsComponent? cooking = null, SleepSettingsComponent? sleep = null)
    {
        var settings = GameSettings.Find(world);
        var profile = settings?.Npcs.FirstOrDefault(n => n.Name == name) ?? settings?.NpcDefaults ?? new NpcSettings();
        initialBait ??= profile.Bait; initialBread ??= profile.Bread;
        farmPlots ??= profile.FarmPlots; initialSpices ??= profile.Spices;
        if (initialBait < 0 || initialBread < 0 || farmPlots < 0 || initialSpices < 0)
            throw new ArgumentException("Starting supplies and plot counts cannot be negative.");
        if (!Enum.IsDefined(job) || !Enum.IsDefined(lifeStage))
            throw new ArgumentException("Choose a defined job and life stage.");
        needs ??= profile.Needs ?? settings?.Needs.Thresholds ?? new NeedsSettingsComponent();
        ai ??= profile.AI ?? settings?.AI.Decision ?? new AISettingsComponent();
        work ??= profile.Work ?? settings?.AI.Work ?? new AIWorkSettingsComponent();
        cooking ??= profile.Cooking ?? settings?.Cooking;
        sleep ??= profile.Sleep ?? settings?.Needs.Sleep;
        if (schedule is null && profile.Schedule is not null && settings is not null) schedule = settings.Schedules[profile.Schedule];
        needs.Validate(); ai.Validate(); work.Validate();
        cooking?.Validate();
        Entity npc = new HumanBlueprint().Spawn(world, name, x, y);
        // Recipe knowledge does not grant cooking skill or access to a private kitchen.
        if (cooking is not null) world.Add(npc, cooking);
        world.Add(npc, new JobComponent(job));
        world.Add(npc, new ProcessingSkillsComponent { Levels = profile.ProcessingSkills });
        foreach (string id in profile.Tools)
        {
            var tool = settings?.Processing.Tools.FirstOrDefault(t => t.Id == id);
            if (tool is null) continue;
            Entity item = world.CreateEntity();
            world.Add(item, new ItemComponent(tool.Name)); world.Add(item, new ProcessingToolComponent(id));
            new InventorySystem(world).AddItem(npc, item);
        }
        world.Add(npc, new LifeStageComponent(lifeStage));
        world.Add(npc, needs);
        world.Add(npc, ai);
        world.Add(npc, work);
        world.Add(npc, new ScheduleComponent(schedule));

        // Village membership is explicit; legacy maps keep their original remote-action behavior.
        Entity? villageRoot = world.GetEntities().FirstOrDefault(e => world.Has<VillageComponent>(e));
        if (villageRoot is not null && world.TryGet<VillageComponent>(villageRoot, out var village) && village is not null)
        {
            world.Add(npc, new VillageResidentComponent(village));
            world.Add(npc, new FatigueComponent(0));
            world.Add(npc, new FatigueClockComponent(village.CurrentMinute));
            world.Add(npc, sleep ?? new SleepSettingsComponent());
            if (job != Job.Merchant)
            {
                // Configured maps offer complete houses; legacy worlds retain their original free-tile assignment.
                Entity? house = world.GetEntities().FirstOrDefault(e => !world.Has<OwnershipComponent>(e) &&
                    (settings?.Map is not null
                        ? world.TryGet<BuildingComponent>(e, out var building) && building is not null && building.Kind == BuildingKind.House
                        : world.TryGet<MapTileComponent>(e, out var tile) && tile is not null && tile.Zone == MapZone.Building && !world.Has<BuildingComponent>(e)));
                if (house is not null)
                {
                    world.Add(house, new BuildingComponent(BuildingKind.House));
                    world.Add(house, new OwnershipComponent(npc.Id));
                    world.Add(house, new NameComponent(name + "'s House"));
                    world.Add(npc, new HomeComponent(house.Id));
                }
            }
            if (lifeStage == LifeStage.Adult && job == Job.Cook && world.TryGetEntity(village.InnId, out var inn) && inn is not null && !world.Has<OwnershipComponent>(inn))
            {
                world.Add(inn, new OwnershipComponent(npc.Id));
                world.Add(npc, new CookingSkillComponent(profile.CookingSkill));
                world.Add(npc, cooking ?? new CookingSettingsComponent());
                Entity utensils = world.CreateEntity();
                world.Add(utensils, new ItemComponent("Cooking Utensils"));
                world.Add(utensils, new CookingUtensilsComponent());
                world.Add(utensils, new ProcessingToolComponent((cooking ?? settings?.Cooking ?? new CookingSettingsComponent()).UtensilToolId));
                new InventorySystem(world).AddItem(npc, utensils);
                if (initialSpices > 0)
                {
                    Entity spice = world.CreateEntity();
                    world.Add(spice, new ItemComponent("Spices"));
                    world.Add(spice, new SpiceComponent(initialSpices.Value));
                    new InventorySystem(world).AddItem(npc, spice);
                }
            }
        }

        // Dependents have no adult work supplies or autonomous employment behavior.
        if (lifeStage != LifeStage.Adult) return npc;
        InventorySystem inventory = new(world);
        if (job == Job.Farmer)
        {
            inventory.AddItem(npc, new WateringCanBlueprint().Spawn(world));
            foreach (var plant in settings?.Farming.Plants ?? Array.Empty<PlantDefinition>())
                if (plant.StartingSeedsPerFarmer > 0) inventory.AddItem(npc, new PlantSeedBlueprint().Spawn(world, plant, plant.StartingSeedsPerFarmer));
            int assigned = 0;
            foreach (Entity tile in world.GetEntities())
            {
                if (assigned >= farmPlots) break;
                if (!world.TryGet<MapTileComponent>(tile, out var map) || map is null ||
                    map.Zone != MapZone.Farmland || world.Has<OwnershipComponent>(tile)) continue;
                world.Add(tile, new OwnershipComponent(npc.Id));
                assigned++;
            }
        }
        else if (job == Job.Fisher)
        {
            world.Add(npc, new FishingSkillComponent(profile.FishingSkill));
            inventory.AddItem(npc, new FishingRodBlueprint().Spawn(world));
            if (initialBait > 0) inventory.AddItem(npc, new BaitBlueprint().Spawn(world, initialBait.Value));
        }
        else if (job == Job.Cook)
        {
            for (int loaf = 0; loaf < initialBread; loaf++)
                inventory.AddItem(npc, new BreadBlueprint().Spawn(world));
        }
        return npc;
    }
}
