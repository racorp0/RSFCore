using RSF.Core.Commands;
using RSF.Core.Components;
using RSF.Core.Simulation;
using RSF.Core.Systems;

namespace RSF.Core.CommandHandlers;

public sealed class MoveCommandHandler
    : ICommandHandler<MoveCommand>
{
    private readonly World _world;
    private readonly MovementSystem _movementSystem;

    public MoveCommandHandler(
        World world,
        MovementSystem movementSystem)
    {
        _world = world;
        _movementSystem = movementSystem;
    }

    public void Handle(
        MoveCommand command)
    {
        if (_world.TryGet<DeadComponent>(
            command.Entity,
            out _))
        {
            Console.WriteLine(
                "Dead entities cannot move.");

            return;
        }

        _movementSystem.SetVelocity(
            command.Entity,
            command.X,
            command.Y);
    }
}