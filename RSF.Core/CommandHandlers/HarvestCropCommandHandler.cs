using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes HarvestCrop requests through the existing FarmingSystem.</summary>
public sealed class HarvestCropCommandHandler : ICommandHandler<HarvestCropCommand>
{
    private readonly FarmingSystem _farming;
    /// <summary>Connects this command handler to the simulation's farming rules.</summary>
    /// <param name="farming">The existing farming system.</param>
    public HarvestCropCommandHandler(FarmingSystem farming) { _farming = farming; }
    /// <summary>Harvests a mature JSON-defined crop without bypassing farming validation.</summary>
    /// <param name="command">The actor and items or plot to validate.</param>
    public void Handle(HarvestCropCommand command)
    {
        bool success = _farming.HarvestCrop(command.Farmer, command.Plot);
        Console.WriteLine(success ? "HarvestCrop completed." : "HarvestCrop failed: check the crop, supplies, and location.");
    }
}
