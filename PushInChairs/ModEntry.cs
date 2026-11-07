using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;

namespace PushInChairs;

public sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu != null || !e.Button.IsActionButton())
            return;
        var location = Game1.currentLocation;
        if (location == null)
            return;

        Vector2 target = e.Cursor.GrabTile;
        Furniture? chair = null;
        foreach (Furniture furniture in location.furniture)
        {
            if (furniture.TileLocation == target && IsChair(furniture))
            {
                chair = furniture;
                break;
            }
        }
        if (chair == null)
            return;

        // Only chair furniture can move. Never alter tables, couches or cabinets.
        foreach (Vector2 direction in new[] { new Vector2(0, -1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, 1) })
        {
            Vector2 destination = chair.TileLocation + direction;
            bool tableFound = false;
            foreach (Furniture furniture in location.furniture)
            {
                if (furniture != chair && IsTable(furniture) &&
                    Vector2.Distance(furniture.TileLocation, destination) <= 1f)
                {
                    tableFound = true;
                    break;
                }
            }
            if (!tableFound || !location.isTileOnMap(destination) || location.furniture.Any(f => f != chair && f.getBoundingBox(f.TileLocation).Contains((int)(destination.X * 64 + 32), (int)(destination.Y * 64 + 32))))
                continue;
            chair.TileLocation = destination;
            Helper.Input.Suppress(e.Button);
            Monitor.Log($"Moved chair to {destination}.", LogLevel.Trace);
            return;
        }
    }

    private static bool IsChair(Furniture furniture) => furniture.furniture_type.Value == Furniture.chair;
    private static bool IsTable(Furniture furniture) => furniture.furniture_type.Value == Furniture.table || furniture.furniture_type.Value == Furniture.longTable;
}