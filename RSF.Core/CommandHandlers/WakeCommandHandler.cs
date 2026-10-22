using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes Wake requests through the existing command framework.</summary>
public sealed class WakeCommandHandler : ICommandHandler<WakeCommand>
{
    private readonly SleepSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public WakeCommandHandler(SleepSystem system) { _system = system; }
    /// <summary>Attempts the requested action; the AI observes its result in world components.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(WakeCommand command) { _system.Wake(command.Npc); }
}
