using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Entities;
using System;
using System.Collections.Generic;

namespace RSF.Core.Simulation;

public sealed class World
{
    private readonly ComponentStore _components = new();

    private readonly Dictionary<EntityId, Entity> _entities = new();

    public Entity CreateEntity()
    {
        EntityId id = EntityId.Create();

        Entity entity = new Entity(id);

        _entities.Add(id, entity);

        return entity;
    }

    public void Add<T>(
    Entity entity,
    T component)
    where T : IComponent
    {
        _components.Add(entity.Id, component);
    }

    public bool Has<T>(Entity entity)
    where T : IComponent
    {
        return _components.Has<T>(entity.Id);
    }

    public bool TryGet<T>(
    Entity entity,
    out T? component)
    where T : IComponent
    {
        return _components.TryGet(
            entity.Id,
            out component);
    }

    public bool TryGetEntity(
    EntityId id,
    out Entity? entity)
    {
        return _entities.TryGetValue(
            id,
            out entity);
    }

    public bool DestroyEntity(EntityId id)
    {
        if (!_entities.Remove(id))
        {
            return false;
        }

        _components.RemoveAll(id);

        return true;
    }

    public void Set<T>(
    Entity entity,
    T component)
    where T : IComponent
    {
        _components.Set(
            entity.Id,
            component);
    }

    public IReadOnlyDictionary<EntityId, Entity> GetAllEntities()
    {
        return _entities;
    }

    public IEnumerable<Entity> GetEntities()
    {
        return _entities.Values;
    }

    /// <summary>Enumerates registered entities from the existing component index instead of scanning the entire world.</summary>
    /// <typeparam name="T">The component every returned entity must have.</typeparam>
    /// <returns>A live enumeration of matching entities; its order is not guaranteed.</returns>
    /// <remarks>Do not change the queried component collection while iterating. Use ToList first when adding or removing matching components or entities.</remarks>
    public IEnumerable<Entity> GetEntitiesWith<T>() where T : IComponent
    {
        if (!_components.TryGetStorageSnapshot<T>(out var storage) || storage is null) yield break;
        // The component store already owns this index; no separate cache needs synchronization.
        foreach (EntityId id in storage.Keys)
            if (_entities.TryGetValue(id, out var entity)) yield return entity;
    }

    public IEnumerable<Type> GetComponentTypes()
    {
        return _components.GetComponentTypes();
    }

    public IEnumerable<object> GetComponents(EntityId entityId)
    {
        return _components.GetComponents(entityId);
    }

    public bool Remove<T>(Entity entity)
    where T : IComponent
    {
        return _components.Remove<T>(entity.Id);
    }
}