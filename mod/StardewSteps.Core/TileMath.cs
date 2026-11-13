namespace StardewSteps.Core;

public static class TileMath
{
    /// <summary>Center of the tile next to the one containing (x, y), in direction d.</summary>
    public static (float x, float y) TargetCenter(float x, float y, Direction d, int tile = 64)
    {
        int tx = (int)MathF.Floor(x / tile);
        int ty = (int)MathF.Floor(y / tile);
        switch (d)
        {
            case Direction.Up: ty--; break;
            case Direction.Down: ty++; break;
            case Direction.Left: tx--; break;
            case Direction.Right: tx++; break;
        }
        return (tx * tile + tile / 2f, ty * tile + tile / 2f);
    }
}
