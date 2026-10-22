using RSF.Core.Common;

namespace RSF.Core.Components;

/// <summary>Distinguishes an inventory purchase from an inn payment in diagnostic history.</summary>
public enum TransactionKind { Purchase, Rent }

/// <summary>Snapshots a completed payment so later item consumption, renaming, or departure cannot change its history.</summary>
/// <param name="At">The game minute payment completed.</param>
/// <param name="Kind">Purchase or rent.</param>
/// <param name="PayerId">The existing ID of the buyer or guest.</param>
/// <param name="PayerName">The payer's name at payment time.</param>
/// <param name="ReceiverId">The existing ID of the seller or inn owner.</param>
/// <param name="ReceiverName">The receiver's name at payment time.</param>
/// <param name="ItemName">The purchased item's name and optional quality, or the rented inn.</param>
/// <param name="Quantity">Transferred trade units, or one daily rental payment.</param>
/// <param name="Unit">Grams, units, or days; prices use this same unit.</param>
/// <param name="WeightGrams">Whole fish weight; null for items without an individual fish weight.</param>
/// <param name="GoldPerUnit">The actual posted unit price, or the daily rent.</param>
/// <param name="TotalGold">The actual total payment.</param>
/// <param name="PayerGoldBefore">Payer gold immediately before payment.</param>
/// <param name="PayerGoldAfter">Payer gold immediately after payment.</param>
/// <param name="ReceiverGoldBefore">Receiver gold immediately before payment.</param>
/// <param name="ReceiverGoldAfter">Receiver gold immediately after payment.</param>
public sealed record TransactionRecord(GameMinute At, TransactionKind Kind,
    EntityId PayerId, string PayerName, EntityId ReceiverId, string ReceiverName,
    string ItemName, int Quantity, string Unit, int? WeightGrams, int GoldPerUnit, int TotalGold,
    int PayerGoldBefore, int PayerGoldAfter, int ReceiverGoldBefore, int ReceiverGoldAfter);

/// <summary>Stores bounded payment history on an NPC or village; existing trade and inn systems still perform all transfers.</summary>
public sealed class TransactionHistoryComponent : IComponent
{
    public EntityId OwnerId { get; }
    /// <summary>A queue keeps records in completion order and removes the oldest entry cheaply.</summary>
    public Queue<TransactionRecord> Entries { get; } = new();
    public long TotalRecorded { get; set; }
    /// <summary>Lifetime recorded income for an NPC, including entries removed by the retention limit.</summary>
    public long GoldReceived { get; set; }
    /// <summary>Lifetime recorded spending for an NPC, including entries removed by the retention limit.</summary>
    public long GoldSpent { get; set; }

    /// <summary>Identifies the existing NPC or village that owns this diagnostic history.</summary>
    /// <param name="ownerId">The existing entity ID; no new identity mechanism is introduced.</param>
    public TransactionHistoryComponent(EntityId ownerId) { OwnerId = ownerId; }
}
