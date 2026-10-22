using RSF.Core.Commands;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

/// <summary>Routes fishing commands to FishingSystem without blocking the simulation.</summary>
public sealed class StartFishingCommandHandler : ICommandHandler<StartFishingCommand>
{
    private readonly FishingSystem _fishingSystem;

    /// <summary>Connects the handler to the simulation's fishing system.</summary>
    /// <param name="fishingSystem">The system that validates and schedules attempts.</param>
    public StartFishingCommandHandler(FishingSystem fishingSystem)
    {
        _fishingSystem = fishingSystem;
    }

    /// <summary>Attempts to start fishing; rejected commands do not consume bait.</summary>
    /// <param name="command">The fisher, river, rod, and bait supply.</param>
    public void Handle(StartFishingCommand command)
    {
        bool started = _fishingSystem.StartFishing(command.Fisher, command.River, command.Rod, command.Bait);
        Console.WriteLine(started
            ? "Fishing started; one bait used."
            : "Cannot start fishing: check skill, river, supplies, and active attempt.");
    }
}
