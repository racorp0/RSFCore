using System.Collections.Generic;

namespace RSF.Core.Components;

public sealed class StatModifierComponent : IComponent
{
    public Dictionary<string, int> Modifiers { get; } = new();

    public int Get(string statName)
    {
        if (Modifiers.TryGetValue(
            statName,
            out int value))
        {
            return value;
        }

        return 0;
    }

    public void Set(
        string statName,
        int value)
    {
        Modifiers[statName] = value;
    }

    public void Add(
        string statName,
        int value)
    {
        Modifiers[statName] =
            Get(statName) + value;
    }

    public bool Has(string statName)
    {
        return Modifiers.ContainsKey(statName);
    }
}