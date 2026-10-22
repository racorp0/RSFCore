using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Stops a route through TravelSystem when AI changes its goal.</summary>
public sealed class StopTravelCommandHandler : ICommandHandler<StopTravelCommand>
{
    private readonly TravelSystem _travel;
    /// <summary>Connects the handler to existing travel state.</summary>
    /// <param name="travel">The world's travel system.</param>
    public StopTravelCommandHandler(TravelSystem travel) { _travel = travel; }
    /// <summary>Cancels the route and stops its velocity.</summary>
    /// <param name="command">The NPC to stop.</param>
    public void Handle(StopTravelCommand command) { _travel.Stop(command.Npc); }
}
