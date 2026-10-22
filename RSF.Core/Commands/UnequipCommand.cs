using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct UnequipCommand(
    Entity Player);