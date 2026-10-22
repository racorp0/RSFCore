using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct UseItemCommand(
    Entity Player,
    Entity Item);