using NDSRenderer;
using NTRModern;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class FullScreenPictures
{
    private const int TileSize = 8;

    private static readonly Dictionary<uint, TilemapCopy> Pictures = [];
    private static readonly Dictionary<(short Map, int Render), bool> FitsScreen = [];

    public static void Install()
    {
        var loadPicture = Funcs.Patches.sub_02052060;
        Funcs.Patches.sub_02052060 = ctx =>
        {
            string path = BootScreen.ReadPath(ctx.R0);
            var (layer, screen) = (Memory.Peek<int>(ctx.R13), Memory.Peek<int>(ctx.R13 + sizeof(int)));
            loadPicture(ctx);
            if (!BootScreen.IsBootPicture(path))
            {
                Add(Tilemaps.Address(screen, layer));
            }
        };

        var render = Funcs.Patches.ov11_022EF70C;
        Funcs.Patches.ov11_022EF70C = ctx =>
        {
            uint groundBg = ctx.R0;
            render(ctx);
            if (groundBg == GroundBg.Main)
            {
                CoverIfPicture(groundBg);
            }
        };

        GameFrame.Finished += _ =>
        {
            foreach (var (address, tilemap) in Pictures)
            {
                if (tilemap.IsUnchanged(address))
                {
                    GameFrame.Insets.Cover(tilemap.Engine, (ScreenLayers)(1 << tilemap.Background));
                }
                else
                {
                    Pictures.Remove(address);
                }
            }
        };
    }

    private static void Add(uint address)
    {
        var tilemap = new TilemapCopy();
        tilemap.Copy(address);
        Pictures[address] = tilemap;
    }

    private static void CoverIfPicture(uint groundBg)
    {
        ref readonly var ground = ref Memory.Peek<GroundBg>(groundBg);
        bool isPicture = !StackedScenes.IsInTopMenu && !OffScreenActors.IsAnyActorLive();
        for (int index = 0; index < ground.CameraCount; index++)
        {
            isPicture &= IsWithinScreen(ground.BackgroundId, GroundBg.MapRenderAddress(groundBg, index), index);
        }

        for (int index = 0; index < ground.CameraCount; index++)
        {
            ref readonly var map = ref Memory.Peek<MapRender>(GroundBg.MapRenderAddress(groundBg, index));
            for (int background = 0; background < map.BackgroundCount; background++)
            {
                uint tilemap = ground.TilemapAddress(map.FirstTilemap + background);
                if (isPicture)
                {
                    Add(tilemap);
                }
                else
                {
                    Pictures.Remove(tilemap);
                }
            }
        }
    }

    private static bool IsWithinScreen(short map, uint mapRender, int render)
    {
        if (Memory.Peek<MapRender>(mapRender).ChunkDimensions != MapRender.Chunks3x3)
        {
            return false;
        }

        if (!FitsScreen.TryGetValue((map, render), out bool fits))
        {
            var (columns, rows) = MapRender.ArtBounds(mapRender);
            fits = columns.Last - columns.First < FrameRenderer.Width / TileSize && rows.Last - rows.First < Video.VisibleLines / TileSize;
            FitsScreen[(map, render)] = fits;
        }

        return fits;
    }
}
