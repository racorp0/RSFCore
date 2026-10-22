using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes StartTravel requests through the existing command framework.</summary>
public sealed class StartTravelCommandHandler : ICommandHandler<StartTravelCommand>
{
    private readonly TravelSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public StartTravelCommandHandler(TravelSystem system) { _system = system; }
    /// <summary>Attempts the requested action; the AI observes its result in world components.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(StartTravelCommand command) { _system.Start(command.Npc, command.Destination); }
}
