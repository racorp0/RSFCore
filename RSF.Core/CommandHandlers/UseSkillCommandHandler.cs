using RSF.Core.Commands;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class UseSkillCommandHandler
    : ICommandHandler<UseSkillCommand>
{
    private readonly World _world;
    private readonly CooldownSystem _cooldownSystem;

    public UseSkillCommandHandler(
        World world,
        CooldownSystem cooldownSystem)
    {
        _world = world;
        _cooldownSystem = cooldownSystem;
    }

    public void Handle(
        UseSkillCommand command)
    {
        // 1. Caster cannot be dead
        if (_world.TryGet<DeadComponent>(
            command.Caster,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot use skills.");

            return;
        }

        // 2. Target cannot be dead
        if (_world.TryGet<DeadComponent>(
            command.Target,
            out _))
        {
            Console.WriteLine(
                "Cannot use skill on a dead target.");

            return;
        }

        // 3. Target must have health
        if (!_world.TryGet<HealthComponent>(
            command.Target,
            out var health))
        {
            Console.WriteLine(
                "Target does not have health.");

            return;
        }

        // 4. Caster must have mana
        if (!_world.TryGet<ManaComponent>(
            command.Caster,
            out var mana))
        {
            Console.WriteLine(
                "Caster does not have mana.");

            return;
        }

        // 5. Check cooldown
        if (!_cooldownSystem.IsReady(
            command.Caster))
        {
            Console.WriteLine(
                "Skill is still on cooldown!");

            return;
        }

        // 6. Fireball settings
        const int damage = 20;
        const int manaCost = 10;

        // 7. Check enough mana
        if (mana.Current < manaCost)
        {
            Console.WriteLine(
                "Not enough mana.");

            return;
        }

        // 8. Deal damage
        int newHealth =
            Math.Max(
                health.Current - damage,
                0);

        _world.Add(
            command.Target,
            new HealthComponent(
                newHealth,
                health.Max));

        // 9. Consume mana
        int newMana =
            mana.Current - manaCost;

        _world.Add(
            command.Caster,
            new ManaComponent(
                newMana,
                mana.Max));

        // 10. Start cooldown
        _cooldownSystem.StartCooldown(
            command.Caster,
            GameDuration.FromMinutes(5));

        Console.WriteLine(
            $"Entity {command.Caster.Id} " +
            $"casts Fireball on " +
            $"{command.Target.Id}.");

        Console.WriteLine(
            $"Fireball dealt {damage} damage.");

        Console.WriteLine(
            $"Mana: {newMana}/{mana.Max}");

        Console.WriteLine(
            $"Target HP: " +
            $"{newHealth}/{health.Max}");
    }
}