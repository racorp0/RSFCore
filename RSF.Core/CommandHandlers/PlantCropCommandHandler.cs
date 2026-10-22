using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes PlantCrop requests through the existing FarmingSystem.</summary>
public sealed class PlantCropCommandHandler : ICommandHandler<PlantCropCommand>
{
    private readonly FarmingSystem _farming;
    /// <summary>Connects this command handler to the simulation's farming rules.</summary>
    /// <param name="farming">The existing farming system.</param>
    public PlantCropCommandHandler(FarmingSystem farming) { _farming = farming; }
    /// <summary>Plants any JSON-defined crop using matching seeds.</summary>
    /// <param name="command">The actor and items or plot to validate.</param>
    public void Handle(PlantCropCommand command)
    {
        bool success = _farming.PlantCrop(command.Farmer, command.Plot, command.Seeds);
        Console.WriteLine(success ? "PlantCrop completed." : "PlantCrop failed: check the crop, supplies, and location.");
    }
}
