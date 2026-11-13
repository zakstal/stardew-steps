using Microsoft.Xna.Framework;
using StardewSteps.Core;
using StardewValley;

namespace StardewSteps;

/// <summary>IFarmerDriver over the local player. "Position" maps a farmer standing on a tile to that tile's center.</summary>
internal sealed class FarmerDriver : IFarmerDriver
{
    private static Farmer Player => Game1.player;

    public (float x, float y) Position
    {
        get
        {
            // The game stands a farmer on tile (tx, ty) at Position (tx*64, ty*64 + 16)
            // (see warpFarmer), so these offsets map that to the tile's center.
            var p = Player.Position;
            return (p.X + 32, p.Y + 16);
        }
    }

    public void Move(Direction d)
    {
        Player.SetMovingUp(d == Direction.Up);
        Player.SetMovingDown(d == Direction.Down);
        Player.SetMovingLeft(d == Direction.Left);
        Player.SetMovingRight(d == Direction.Right);
        Player.FacingDirection = d switch { Direction.Up => 0, Direction.Right => 1, Direction.Down => 2, _ => 3 };
        // The game clears movement flags during its own input handling, so walk the farmer
        // here (after the game's update) using the game's collision-aware movement.
        Player.MovePosition(Game1.currentGameTime, Game1.viewport, Game1.currentLocation);
        Player.updateMovementAnimation(Game1.currentGameTime);
    }

    public void Stop() => Player.Halt();

    public void Snap(float x, float y)
    {
        Player.Position = new Vector2(x - 32, y - 16);
    }
}
