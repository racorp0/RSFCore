using RSF.Core.Entities;

namespace RSF.Core.Commands;

public readonly record struct UseSkillCommand(
    Entity Caster,
    Entity Target);