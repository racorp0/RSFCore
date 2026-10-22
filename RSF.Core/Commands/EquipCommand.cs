using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct EquipCommand(
    Entity Player,
    Entity Item);