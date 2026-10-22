using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes shared operate actions to ProcessingSystem.</summary>
public sealed class OperateProcessingCommandHandler : ICommandHandler<OperateProcessingCommand>
{
    private readonly ProcessingSystem _processing;
    /// <summary>Connects this handler to the shared processing rules.</summary>
    /// <param name="processing">The simulation processing system.</param>
    public OperateProcessingCommandHandler(ProcessingSystem processing) { _processing = processing; }
    /// <summary>Executes one validated action while retaining persistent job progress.</summary>
    /// <param name="command">The actor, job host, and any recipe choice.</param>
    public void Handle(OperateProcessingCommand command) { bool success = _processing.Operate(command.Npc, command.Host); }
}
