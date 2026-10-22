using System;
using System.Collections.Generic;
using RSF.Core.Common;
using System.Collections;

namespace RSF.Core.Components;

public sealed class ComponentStore
{
    private readonly Dictionary<Type, object> _store = new();

    private Dictionary<EntityId, T> GetStorage<T>()
    where T : IComponent
    {
        Type componentType = typeof(T);

        if (!_store.TryGetValue(componentType, out object? storage))
        {
            storage = new Dictionary<EntityId, T>();

            _store[componentType] = storage;
        }

        return (Dictionary<EntityId, T>)storage;
    }

    public bool TryGetStorage<T>(
    out Dictionary<EntityId, T>? storage)
    where T : IComponent
    {
        Type componentType = typeof(T);

        if (_store.TryGetValue(componentType, out object? value))
        {
            storage = (Dictionary<EntityId, T>)value;

            return true;
        }

        storage = null;

        return false;
    }

    public void Add<T>(EntityId entityId, T component)
    where T : IComponent
    {
        GetStorage<T>()[entityId] = component;
    }

    public bool Has<T>(EntityId entityId)
    where T : IComponent
    {
        if (!TryGetStorage<T>(out var storage))
        {
            return false;
        }
        return GetStorage<T>().ContainsKey(entityId);
    }

    public T Get<T>(EntityId entityId)
    where T : IComponent
    {
        if (!TryGetStorage<T>(out var storage))
        {
            throw new InvalidOperationException(
                $"Storage for {typeof(T).Name} does not exist.");
        }

        if (!storage.TryGetValue(entityId, out T? component))
        {
            throw new InvalidOperationException(
                $"Entity '{entityId}' does not have component '{typeof(T).Name}'.");
        }

        return component;
    }

    public bool Remove<T>(EntityId entityId)
    where T : IComponent
    {
        if (!TryGetStorage<T>(out var storage))
        {
            return false;
        }

        return storage.Remove(entityId);
    }

    public IReadOnlyDictionary<EntityId, T> GetAll<T>()
    where T : IComponent
    {
        if (!TryGetStorage<T>(out var storage))
        {
            return new Dictionary<EntityId, T>();
        }

        return storage;
    }

    public bool TryGetStorageSnapshot<T>(
    out IReadOnlyDictionary<EntityId, T>? storage)
    where T : IComponent
    {
        if (TryGetStorage<T>(out var dictionary))
        {
            storage = dictionary;
            return true;
        }

        storage = null;
        return false;
    }

    public IEnumerable<Type> GetComponentTypes()
    {
        return _store.Keys;
    }

    public IEnumerable<(Type ComponentType, object Storage)> GetAllStorages()
    {
        foreach (var pair in _store)
        {
            yield return (pair.Key, pair.Value);
        }
    }

    public bool TryGetStorage(
    Type componentType,
    out object? storage)
    {
        return _store.TryGetValue(componentType, out storage);
    }

    public bool TryGet<T>(
    EntityId entityId,
    out T? component)
    where T : IComponent
    {
        if (!TryGetStorage<T>(out var storage))
        {
            component = default;
            return false;
        }

        return storage.TryGetValue(
            entityId,
            out component);
    }

    private bool RemoveFromStorage(
    object storage,
    EntityId entityId)
    {
        var dictionary =
            (System.Collections.IDictionary)storage;

        if (!dictionary.Contains(entityId))
        {
            return false;
        }

        dictionary.Remove(entityId);

        return true;
    }

    public void RemoveAll(EntityId entityId)
    {
        foreach (Type componentType in GetComponentTypes())
        {
            if (!TryGetStorage(
                componentType,
                out object? storage))
            {
                continue;
            }

            RemoveFromStorage(
                storage,
                entityId);
        }
    }

    public void Set<T>(
    EntityId entityId,
    T component)
    where T : IComponent
    {
        if (!TryGetStorage<T>(
            out Dictionary<EntityId, T>? storage))
        {
            throw new InvalidOperationException(
                $"Component {typeof(T).Name} belum memiliki storage.");
        }

        storage[entityId] = component;
    }

    public IEnumerable<object> GetComponents(EntityId entityId)
    {
        foreach (Type componentType in _store.Keys)
        {
            if (!TryGetStorage(
                    componentType,
                    out object? storage))
            {
                continue;
            }

            var dictionary =
                (System.Collections.IDictionary)storage;

            if (!dictionary.Contains(entityId))
            {
                continue;
            }

            object? component =
                dictionary[entityId];

            if (component != null)
            {
                yield return component;
            }
        }
    }
}