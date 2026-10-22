using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes LeaveVillage requests through the existing command framework.</summary>
public sealed class LeaveVillageCommandHandler : ICommandHandler<LeaveVillageCommand>
{
    private readonly MerchantVisitSystem _system;
    /// <summary>Connects the handler to the relevant gameplay system.</summary>
    /// <param name="system">The shared gameplay system.</param>
    public LeaveVillageCommandHandler(MerchantVisitSystem system) { _system = system; }
    /// <summary>Attempts the requested action; the AI observes its result in world components.</summary>
    /// <param name="command">The requested action and its arguments.</param>
    public void Handle(LeaveVillageCommand command) { _system.Leave(command.Merchant); }
}
