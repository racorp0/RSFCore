using RSF.Core.Configuration;
using RSF.Core.Commands;
using RSF.Core.CommandHandlers;
using RSF.Core.Systems;
namespace RSF.Core.Simulation;

/// <summary>Registers RSF's existing gameplay and AI systems so entry points only set up the world.</summary>
public static class SimulationSetup
{
    /// <summary>Preserves manual simulation setup without requiring external files.</summary>
    /// <param name="simulation">The fresh simulation to configure.</param>
    public static void Configure(Simulation simulation) => Configure(simulation, null);

    /// <summary>Configures gameplay in explicit order, then observes local markets and updates coverage-based quotes after AI commands have executed.</summary>
    /// <param name="settings">Optional JSON settings, registered before constructing systems.</param>
    /// <param name="simulation">The fresh simulation to configure before spawning NPCs.</param>
    public static void Configure(Simulation simulation, GameSettings? settings)
    {
        World world = simulation.World;
        if (settings is not null)
        {
            if (GameSettings.Find(world) is not null) throw new InvalidOperationException("Settings are already registered.");
            world.Add(world.CreateEntity(), settings);
        }
        CommandDispatcher dispatcher = new();
        MovementSystem movement = new(world);
        StatSystem stats = new(world);
        InventorySystem inventory = new(world);
        EquipmentSystem equipment = new(world, stats);
        CooldownSystem cooldown = new(world, simulation.Clock);
        ItemEffectSystem effects = new(world, simulation.Clock);
        FarmingSystem farming = new(world, simulation.Clock, inventory);
        FishingSystem fishing = new(world, simulation.Clock, inventory);
        TradeSystem trading = new(world, inventory);
        TravelSystem travel = new(world, simulation.Clock, movement);
        ProcessingSystem processing = new(world, simulation.Clock, travel);
        FoodExpirationSystem expiration = new(world, simulation.Clock);
        CookingSystem cooking = new(world, simulation.Clock, travel);
        SleepSystem sleep = new(world, simulation.Clock, travel);
        InnSystem inn = new(world, simulation.Clock);
        MerchantVisitSystem visitors = new(world, simulation.Clock);

        dispatcher.Register(new MoveCommandHandler(world, movement));
        dispatcher.Register(new PickupCommandHandler(world, inventory));
        dispatcher.Register(new DropCommandHandler(inventory, equipment));
        dispatcher.Register(new EquipCommandHandler(world, inventory, equipment));
        dispatcher.Register(new UnequipCommandHandler(equipment, world));
        dispatcher.Register(new UseItemCommandHandler(world, inventory, effects));
        dispatcher.Register(new AttackCommandHandler(world, simulation.Clock, cooldown));
        dispatcher.Register(new UseSkillCommandHandler(world, cooldown));
        dispatcher.Register(new PlowCommandHandler(farming));
        dispatcher.Register(new PlantCropCommandHandler(farming));
        dispatcher.Register(new StartProcessingCommandHandler(processing));
        dispatcher.Register(new OperateProcessingCommandHandler(processing));
        dispatcher.Register(new CollectProcessingCommandHandler(processing));
        dispatcher.Register(new PauseProcessingCommandHandler(processing));
        dispatcher.Register(new HarvestCropCommandHandler(farming));
        dispatcher.Register(new ClearCropCommandHandler(farming));
        dispatcher.Register(new WaterCommandHandler(farming));
        dispatcher.Register(new StartFishingCommandHandler(fishing));
        dispatcher.Register(new CancelFishingCommandHandler(fishing));
        dispatcher.Register(new BuyItemCommandHandler(trading));
        dispatcher.Register(new OfferItemCommandHandler(trading));
        dispatcher.Register(new WithdrawOfferCommandHandler(trading));

        dispatcher.Register(new StartTravelCommandHandler(travel));
        dispatcher.Register(new StopTravelCommandHandler(travel));
        dispatcher.Register(new StartCookingCommandHandler(cooking));
        dispatcher.Register(new CancelCookingCommandHandler(cooking));
        dispatcher.Register(new SleepCommandHandler(sleep));
        dispatcher.Register(new WakeCommandHandler(sleep));
        dispatcher.Register(new RentBedCommandHandler(inn));
        dispatcher.Register(new LeaveVillageCommandHandler(visitors));

        // Survival and timed outcomes are updated before AI reads needs and chooses its next action.
        simulation.AddSystem(new TimeSystem(simulation.Clock));
        simulation.AddSystem(expiration);
        simulation.AddSystem(visitors);
        simulation.AddSystem(new HungerSystem(world));
        simulation.AddSystem(new ManaRegenerationSystem(world, simulation.Clock));
        simulation.AddSystem(cooldown);
        simulation.AddSystem(new StatusEffectSystem(world, simulation.Clock));
        simulation.AddSystem(new DeathSystem(world));
        simulation.AddSystem(new LeaseSystem(world, simulation.Clock));
        simulation.AddSystem(inn);
        simulation.AddSystem(sleep);
        simulation.AddSystem(travel);
        simulation.AddSystem(movement);
        simulation.AddSystem(cooking);
        simulation.AddSystem(farming);
        simulation.AddSystem(processing);
        simulation.AddSystem(fishing);
        simulation.AddSystem(new NeedsSystem(world));
        simulation.AddSystem(new ScheduleSystem(world, simulation.Clock));
        simulation.AddSystem(new AIDecisionSystem(world, simulation.Clock));
        simulation.AddSystem(new AIActionExecutionSystem(world, simulation.Clock, dispatcher));
        simulation.AddSystem(new MarketTrackingSystem(world, simulation.Clock));
        simulation.AddSystem(new MarketPriceSystem(world, simulation.Clock));
    }
}
