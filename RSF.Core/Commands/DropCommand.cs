using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct DropCommand(
    Entity Player,
    Entity Item);