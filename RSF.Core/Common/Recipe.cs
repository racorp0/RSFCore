namespace RSF.Core.Common;

/// <summary>Uses the shared processing recipe structure for cooking, with Meal output options carrying food effects.</summary>
public sealed record Recipe : ProcessingDefinition
{
    /// <summary>Explicitly permits long-life output and enables bounded surplus preservation choices.</summary>
    public bool PreservesFood { get; init; }
}
