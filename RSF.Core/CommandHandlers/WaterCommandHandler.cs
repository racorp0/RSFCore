using RSF.Core.Commands;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

/// <summary>Routes watering commands to RSF's farming rules.</summary>
public sealed class WaterCommandHandler : ICommandHandler<WaterCommand>
{
    private readonly FarmingSystem _farmingSystem;

    /// <summary>Connects this handler to the simulation's farming system.</summary>
    /// <param name="farmingSystem">The system responsible for farming validation and behavior.</param>
    public WaterCommandHandler(FarmingSystem farmingSystem)
    {
        _farmingSystem = farmingSystem;
    }

    /// <summary>Attempts watering for any JSON plant without resetting the timer when the action fails.</summary>
    /// <param name="command">The farmer, planted plot, and watering can.</param>
    public void Handle(WaterCommand command)
    {
        bool watered = _farmingSystem.Water(command.Farmer, command.Plot, command.WateringCan);
        Console.WriteLine(watered ? "Crop watered." : "Cannot water: check the crop, can, and configured watering interval.");
    }
}
