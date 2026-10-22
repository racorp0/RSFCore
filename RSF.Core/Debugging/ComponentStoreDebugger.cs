using RSF.Core.Common;
using RSF.Core.Components;
using System;
using System.Text;

namespace RSF.Core.Debugging;

public static class ComponentStoreDebugger
{
    public static string DumpComponent<T>(ComponentStore store)
        where T : IComponent
    {
        StringBuilder sb = new();

        sb.AppendLine($"=== {typeof(T).Name} ===");

        if (!store.TryGetStorageSnapshot<T>(out var storage))
        {
            sb.AppendLine("(Empty)");

            return sb.ToString();
        }

        foreach (var pair in storage)
        {
            sb.AppendLine(
                $"{pair.Key} -> {pair.Value}");
        }

        return sb.ToString();
    }

    public static string DumpEntity(
    ComponentStore store,
    EntityId entityId)
    {
        StringBuilder sb = new();

        sb.AppendLine("========== ENTITY ==========");
        sb.AppendLine($"Id: {entityId}");
        sb.AppendLine();

        foreach (Type componentType in store.GetComponentTypes())
        {
            if (!store.TryGetStorage(componentType, out object? storage))
            {
                continue;
            }

            // Kita akan menangani storage di sini.
        }

        sb.AppendLine("============================");

        return sb.ToString();
    }
}