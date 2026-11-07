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
    private SButton? heldButton;
    private DateTime pressedAt;
    private Vector2 pressedTile;
    private GameLocation? pressedLocation;
    private bool longPressHandled;
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(600);

    public override void Entry(IModHelper helper)
    {
        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => { originalPositions.Clear(); ResetHold(); };
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null ||
            !e.Button.IsActionButton() || heldButton != null)
            return;

        heldButton = e.Button;
        pressedAt = DateTime.UtcNow;
        pressedTile = e.Cursor.GrabTile;
        pressedLocation = Game1.currentLocation;
        longPressHandled = false;
        // Do not suppress short taps: the base game's rotate/use action must remain intact.
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        if (heldButton == e.Button)
            ResetHold();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (heldButton is not SButton button || longPressHandled)
            return;
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null ||
            Game1.currentLocation != pressedLocation || !Helper.Input.IsDown(button))
        {
            ResetHold();
            return;
        }
        if (DateTime.UtcNow - pressedAt < HoldDuration)
            return;

        // Once per hold. No second activation until the button is released.
        longPressHandled = true;
        ToggleChair(pressedTile);
    }

    private void ResetHold()
    {
        heldButton = null;
        pressedLocation = null;
        longPressHandled = false;
    }

    private void ToggleChair(Vector2 tap)
    {
        var location = Game1.currentLocation;
        if (location == null)
            return;

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
            // Do not suppress the original tap; long-press action is separate.
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
        // Do not suppress the original tap; long-press action is separate.
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
