using LibRecomp;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class DungeonMargins
{
    private const int MapScreen = 0;
    private const int MapLayer = 1;
    private const int RowsSupplied = 26;
    private const int RowsKept = 32;

    private static bool _cameraWasUpdated;

    public static void Install()
    {
        var updateCamera = Funcs.Patches.UpdateCamera;
        Funcs.Patches.UpdateCamera = ctx =>
        {
            updateCamera(ctx);
            _cameraWasUpdated = true;
        };

        GameFrame.Finished += SupplyColumns;
    }

    private static void SupplyColumns(RecompContext ctx)
    {
        bool isInDungeon = _cameraWasUpdated;
        _cameraWasUpdated = false;

        if (!isInDungeon)
        {
            return;
        }

        int count = GameFrame.Margins.ColumnsShown;
        for (int i = 0; i < count; i++)
        {
            SupplyColumn(ctx, -i);
            SupplyColumn(ctx, GameFrame.ScreenColumns + i);
        }
    }

    private static void SupplyColumn(RecompContext ctx, int column)
    {
        var camera = DisplayData.Current.CameraPixelPos;
        int mapColumn = (camera.X >> 3) + column;
        int mapRow = camera.Y >> 3;

        ref var tilemap = ref Memory.Poke<Tilemap>(Tilemaps.Address(MapScreen, MapLayer));
        Span<ushort> kept = stackalloc ushort[RowsKept];
        for (int row = 0; row < kept.Length; row++)
        {
            kept[row] = Tilemaps.Entry(tilemap, mapColumn, row);
        }

        byte wasCopyScheduled = tilemap.IsCopyScheduled;

        (ctx.R0, ctx.R1) = ((uint)(column * 8), 0);
        Funcs.ov29_0233711C(ctx);

        var supplied = GameFrame.Margins.Supply(tilemap.Engine, tilemap.Background, column);
        for (int row = 0; row < RowsSupplied; row++)
        {
            supplied[row] = Tilemaps.Entry(tilemap, mapColumn, mapRow + row);
        }

        for (int row = 0; row < kept.Length; row++)
        {
            Tilemaps.Entry(tilemap, mapColumn, row) = kept[row];
        }

        tilemap.IsCopyScheduled = wasCopyScheduled;
    }
}
