using RSF.Core.Commands;
using RSF.Core.Common;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class AttackCommandHandler
    : ICommandHandler<AttackCommand>
{
    private readonly World _world;
    private readonly WorldClock _clock;
    private readonly CooldownSystem _cooldownSystem;

    public AttackCommandHandler(
        World world,
        WorldClock clock,
        CooldownSystem cooldownSystem)
    {
        _world = world;
        _clock = clock;
        _cooldownSystem = cooldownSystem;
    }

    public void Handle(AttackCommand command)
    {
        if (_world.TryGet<DeadComponent>(
            command.Attacker,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot attack.");

            return;
        }

        if (_world.TryGet<DeadComponent>(
            command.Target,
            out _))
        {
            Console.WriteLine(
                "Cannot attack a dead entity.");

            return;
        }

        if (!_world.TryGet<HealthComponent>(
            command.Target,
            out var health))
        {
            Console.WriteLine(
                "Target does not have health.");

            return;
        }

        if (!_cooldownSystem.IsReady(
            command.Attacker))
        {
            Console.WriteLine(
                "Attack is on cooldown.");

            return;
        }

        if (!_world.TryGet<StrengthComponent>(
            command.Attacker,
            out var strength))
        {
            Console.WriteLine(
                "Attacker does not have strength.");

            return;
        }

        int damage = strength.Value;

        int newHealth =
            Math.Max(
                health.Current - damage,
                0);

        _world.Add(
            command.Target,
            new HealthComponent(
                newHealth,
                health.Max));

        _cooldownSystem.StartCooldown(
            command.Attacker,
            GameDuration.FromMinutes(5));

        Console.WriteLine(
            $"Entity {command.Attacker.Id} attacked " +
            $"Entity {command.Target.Id} " +
            $"for {damage} damage.");

        Console.WriteLine(
            $"Target HP: {newHealth}/{health.Max}");
    }
}