using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace PushInChairs;

/// <summary>Experimental tap-to-toggle chair movement. Only furniture classified as a chair moves.</summary>
public sealed class ModEntry : Mod
{
    private readonly Dictionary<Furniture, Vector2> originalPositions = new();

    public override void Entry(IModHelper helper)
    {
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => originalPositions.Clear();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null || !e.Button.IsActionButton())
            return;

        var location = Game1.currentLocation;
        if (location == null)
            return;

        Vector2 tap = e.Cursor.GrabTile;

        // Tapping the chair toggles it. If the table covers the chair,
        // tapping that table tile can also retrieve a previously tucked chair.
        Furniture? chair = location.furniture.FirstOrDefault(f => IsChair(f) && f.TileLocation == tap);
        if (chair == null)
        {
            chair = location.furniture.FirstOrDefault(f =>
                IsChair(f) && originalPositions.ContainsKey(f) &&
                (f.TileLocation == tap || (IsTableAt(location, tap) &&
                 Vector2.Distance(f.TileLocation, tap) <= 1.5f)));
        }

        if (chair == null)
            return;

        if (originalPositions.TryGetValue(chair, out Vector2 original))
        {
            if (location.furniture.Any(f => f != chair && !IsTable(f) && f.TileLocation == original))
                return;
            chair.TileLocation = original;
            originalPositions.Remove(chair);
            Helper.Input.Suppress(e.Button);
            Monitor.Log("Pulled chair out.", LogLevel.Trace);
            return;
        }

        // Pick only tables, never couches, dressers, armchairs or cabinets.
        Furniture? table = location.furniture
            .Where(IsTable)
            .OrderBy(f => Vector2.DistanceSquared(f.TileLocation, chair.TileLocation))
            .FirstOrDefault(f => Vector2.DistanceSquared(f.TileLocation, chair.TileLocation) <= 9f);
        if (table == null)
            return;

        // Experimental overlap of the chair's anchor with the table anchor.
        // This is not yet a verified visual tuck for all table sizes and rotations.
        Vector2 destination = table.TileLocation;
        if (!location.isTileOnMap(destination) ||
            location.furniture.Any(f => f != chair && f != table && f.TileLocation == destination))
            return;

        originalPositions[chair] = chair.TileLocation;
        chair.TileLocation = destination;
        Helper.Input.Suppress(e.Button);
        Monitor.Log("Tucked chair under table (experimental).", LogLevel.Trace);
    }

    private static bool IsTableAt(GameLocation location, Vector2 tile) =>
        location.furniture.Any(f => IsTable(f) && f.TileLocation == tile);

    private static bool IsChair(Furniture furniture) =>
        furniture.furniture_type.Value == Furniture.chair;

    private static bool IsTable(Furniture furniture) =>
        furniture.furniture_type.Value == Furniture.table ||
        furniture.furniture_type.Value == Furniture.longTable;
}
