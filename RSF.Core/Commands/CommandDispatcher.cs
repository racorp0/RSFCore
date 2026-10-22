namespace RSF.Core.Commands;

public sealed class CommandDispatcher
{
    private readonly Dictionary<Type, object> _handlers = new();

    public void Register<TCommand>(
        ICommandHandler<TCommand> handler)
    {
        _handlers[typeof(TCommand)] = handler;
    }

    public void Dispatch<TCommand>(
        TCommand command)
    {
        if (!_handlers.TryGetValue(
            typeof(TCommand),
            out object? handler))
        {
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TCommand).Name}");
        }

        ICommandHandler<TCommand> typedHandler =
            (ICommandHandler<TCommand>)handler;

        typedHandler.Handle(command);
    }
}