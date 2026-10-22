namespace RSF.Core.Configuration;

/// <summary>Identifies the purpose of a standard building or farm parcel in map.json.</summary>
public enum MapPlotKind { House, Inn, Workshop, Farm }

/// <summary>Places one parcel; building entrances remain the positions used by existing gameplay systems.</summary>
public sealed record MapPlotDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public MapPlotKind Kind { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int EntranceX { get; init; }
    public int EntranceY { get; init; }
    /// <summary>Floor metadata only; interiors and stairs are introduced in a later phase.</summary>
    public int Floors { get; init; } = 1;
}

/// <summary>Configures map dimensions, physical scale, and equal-sized building and farm parcels without replacing tile entities.</summary>
public sealed record MapSettings
{
    public int Width { get; init; } = 150;
    public int Height { get; init; } = 150;
    public double MetresPerTile { get; init; } = 1;
    public int PlotWidth { get; init; } = 12;
    public int PlotHeight { get; init; } = 9;
    public int EntranceY { get; init; } = 10;
    public int MarketX { get; init; } = 16;
    public int MarketY { get; init; } = 8;
    public IReadOnlyList<MapPlotDefinition> Plots { get; init; } = Array.Empty<MapPlotDefinition>();

    /// <summary>Finds the parcel occupying a map cell, using its rectangular footprint.</summary>
    /// <param name="x">The cell column.</param>
    /// <param name="y">The cell row.</param>
    /// <returns>The parcel, or null for an unreserved cell.</returns>
    public MapPlotDefinition? PlotAt(int x, int y) => Plots.FirstOrDefault(plot =>
        x >= plot.X && (long)x < (long)plot.X + PlotWidth && y >= plot.Y && (long)y < (long)plot.Y + PlotHeight);

    /// <summary>Checks a free interior tile for outdoor fishing spots or machinery, excluding all parcels and the market.</summary>
    /// <param name="x">The proposed column.</param>
    /// <param name="y">The proposed row.</param>
    /// <returns>True when the tile can host an outdoor resource.</returns>
    public bool ResourceTileFree(int x, int y) => x > 0 && x < Width - 1 && y > 0 && y < Height - 1 &&
        (x != MarketX || y != MarketY) && PlotAt(x, y) is null;

    /// <summary>Rejects invalid dimensions, overlapping parcels, and inaccessible or misplaced building entrances.</summary>
    public void Validate()
    {
        // A bound avoids accidentally allocating millions of entities from a mistyped setting.
        if (Width < 3 || Width > 512 || Height < 3 || Height > 512 || PlotWidth < 1 || PlotHeight < 1 ||
            !double.IsFinite(MetresPerTile) || MetresPerTile <= 0 || EntranceY < 1 || EntranceY >= Height - 1 ||
            MarketX < 1 || MarketX >= Width - 1 || MarketY < 1 || MarketY >= Height - 1 || Plots is null)
            throw new ArgumentException("Invalid map dimensions, scale, standard parcel size, market, or entrance.");
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<(int X, int Y)> occupied = new();
        foreach (var plot in Plots)
        {
            if (plot is null || string.IsNullOrWhiteSpace(plot.Id) || !plot.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-') ||
                !ids.Add(plot.Id) || string.IsNullOrWhiteSpace(plot.Name) || !Enum.IsDefined(plot.Kind) || plot.Floors < 1 || plot.Floors > 10 ||
                plot.X < 1 || plot.Y < 1 || (long)plot.X + PlotWidth >= Width || (long)plot.Y + PlotHeight >= Height)
                throw new ArgumentException("Parcels need unique valid IDs, names, floors, and footprints inside the map border.");
            if (plot.Kind != MapPlotKind.Farm &&
                (plot.EntranceX < plot.X || (long)plot.EntranceX >= (long)plot.X + PlotWidth ||
                 plot.EntranceY < plot.Y || (long)plot.EntranceY >= (long)plot.Y + PlotHeight ||
                 (plot.EntranceX != plot.X && plot.EntranceX != plot.X + PlotWidth - 1 &&
                  plot.EntranceY != plot.Y && plot.EntranceY != plot.Y + PlotHeight - 1)))
                throw new ArgumentException($"{plot.Id}: building entrance must lie on the footprint boundary.");
            if (plot.Kind == MapPlotKind.Farm && plot.Floors != 1) throw new ArgumentException($"{plot.Id}: farms have one ground level.");
            for (int y = plot.Y; y < plot.Y + PlotHeight; y++)
                for (int x = plot.X; x < plot.X + PlotWidth; x++)
                    if (!occupied.Add((x, y))) throw new ArgumentException($"{plot.Id}: parcels overlap.");
        }
        if (Plots.Count(plot => plot.Kind == MapPlotKind.Inn) != 1 || PlotAt(MarketX, MarketY) is not null)
            throw new ArgumentException("This phase needs one inn and a market outside all parcels.");
        foreach (var plot in Plots.Where(plot => plot.Kind != MapPlotKind.Farm))
            if (!ResourceTileFree(plot.EntranceX - 1, plot.EntranceY) && !ResourceTileFree(plot.EntranceX + 1, plot.EntranceY) &&
                !ResourceTileFree(plot.EntranceX, plot.EntranceY - 1) && !ResourceTileFree(plot.EntranceX, plot.EntranceY + 1))
                throw new ArgumentException($"{plot.Id}: entrance needs an adjacent unreserved outdoor tile.");
    }
}
