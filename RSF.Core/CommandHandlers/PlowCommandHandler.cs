using RSF.Core.Commands;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

/// <summary>Routes plow commands to RSF's farming rules.</summary>
public sealed class PlowCommandHandler : ICommandHandler<PlowCommand>
{
    private readonly FarmingSystem _farmingSystem;

    /// <summary>Connects this handler to the simulation's farming system.</summary>
    /// <param name="farmingSystem">The system responsible for farming validation and behavior.</param>
    public PlowCommandHandler(FarmingSystem farmingSystem)
    {
        _farmingSystem = farmingSystem;
    }

    /// <summary>Attempts to plow the requested tile and reports the outcome.</summary>
    /// <param name="command">The farmer and target tile.</param>
    public void Handle(PlowCommand command)
    {
        bool plowed = _farmingSystem.Plow(command.Farmer, command.Tile);
        Console.WriteLine(plowed ? "Farmland plowed into a farm plot." : "Cannot plow this tile.");
    }
}
