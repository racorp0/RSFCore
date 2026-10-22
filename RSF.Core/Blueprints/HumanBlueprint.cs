using RSF.Core.Configuration;

using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
using System.Xml.Linq;

namespace RSF.Core.Blueprints;

public sealed class HumanBlueprint
{
    private readonly Random _random = new();

    /// <summary>Creates a human using configured attribute ranges, hunger, health calculation, and starting gold.</summary>
    /// <param name="world">The world that owns the human.</param>
    /// <param name="name">The human's display name.</param>
    /// <param name="x">The starting horizontal position.</param>
    /// <param name="y">The starting vertical position.</param>
    /// <returns>The newly registered human entity.</returns>
    public Entity Spawn(
        World world,
        string name,
        float x,
        float y)
    {
        var settings = GameSettings.Find(world)?.Npcs.FirstOrDefault(n => n.Name == name) ?? GameSettings.Find(world)?.NpcDefaults ?? new NpcSettings();
        // 1. Create a new entity.
        Entity human = world.CreateEntity();

        // 2. Generate random attributes.
        int vitality = _random.Next(settings.MinimumVitality, settings.MaximumVitality + 1);
        int strength = _random.Next(settings.MinimumStrength, settings.MaximumStrength + 1);

        // 3. Calculate initial health.
        int maxHealth = settings.BaseHealth + vitality * settings.HealthPerVitality;

        // 4. Add components.
        world.Add(
            human,
            new NameComponent(name));

        world.Add(
            human,
            new PositionComponent(x, y));

        world.Add(
            human,
            new VitalityComponent(vitality));

        world.Add(
            human,
            new StrengthComponent(strength));

        world.Add(
            human,
            new HealthComponent(maxHealth, maxHealth));

        world.Add(
            human,
            new HungerComponent(settings.Hunger, settings.MaximumHunger));

        world.Add(
            human,
            new InventoryComponent());

        world.Add(
            human,
            new EquipmentComponent());

        // Gold is separate from inventory so direct trades can transfer whole coins.
        world.Add(human, new GoldComponent(settings.Gold));

        return human;
    }
}
