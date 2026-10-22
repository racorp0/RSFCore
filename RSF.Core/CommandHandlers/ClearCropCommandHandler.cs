using RSF.Core.Commands;
using RSF.Core.Systems;
namespace RSF.Core.CommandHandlers;

/// <summary>Routes ClearCrop requests through the existing FarmingSystem.</summary>
public sealed class ClearCropCommandHandler : ICommandHandler<ClearCropCommand>
{
    private readonly FarmingSystem _farming;
    /// <summary>Connects this command handler to the simulation's farming rules.</summary>
    /// <param name="farming">The existing farming system.</param>
    public ClearCropCommandHandler(FarmingSystem farming) { _farming = farming; }
    /// <summary>Clears a dried crop before the next plowing cycle.</summary>
    /// <param name="command">The actor and items or plot to validate.</param>
    public void Handle(ClearCropCommand command)
    {
        bool success = _farming.ClearCrop(command.Farmer, command.Plot);
        Console.WriteLine(success ? "ClearCrop completed." : "ClearCrop failed: check the crop, supplies, and location.");
    }
}
