namespace RSF.Core.Components;

/// <summary>Stores an NPC's gold balance; TradeSystem transfers it when a purchase succeeds.</summary>
/// <param name="Amount">The number of whole gold coins available.</param>
public readonly record struct GoldComponent(int Amount) : IComponent;
