using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct PickupCommand(
    Entity Player,
    Entity Item);