using RSF.Core.Entities;

namespace RSF.Core.Components;

public sealed class EquipmentComponent : IComponent
{
    public Entity? Weapon { get; set; }
}