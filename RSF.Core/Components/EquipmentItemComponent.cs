using RSF.Core.Components;

namespace RSF.Core.Components;

public readonly record struct EquipmentItemComponent(
    EquipmentSlot Slot) : IComponent;