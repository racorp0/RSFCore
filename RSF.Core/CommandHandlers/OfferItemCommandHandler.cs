using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes sale offers through the existing trading system.</summary>
public sealed class OfferItemCommandHandler : ICommandHandler<OfferItemCommand>
{
    private readonly TradeSystem _tradeSystem;
    /// <summary>Connects the handler to trading validation.</summary>
    /// <param name="tradeSystem">The world's trading system.</param>
    public OfferItemCommandHandler(TradeSystem tradeSystem) { _tradeSystem = tradeSystem; }
    /// <summary>Attempts to publish a sale price without transferring the item.</summary>
    /// <param name="command">The seller, item, and unit price.</param>
    public void Handle(OfferItemCommand command)
    {
        if (_tradeSystem.Offer(command.Seller, command.Item, command.GoldPerUnit))
            Console.WriteLine($"Item {command.Item.Id} offered for {command.GoldPerUnit} gold per unit.");
    }
}
