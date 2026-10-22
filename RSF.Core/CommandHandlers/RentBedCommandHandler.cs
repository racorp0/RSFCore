using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes RentBed requests through the existing command framework.</summary>
public sealed class RentBedCommandHandler : ICommandHandler<RentBedCommand>
{
    private readonly InnSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public RentBedCommandHandler(InnSystem system) { _system = system; }
    /// <summary>Attempts the requested action; the AI observes its result in world components.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(RentBedCommand command) { _system.Rent(command.Guest, command.Inn); }
}
