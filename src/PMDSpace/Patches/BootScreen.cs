using System.Text;
using NDSRenderer;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class BootScreen
{
    private const string NintendoLogo = "BACK/n_logo.bgp";
    private const string RatingNotice = "BACK/w_esrb.bgp";
    private const int ScreenMain = 0;
    private const int LogoLayer = 0;
    private const uint NoticePaletteBank = 1;
    private const int PictureRows = 24;
    private const int LogoFirstRow = 9;
    private const int LogoRows = 6;
    private const int LogoRaise = 4;
    private const int NoticeDrop = 4;
    private const ushort Transparent = 0;
    private const uint KeepPicturesMode = 3;

    private enum Picture { None, Logo, Notice }

    private static readonly ushort[] LeftEdge = new ushort[FrameMargins.Rows];
    private static readonly ushort[] RightEdge = new ushort[FrameMargins.Rows];

    private static Picture _loading;
    private static (int Engine, int Background)? _notice;
    private static (uint Address, uint Size)? _logoMap;

    public static void Install()
    {
        var loadPicture = Funcs.Patches.sub_02052060;
        Funcs.Patches.sub_02052060 = ctx =>
        {
            _loading = ReadPath(ctx.R0) switch
            {
                NintendoLogo => Picture.Logo,
                RatingNotice => Picture.Notice,
                _ => Picture.None,
            };

            if (_loading == Picture.Logo)
            {
                Memory.Poke<int>(ctx.R13) = LogoLayer;
                Memory.Poke<int>(ctx.R13 + 4) = ScreenMain;
            }
            else if (_loading == Picture.Notice)
            {
                ctx.R3 = NoticePaletteBank;
            }

            loadPicture(ctx);
            _loading = Picture.None;
        };

        var showTilemap = Funcs.Patches.sub_02051E60;
        Funcs.Patches.sub_02051E60 = ctx =>
        {
            if (_loading != Picture.None)
            {
                ref var tilemap = ref Memory.Poke<Tilemap>(Tilemaps.Address((int)ctx.R1, (int)ctx.R0));
                if (_loading == Picture.Logo)
                {
                    RaiseLogo(tilemap);
                }
                else
                {
                    DropNotice(tilemap);
                }
            }

            showTilemap(ctx);
        };

        var drawFrame = Funcs.Patches.ov34_022DC5B0;
        Funcs.Patches.ov34_022DC5B0 = ctx =>
        {
            drawFrame(ctx);
            if (_notice is { } notice)
            {
                SupplyEdges(notice.Engine, notice.Background);
            }
        };

        var main = Funcs.Patches.ExplorersOfSkyMain;
        Funcs.Patches.ExplorersOfSkyMain = ctx =>
        {
            uint mode = ctx.R0;
            main(ctx);
            if (mode == KeepPicturesMode && _logoMap is { } logoMap)
            {
                Scheduler.RunUntimed(() => HideLogo(logoMap.Address, logoMap.Size));
                _logoMap = null;
            }
        };
    }

    public static bool IsBootPicture(string path) => path is NintendoLogo or RatingNotice;

    public static string ReadPath(uint address)
    {
        var path = new StringBuilder();
        for (byte c; (c = Memory.Peek<byte>(address)) != 0; address++)
        {
            path.Append((char)c);
        }

        return path.ToString();
    }

    private static void HideLogo(uint address, uint size)
    {
        for (uint offset = 0; offset < size; offset += 2)
        {
            Memory.Write16(address + offset, Transparent);
        }
    }

    private static void SupplyEdges(int engine, int background)
    {
        for (int i = 0; i < GameFrame.Margins.ColumnsShown; i++)
        {
            LeftEdge.CopyTo(GameFrame.Margins.Supply(engine, background, -i));
            RightEdge.CopyTo(GameFrame.Margins.Supply(engine, background, GameFrame.ScreenColumns + i));
        }
    }

    private static void RaiseLogo(in Tilemap tilemap)
    {
        for (int row = 0; row < PictureRows; row++)
        {
            int source = row + LogoRaise;
            bool isLogo = source is >= LogoFirstRow and < LogoFirstRow + LogoRows;
            for (int column = 0; column < GameFrame.ScreenColumns; column++)
            {
                Tilemaps.Entry(tilemap, column, row) = isLogo ? Tilemaps.Entry(tilemap, column, source) : Transparent;
            }
        }

        _logoMap = (tilemap.VRAM, tilemap.VRAMSize);
    }

    private static void DropNotice(in Tilemap tilemap)
    {
        Span<ushort> entries = stackalloc ushort[PictureRows * GameFrame.ScreenColumns];
        for (int row = 0; row < PictureRows; row++)
        {
            for (int column = 0; column < GameFrame.ScreenColumns; column++)
            {
                entries[(row * GameFrame.ScreenColumns) + column] = Tilemaps.Entry(tilemap, column, row);
            }
        }

        for (int row = 0; row < PictureRows; row++)
        {
            int destination = (row + NoticeDrop) % PictureRows;
            for (int column = 0; column < GameFrame.ScreenColumns; column++)
            {
                Tilemaps.Entry(tilemap, column, destination) = entries[(row * GameFrame.ScreenColumns) + column];
            }
        }

        for (int row = 0; row < FrameMargins.Rows; row++)
        {
            LeftEdge[row] = Tilemaps.Entry(tilemap, 0, row);
            RightEdge[row] = Tilemaps.Entry(tilemap, GameFrame.ScreenColumns - 1, row);
        }

        _notice = (tilemap.Engine, tilemap.Background);
    }
}
