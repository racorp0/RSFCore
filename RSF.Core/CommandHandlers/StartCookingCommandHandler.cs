using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes StartCooking requests through the existing command framework.</summary>
public sealed class StartCookingCommandHandler : ICommandHandler<StartCookingCommand>
{
    private readonly CookingSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public StartCookingCommandHandler(CookingSystem system) { _system = system; }
    /// <summary>Starts cooking at the optional selected station; gameplay validation stays in CookingSystem.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(StartCookingCommand command) { _system.Start(command.Cook, command.RecipeIndex, command.Station); }
}
