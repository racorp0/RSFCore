using System;

namespace RSF.Core.Common;

/// <summary>
/// Represents the unique identity of an Entity.
/// Immutable value object.
/// </summary>
public readonly record struct EntityId(Guid Value)
{
    /// <summary>
    /// Represents an empty EntityId.
    /// </summary>
    public static EntityId Empty => new(Guid.Empty);

    /// <summary>
    /// Creates a new unique EntityId.
    /// </summary>
    public static EntityId Create()
    {
        return new EntityId(Guid.NewGuid());
    }

    /// <summary>
    /// Returns true if this EntityId has no value.
    /// </summary>
    public bool IsEmpty => Value == Guid.Empty;

    /// <summary>
    /// Returns a short string representation for debugging.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString("N")[..8];
    }
}