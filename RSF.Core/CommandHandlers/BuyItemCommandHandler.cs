using RSF.Core.Commands;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

/// <summary>Routes purchases to RSF's direct trading rules.</summary>
public sealed class BuyItemCommandHandler : ICommandHandler<BuyItemCommand>
{
    private readonly TradeSystem _tradeSystem;

    /// <summary>Connects this handler to the shared trading system.</summary>
    /// <param name="tradeSystem">The system validating stock, payment, and item transfers.</param>
    public BuyItemCommandHandler(TradeSystem tradeSystem)
    {
        _tradeSystem = tradeSystem;
    }

    /// <summary>Attempts the requested purchase and reports success or failure.</summary>
    /// <param name="command">The buyer, seller, item, and quantity.</param>
    public void Handle(BuyItemCommand command)
    {
        bool bought = _tradeSystem.Buy(command.Buyer, command.Seller, command.Item, command.Quantity);
        Console.WriteLine(bought
            ? $"Purchase complete: {command.Quantity} unit(s) transferred with payment."
            : "Purchase rejected: check funds, stock, sale price, and ownership.");
    }
}
