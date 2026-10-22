using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct MoveCommand(
    Entity Entity,
    int X,
    int Y);