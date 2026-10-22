using System.Text;
using RSF.Core.Common;
using RSF.Core.Components;

namespace RSF.Core.Debugging;

/// <summary>Formats saved payments for the existing WorldDebugger without looking up entities that may have left the world.</summary>
public static class TransactionHistoryDebugger
{
    /// <summary>Shows retained records, lifetime recorded totals, prices, and both balances in completion order.</summary>
    /// <param name="history">An NPC history or village archive.</param>
    /// <returns>Readable history suitable for the existing entity dump.</returns>
    public static string Dump(TransactionHistoryComponent history)
    {
        StringBuilder text = new();
        text.AppendLine($"Transaction history: {history.Entries.Count} retained / {history.TotalRecorded} recorded");
        if (history.TotalRecorded > history.Entries.Count)
            text.AppendLine($"    {history.TotalRecorded - history.Entries.Count} older record(s) removed by the retention limit.");
        if (history.Entries.Any(entry => entry.PayerId == history.OwnerId || entry.ReceiverId == history.OwnerId))
            text.AppendLine($"    Recorded income: {history.GoldReceived} gold; spending: {history.GoldSpent} gold; net: {history.GoldReceived - history.GoldSpent} gold");
        foreach (TransactionRecord entry in history.Entries)
        {
            string weight = entry.WeightGrams is int grams ? $" ({grams} g)" : string.Empty;
            text.AppendLine($"    [{entry.At.ToGameDate()}, {entry.At.ToGameTime()}; minute {entry.At.Value}] {Action(entry, history.OwnerId)}");
            text.AppendLine($"        {entry.ItemName}: {entry.Quantity} {entry.Unit}{weight}; unit price: {entry.GoldPerUnit} gold/{entry.Unit}; total: {entry.TotalGold} gold");
            text.AppendLine($"        Gold: {entry.PayerName} {entry.PayerGoldBefore} -> {entry.PayerGoldAfter}; {entry.ReceiverName} {entry.ReceiverGoldBefore} -> {entry.ReceiverGoldAfter}");
        }
        return text.ToString();
    }

    /// <summary>Describes buying, selling, or rent from the history owner's viewpoint; archives name both participants.</summary>
    /// <param name="entry">The immutable completed payment.</param>
    /// <param name="owner">The NPC or village owning this history.</param>
    /// <returns>A readable action and counterparty.</returns>
    private static string Action(TransactionRecord entry, EntityId owner)
    {
        if (entry.Kind == TransactionKind.Rent)
        {
            if (owner == entry.PayerId) return $"Paid rent to {entry.ReceiverName}";
            if (owner == entry.ReceiverId) return $"Received rent from {entry.PayerName}";
            return $"{entry.PayerName} paid rent to {entry.ReceiverName}";
        }
        if (owner == entry.PayerId) return $"Bought from {entry.ReceiverName}";
        if (owner == entry.ReceiverId) return $"Sold to {entry.PayerName}";
        return $"{entry.PayerName} bought from {entry.ReceiverName}";
    }
}
