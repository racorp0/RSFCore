namespace RSF.Core.Configuration;

/// <summary>Controls diagnostic transaction records without changing RSF's prices or payment rules.</summary>
public sealed record TransactionHistorySettings
{
    public bool Enabled { get; init; } = true;
    /// <summary>Newest records retained in each NPC history and each village archive.</summary>
    public int MaximumEntries { get; init; } = 2000;

    /// <summary>Rejects an invalid retention limit before simulation setup.</summary>
    public void Validate()
    {
        if (MaximumEntries <= 0) throw new ArgumentException("Transaction history maximumEntries must be positive.");
    }
}
