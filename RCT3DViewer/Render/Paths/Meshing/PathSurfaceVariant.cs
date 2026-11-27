using RCT3D.Interop.Models;
using System.Numerics;

namespace RCT3D.Render.Paths.Meshing;

internal static class PathSurfaceVariant
{
    private static readonly byte[] imageOffsets = [
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15,
         0,  1,  2, 20,  4,  5,  6, 22,  8,  9, 10, 26, 12, 13, 14, 36,
         0,  1,  2,  3,  4,  5, 21, 23,  8,  9, 10, 11, 12, 13, 33, 37,
         0,  1,  2,  3,  4,  5,  6, 24,  8,  9, 10, 11, 12, 13, 14, 38,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 29, 30, 34, 39,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 40,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 35, 41,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 42,
         0,  1,  2,  3,  4,  5,  6,  7,  8, 25, 10, 27, 12, 31, 14, 43,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 28, 12, 13, 14, 44,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 45,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 46,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 32, 14, 47,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 48,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 49,
         0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 50,
    ];

    public static bool TryGetSpriteIndex(RCTTilePathData path, out int spriteIndex)
    {
        if ((path.Edges & 0xf0) != 0 || (path.Corners & 0xf0) != 0
            || (path.IsQueue && BitOperations.PopCount((uint)path.Edges) > 2)) {
            spriteIndex = 0;
            return false;
        }

        var connectivity = path.Edges | (path.IsQueue ? 0 : path.Corners << 4);
        spriteIndex = imageOffsets[connectivity] + 1;
        return true;
    }
}
