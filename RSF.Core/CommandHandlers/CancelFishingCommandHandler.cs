using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Lets AI interrupt fishing through a gameplay command rather than changing attempt data directly.</summary>
public sealed class CancelFishingCommandHandler : ICommandHandler<CancelFishingCommand>
{
    private readonly FishingSystem _fishingSystem;
    /// <summary>Connects the handler to the existing fishing system.</summary>
    /// <param name="fishingSystem">The world's fishing system.</param>
    public CancelFishingCommandHandler(FishingSystem fishingSystem) { _fishingSystem = fishingSystem; }
    /// <summary>Ends an attempt if one is active.</summary>
    /// <param name="command">The fisher to interrupt.</param>
    public void Handle(CancelFishingCommand command) { _fishingSystem.Cancel(command.Fisher); }
}
