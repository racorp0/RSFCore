using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes listing retrieval through the existing TradeSystem validations.</summary>
public sealed class WithdrawOfferCommandHandler : ICommandHandler<WithdrawOfferCommand>
{
    private readonly TradeSystem _system;
    /// <summary>Connects the existing trading system.</summary>
    /// <param name="system">The world's trading system.</param>
    public WithdrawOfferCommandHandler(TradeSystem system) { _system = system; }
    /// <summary>Attempts retrieval without transferring gold or recording a purchase.</summary>
    /// <param name="command">The owner and listed inventory item.</param>
    public void Handle(WithdrawOfferCommand command) { _system.Withdraw(command.Owner, command.Item); }
}
