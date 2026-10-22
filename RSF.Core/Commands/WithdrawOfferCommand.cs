using RSF.Core.Entities;
namespace RSF.Core.Commands;

/// <summary>Requests retrieval of an actor's own listed goods at their real sale location.</summary>
/// <param name="Owner">The living inventory owner.</param>
/// <param name="Item">The owned listing to withdraw.</param>
public readonly record struct WithdrawOfferCommand(Entity Owner, Entity Item);
