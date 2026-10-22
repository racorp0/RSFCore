using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Remembers an NPC-generated offer's original price so market updates never compound already rounded prices.</summary>
/// <param name="MarketId">The local market controlling this offer.</param>
/// <param name="SellerId">The inventory owner who published it.</param>
/// <param name="BaseGoldPerUnit">Unadjusted gold price; whole fish retain their fractional weight calculation.</param>
/// <param name="LastQuotedPrice">Last automatic quote, allowing direct manual overrides to be respected.</param>
public readonly record struct AutomaticMarketPriceComponent(
    EntityId MarketId, EntityId SellerId, double BaseGoldPerUnit, int LastQuotedPrice) : IComponent;
