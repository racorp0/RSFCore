using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct AttackCommand(
    Entity Attacker,
    Entity Target);