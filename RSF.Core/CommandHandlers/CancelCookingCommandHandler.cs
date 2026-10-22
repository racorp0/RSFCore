using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes CancelCooking requests through the existing command framework.</summary>
public sealed class CancelCookingCommandHandler : ICommandHandler<CancelCookingCommand>
{
    private readonly CookingSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public CancelCookingCommandHandler(CookingSystem system) { _system = system; }
    /// <summary>Attempts the requested action; the AI observes its result in world components.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(CancelCookingCommand command) { _system.Cancel(command.Cook); }
}
