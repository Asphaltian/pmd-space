using NTRModern;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class CompanyScreens
{
    private const short CompanyScreensMap = 0x115;
    private const int ChunsoftPanelX = 267;
    private const int CopyrightPanelX = 531;
    private const int NoticeRow = 10;
    private const int NoticeRows = 4;
    private const int NoticeRaise = 16;
    private const uint MasterBrightA = 0x0400006C;
    private const int White = 16;
    private const int Black = -16;

    private enum Panel { None, Chunsoft, Copyright }

    private enum Stage { Waiting, FadingIn, Holding, Switching, Done }

    private static readonly List<int> FadeIn = [];
    private static readonly List<int> FadeOut = [];

    private static Panel _panel;
    private static Stage _stage;
    private static int _hold;
    private static int _frame;

    private static bool IsShowingCompany => _stage < Stage.Switching || (_stage == Stage.Switching && _frame < FadeIn.Count);

    public static void Install()
    {
        var render = Funcs.Patches.ov11_022EF70C;
        Funcs.Patches.ov11_022EF70C = ctx =>
        {
            uint groundBg = ctx.R0;
            if (groundBg == GroundBg.TopScreen && _panel == Panel.Copyright)
            {
                render(ctx);
                StackNotices();
                return;
            }

            var panel = groundBg == GroundBg.Main ? PanelOf(groundBg) : Panel.None;
            if (panel == Panel.None)
            {
                render(ctx);
                return;
            }

            _panel = panel;
            int raise = panel == Panel.Copyright ? NoticeRaise : IsShowingCompany ? Video.VisibleLines : 0;
            ref var main = ref Memory.Poke<GroundBg>(groundBg);
            main.MoveCameras(-raise);
            render(ctx);
            main.MoveCameras(raise);
        };

        var runScript = Funcs.Patches.FuncThatCallsRunNextOpcode;
        Funcs.Patches.FuncThatCallsRunNextOpcode = ctx =>
        {
            if (_stage != Stage.Switching)
            {
                runScript(ctx);
            }
        };

        var setMasterBrightness = Funcs.Patches.GXx_SetMasterBrightness_;
        Funcs.Patches.GXx_SetMasterBrightness_ = ctx =>
        {
            if (_panel == Panel.Chunsoft && ctx.R0 == MasterBrightA)
            {
                ctx.R1 = (uint)Brightness((int)ctx.R1);
            }

            setMasterBrightness(ctx);
        };

        GameFrame.Finished += _ =>
        {
            if (_panel != Panel.None)
            {
                SupplyEdges(Memory.Peek<GroundBg>(GroundBg.Main));
            }

            if (_panel != Panel.Chunsoft && _stage != Stage.Switching)
            {
                _stage = Stage.Waiting;
            }

            _panel = Panel.None;
        };
    }

    private static Panel PanelOf(uint groundBg)
    {
        ref readonly var main = ref Memory.Peek<GroundBg>(groundBg);
        if (main.BackgroundId != CompanyScreensMap || GroundBg.TopScreen == 0)
        {
            return Panel.None;
        }

        return main.Camera.X switch
        {
            ChunsoftPanelX => Panel.Chunsoft,
            CopyrightPanelX => Panel.Copyright,
            _ => Panel.None,
        };
    }

    private static void StackNotices()
    {
        ref readonly var main = ref Memory.Peek<GroundBg>(GroundBg.Main);
        ref readonly var top = ref Memory.Peek<GroundBg>(GroundBg.TopScreen);
        if (top.Camera.X != main.Camera.X)
        {
            return;
        }

        for (int layer = 0; layer < main.LayerCount; layer++)
        {
            ref var destination = ref Memory.Poke<Tilemap>(main.TilemapAddress(layer));
            ref readonly var source = ref Memory.Peek<Tilemap>(top.TilemapAddress(layer));
            int firstRow = NoticeRow + (NoticeRaise >> 3) - NoticeRows;
            for (int row = 0; row < NoticeRows; row++)
            {
                for (int column = 0; column <= GameFrame.ScreenColumns; column++)
                {
                    Tilemaps.Entry(destination, (destination.ScrollX >> 3) + column, (destination.ScrollY >> 3) + firstRow + row) =
                        Tilemaps.Entry(source, (source.ScrollX >> 3) + column, (source.ScrollY >> 3) + NoticeRow + row);
                }
            }

            destination.IsCopyScheduled = 1;
        }
    }

    private static int Brightness(int requested)
    {
        if (_stage == Stage.Waiting && requested < White)
        {
            FadeIn.Clear();
            FadeOut.Clear();
            _hold = 0;
            _stage = Stage.FadingIn;
        }

        if (_stage == Stage.FadingIn && requested == 0)
        {
            _stage = Stage.Holding;
        }

        if (_stage == Stage.Holding && requested != 0)
        {
            _frame = 0;
            _stage = Stage.Switching;
        }

        switch (_stage)
        {
            case Stage.FadingIn:
                FadeIn.Add(requested);
                return requested;
            case Stage.Holding:
                _hold++;
                return requested;
            case Stage.Switching:
                if (FadeOut.Count == 0 || FadeOut[^1] != Black)
                {
                    FadeOut.Add(requested);
                }

                return Switching(_frame++);
            default:
                return requested;
        }
    }

    private static int Switching(int frame)
    {
        int fade = FadeIn.Count;
        if (frame < fade)
        {
            return FadeIn[fade - 1 - frame];
        }

        frame -= fade;
        if (frame == 0)
        {
            return White;
        }

        frame--;
        if (frame < fade)
        {
            return FadeIn[frame];
        }

        frame -= fade;
        if (frame < _hold)
        {
            return 0;
        }

        frame -= _hold;
        if (frame < FadeOut.Count)
        {
            return FadeOut[frame];
        }

        _stage = Stage.Done;
        return FadeOut[^1];
    }

    private static void SupplyEdges(in GroundBg main)
    {
        for (int layer = 0; layer < main.LayerCount; layer++)
        {
            ref readonly var tilemap = ref Memory.Peek<Tilemap>(main.TilemapAddress(layer));
            int left = tilemap.ScrollX >> 3;
            int right = left + ((tilemap.ScrollX & 7) == 0 ? GameFrame.ScreenColumns - 1 : GameFrame.ScreenColumns);
            int top = tilemap.ScrollY >> 3;
            for (int i = 0; i < GameFrame.Margins.ColumnsShown; i++)
            {
                var leftColumn = GameFrame.Margins.Supply(tilemap.Engine, tilemap.Background, -i);
                var rightColumn = GameFrame.Margins.Supply(tilemap.Engine, tilemap.Background, GameFrame.ScreenColumns + i);
                for (int row = 0; row < leftColumn.Length; row++)
                {
                    leftColumn[row] = Tilemaps.Entry(tilemap, left, top + row);
                    rightColumn[row] = Tilemaps.Entry(tilemap, right, top + row);
                }
            }
        }
    }
}
