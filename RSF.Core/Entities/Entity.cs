using RSF.Core.Common;

namespace RSF.Core.Entities;

public sealed class Entity : IEntity
{
    public EntityId Id { get; }

    public Entity(EntityId id)
    {
        Id = id;
    }
}