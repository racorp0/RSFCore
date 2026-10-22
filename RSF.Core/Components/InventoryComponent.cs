using RSF.Core.Entities;

namespace RSF.Core.Components;

public sealed class InventoryComponent : IComponent
{
    public List<Entity> Items { get; } = new();
}