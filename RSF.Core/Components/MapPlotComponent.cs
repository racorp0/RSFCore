using RSF.Core.Configuration;

namespace RSF.Core.Components;

/// <summary>Records a building footprint or farm parcel on its anchor entity; existing crop cells remain individual tiles.</summary>
/// <param name="Id">The map.json parcel identity.</param>
/// <param name="Kind">The parcel purpose.</param>
/// <param name="X">The footprint's left edge, independent of the building entrance position.</param>
/// <param name="Y">The footprint's top edge.</param>
/// <param name="Width">The standard parcel width in tiles.</param>
/// <param name="Height">The standard parcel height in tiles.</param>
/// <param name="Floors">Recorded floor count; interiors are not simulated yet.</param>
public sealed record MapPlotComponent(string Id, MapPlotKind Kind, int X, int Y, int Width, int Height, int Floors) : IComponent;
