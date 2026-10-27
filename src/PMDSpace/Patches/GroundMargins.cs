using NDSRenderer;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class GroundMargins
{
    private const int TileShift = 3;

    private static readonly HashSet<uint> Rendered = [];
    private static readonly Dictionary<uint, TilemapColumns> Columns = [];
    private static readonly HashSet<uint> RenderedTilemaps = [];

    public static void Install()
    {
        var render = Funcs.Patches.ov11_022EF70C;
        Funcs.Patches.ov11_022EF70C = ctx =>
        {
            uint groundBg = ctx.R0;
            render(ctx);
            Rendered.Add(groundBg);
        };

        GameFrame.Finished += _ =>
        {
            foreach (uint groundBg in Rendered)
            {
                SupplyColumns(groundBg);
            }

            foreach (var (tilemap, columns) in Columns)
            {
                if (!RenderedTilemaps.Contains(tilemap) && (!columns.IsUnchanged(tilemap) || IsRenderedOn(columns)))
                {
                    Columns.Remove(tilemap);
                }
            }

            foreach (var columns in Columns.Values)
            {
                columns.Supply();
            }

            Rendered.Clear();
            RenderedTilemaps.Clear();
        };
    }

    private static bool IsRenderedOn(TilemapColumns columns)
    {
        foreach (uint tilemap in RenderedTilemaps)
        {
            if (Columns[tilemap].IsOn(columns.Engine, columns.Background))
            {
                return true;
            }
        }

        return false;
    }

    private static void SupplyColumns(uint groundBg)
    {
        ref readonly var ground = ref Memory.Peek<GroundBg>(groundBg);
        for (int index = 0; index < ground.CameraCount; index++)
        {
            uint mapRender = GroundBg.MapRenderAddress(groundBg, index);
            ref readonly var map = ref Memory.Peek<MapRender>(mapRender);
            if (map.ChunkDimensions != MapRender.Chunks3x3)
            {
                continue;
            }

            int column = map.Camera.X >> TileShift;
            int row = map.Camera.Y >> TileShift;
            for (int background = 0; background < map.BackgroundCount; background++)
            {
                uint tilemap = ground.TilemapAddress(map.FirstTilemap + background);
                if (!Columns.TryGetValue(tilemap, out var columns) || columns.Left.Length != GameFrame.Margins.ColumnsShown)
                {
                    columns = new TilemapColumns(GameFrame.Margins.ColumnsShown);
                    Columns[tilemap] = columns;
                }

                columns.CopyTilemap(tilemap);
                for (int i = 0; i < columns.Left.Length; i++)
                {
                    Fill(columns.Left[i], mapRender, background, column - i, row);
                    Fill(columns.Right[i], mapRender, background, column + GameFrame.ScreenColumns + i, row);
                }

                RenderedTilemaps.Add(tilemap);
            }
        }
    }

    private static void Fill(ushort[] column, uint mapRender, int background, int mapColumn, int firstRow)
    {
        for (int row = 0; row < column.Length; row++)
        {
            column[row] = MapRender.Entry(mapRender, background, mapColumn, firstRow + row);
        }
    }

    private sealed class TilemapColumns(int count)
    {
        private readonly TilemapCopy _tilemap = new();

        public ushort[][] Left { get; } = NewColumns(count);

        public ushort[][] Right { get; } = NewColumns(count);

        public int Engine => _tilemap.Engine;

        public int Background => _tilemap.Background;

        public bool IsOn(int engine, int background) => Engine == engine && Background == background;

        public void CopyTilemap(uint address) => _tilemap.Copy(address);

        public bool IsUnchanged(uint address) => Left.Length == GameFrame.Margins.ColumnsShown && _tilemap.IsUnchanged(address);

        public void Supply()
        {
            for (int i = 0; i < Left.Length; i++)
            {
                Left[i].CopyTo(GameFrame.Margins.Supply(Engine, Background, -i));
                Right[i].CopyTo(GameFrame.Margins.Supply(Engine, Background, GameFrame.ScreenColumns + i));
            }
        }

        private static ushort[][] NewColumns(int count)
        {
            var columns = new ushort[count][];
            for (int i = 0; i < count; i++)
            {
                columns[i] = new ushort[FrameMargins.Rows];
            }

            return columns;
        }
    }
}
