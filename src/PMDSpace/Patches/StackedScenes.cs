using LibRecomp;
using NDSRenderer;
using NTRModern;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class StackedScenes
{
    private const uint MasterBrightA = 0x0400006C;
    private const uint MasterBrightB = 0x0400106C;
    private const int Darkest = -16;
    private const uint TopScreenState = 0x02324E00;        // ov11_02324E00
    private const uint TopScreenOffset = 0x023239B4;       // ov11_023239B4
    private const uint SecondTopScreenState = 0x02324E08;  // ov11_02324E08
    private const uint SecondTopScreenOffset = 0x02323A98; // ov11_02323A98
    private const uint StateValue = 0x04;
    private const int ActiveState = 3;
    private const uint OffsetY = 0x04;
    private const uint ScreenCamera = 0x023891F8; // ov11_023891F8
    private const uint CameraPosition = 0x18;
    private const uint CameraShake = 0x40;
    private const uint CameraPixel = 0x48;
    private const int CameraOriginX = 0x8080;
    private const int CameraOriginY = 0x6B80;
    private const float SubPixels = 256;
    private const int SubScreen = 1;
    private const int TileSize = 8;
    private const int NarrowMapReach = 27;
    private const int FountainReach = 11;
    private const uint GameMode = 0x020AFF70; // GAME_MODE
    private const byte GameModeMenu = 0;      // GAME_MODE_MENU

    private static readonly Scene[] Scenes =
    [
        Still(map: 0x121, ScreenSizing.Cropped, center: 288),
        new(Map: 0x122, Top: 0x122, ScreenSizing.Scrolled, Axis.Y, CameraFrom: 456, CameraTo: 272, CenterFrom: 148, CenterTo: 246),
        new(Map: 0x124, Top: 0x123, ScreenSizing.Scrolled, Axis.X, CameraFrom: 3.5f, CameraTo: 103.5f, CenterFrom: 96, CenterTo: 288) { HoldsCameras = true },
        Still(map: 0x125, ScreenSizing.Scrolled, center: 96),
        Still(map: 0xF8, ScreenSizing.Scrolled, center: 288) with { MovesActorsOut = true },
        Still(map: 0x184, ScreenSizing.Cropped, center: 288) with { CroppedWidth = NarrowMapReach },
        Still(map: 0x164, ScreenSizing.Cropped, center: 280) with { CroppedWidth = FountainReach },
        Still(map: 0x193, ScreenSizing.Cropped, center: 280) with { CroppedWidth = NarrowMapReach },
        Still(map: 0x128, ScreenSizing.Cropped, center: 288) with { CroppedWidth = NarrowMapReach },
        Still(map: 0x126, ScreenSizing.Scrolled, center: 312) with { NeedsTopScreen = true, MovesActorsOut = true },
        new(Map: 0x129, Top: 0x129, ScreenSizing.Cropped, Axis.Y, CameraFrom: 344, CameraTo: 192, CenterFrom: 312, CenterTo: 96),
        Still(map: 0x154, ScreenSizing.Cropped, center: 288) with { CroppedWidth = NarrowMapReach, FirstLine = Video.VisibleLines },
    ];

    private static readonly Dictionary<(short Map, int Render), (int First, int Last)> ArtColumns = [];

    private static Scene? _scene;
    private static float? _placedCamera;
    private static Scene? _topScene;

    public static void Install()
    {
        var render = Funcs.Patches.ov11_022EF70C;
        Funcs.Patches.ov11_022EF70C = ctx =>
        {
            uint groundBg = ctx.R0;
            ref var ground = ref Memory.Poke<GroundBg>(groundBg);
            short map = ground.BackgroundId;
            if (Find(map) is not { } scene)
            {
                if (groundBg == GroundBg.Main)
                {
                    Show(null);
                }

                render(ctx);
                return;
            }

            if (groundBg == GroundBg.Main)
            {
                GameFrame.Margins.CroppedWidth = scene.CroppedWidth;
                Show(scene);
            }
            else
            {
                _topScene = scene;
            }

            if (!scene.HoldsCameras)
            {
                render(ctx);
                return;
            }

            int shift = HeldX(groundBg, scene, 0) - ground.Camera.X;
            int secondShift = ground.CameraCount > 1 ? HeldX(groundBg, scene, 1) - ground.SecondCamera.X : 0;
            ground.Camera.X += shift;
            ground.SecondCamera.X += secondShift;
            render(ctx);
            ground.Camera.X -= shift;
            ground.SecondCamera.X -= secondShift;
        };

        var updateTopScreen = Funcs.Patches.ov11_023117C0;
        Funcs.Patches.ov11_023117C0 = ctx => JoinScreens(ctx, updateTopScreen, TopScreenOffset);

        var updateSecondTopScreen = Funcs.Patches.ov11_02311CC0;
        Funcs.Patches.ov11_02311CC0 = ctx => JoinScreens(ctx, updateSecondTopScreen, SecondTopScreenOffset);

        var screenCamera = Funcs.Patches.ov11_022EB3C8;
        Funcs.Patches.ov11_022EB3C8 = ctx =>
        {
            var (screen, position) = ((int)ctx.R0, ctx.R1);
            screenCamera(ctx);
            if (screen == SubScreen && IsShown())
            {
                Memory.Poke<GroundBg.PixelPosition>(position).Y += JoinShift();
            }
        };

        var renderElement = Funcs.Patches.sub_0201F2A0;
        Funcs.Patches.sub_0201F2A0 = ctx =>
        {
            if (!ShowsBothScreens() || !FullScreenRectangles.IsFullScreen(ctx.R0))
            {
                renderElement(ctx);
            }
        };

        var setMasterBrightness = Funcs.Patches.GXx_SetMasterBrightness_;
        Funcs.Patches.GXx_SetMasterBrightness_ = ctx =>
        {
            if (ShowsBothScreens() && !ReferenceEquals(_topScene, _scene))
            {
                ctx.R1 = unchecked((uint)Darkest);
            }

            var (register, brightness, returnAddress) = (ctx.R0, ctx.R1, ctx.R14);
            setMasterBrightness(ctx);
            if (register == MasterBrightB && ShowsBothScreens())
            {
                (ctx.R0, ctx.R1, ctx.R14) = (MasterBrightA, brightness, returnAddress);
                setMasterBrightness(ctx);
            }
        };

        GameFrame.Finished += _ =>
        {
            if (_scene is { } shown && !IsShowing(shown))
            {
                Show(null);
            }

            if (_scene is { } scene)
            {
                GameFrame.Sizing = scene.Sizing;
                GameFrame.Margins.CroppedWidth = scene.CroppedWidth;
                GameFrame.Scroll = ShownScroll(scene);
            }
        };
    }

    private static Scene Still(short map, ScreenSizing sizing, int center) => new(map, map, sizing, Axis.Y, 0, 0, center, center);

    public static bool IsInTopMenu => Memory.Peek<byte>(GameMode) == GameModeMenu;

    public static int ActorMargin => _scene is { MovesActorsOut: true } scene ? scene.Margin : 0;

    private static Scene? Find(short map)
    {
        return IsInTopMenu ? Array.Find(Scenes, scene => (scene.Map == map || scene.Top == map) && (!scene.NeedsTopScreen || ActiveTopScreen() is not null)) : null;
    }

    private static (uint State, uint Offset)? ActiveTopScreen()
    {
        foreach (var (pointer, offset) in new[] { (TopScreenState, TopScreenOffset), (SecondTopScreenState, SecondTopScreenOffset) })
        {
            uint state = Memory.Peek<uint>(pointer);
            if (state != 0 && Memory.Peek<int>(state + StateValue) == ActiveState)
            {
                return (state, offset);
            }
        }

        return null;
    }

    private static bool IsShown()
    {
        return GroundBg.Main != 0 && Find(Memory.Peek<GroundBg>(GroundBg.Main).BackgroundId) is { } scene && IsShowing(scene);
    }

    private static bool IsShowing(Scene scene)
    {
        return GroundBg.Main != 0 && Memory.Peek<GroundBg>(GroundBg.Main).BackgroundId == scene.Map && Find(scene.Map) == scene;
    }

    private static void Show(Scene? scene)
    {
        if (!ReferenceEquals(scene, _scene))
        {
            (_scene, _placedCamera) = (scene, null);
            _topScene = scene is null ? null : _topScene;
        }
    }

    private static bool ShowsBothScreens()
    {
        return _scene is { } scene && ShownScroll(scene) < Video.VisibleLines && ShownScroll(scene) + scene.Lines > Video.VisibleLines;
    }

    private static float ShownScroll(Scene scene)
    {
        if (Memory.Peek<int>(ScreenCamera + CameraPosition) != 0 || Memory.Peek<int>(ScreenCamera + CameraPosition + sizeof(int)) != 0)
        {
            uint axisOffset = scene.Axis == Axis.Y ? sizeof(int) : 0u;
            _placedCamera = scene.HoldsCameras ? SubPixelCamera(scene.Axis, axisOffset) : Memory.Peek<int>(ScreenCamera + CameraPixel + axisOffset);
        }

        return scene.Scroll(_placedCamera ?? scene.CameraFrom);
    }

    private static float SubPixelCamera(Axis axis, uint axisOffset)
    {
        int position = Memory.Peek<int>(ScreenCamera + CameraPosition + axisOffset) + Memory.Peek<int>(ScreenCamera + CameraShake + axisOffset);
        return (position - (axis == Axis.Y ? CameraOriginY : CameraOriginX)) / SubPixels;
    }

    private static void JoinScreens(RecompContext ctx, RecompFunc update, uint offset)
    {
        if (!IsShown())
        {
            update(ctx);
            return;
        }

        ref int y = ref Memory.Poke<int>(offset + OffsetY);
        int original = y;
        y = -Video.VisibleLines;
        update(ctx);
        y = original;
    }

    private static int JoinShift()
    {
        return ActiveTopScreen() is { } topScreen ? -Video.VisibleLines - Memory.Peek<int>(topScreen.Offset + OffsetY) : 0;
    }

    private static int HeldX(uint groundBg, Scene scene, int render)
    {
        var key = (Memory.Peek<GroundBg>(groundBg).BackgroundId, render);
        if (!ArtColumns.ContainsKey(key))
        {
            ArtColumns[key] = MapRender.ArtBounds(GroundBg.MapRenderAddress(groundBg, render)).Columns;
        }

        var (first, last) = (int.MaxValue, int.MinValue);
        foreach (short map in new[] { scene.Map, scene.Top })
        {
            if (ArtColumns.TryGetValue((map, render), out var columns))
            {
                (first, last) = (Math.Min(first, columns.First), Math.Max(last, columns.Last));
            }
        }

        return ((first + last + 1) * TileSize / 2) - (FrameRenderer.Width / 2);
    }

    private enum Axis
    {
        X,
        Y,
    }

    private sealed record Scene(short Map, short Top, ScreenSizing Sizing, Axis Axis, float CameraFrom, float CameraTo, int CenterFrom, int CenterTo)
    {
        public bool HoldsCameras { get; init; }

        public bool NeedsTopScreen { get; init; }

        public int CroppedWidth { get; init; }

        public bool MovesActorsOut { get; init; }

        public int? FirstLine { get; init; }

        public int Margin => Sizing == ScreenSizing.Cropped ? CroppedWidth : GameFrame.Margins.Width;

        public int Lines => Sizing == ScreenSizing.Cropped ? GameFrame.Margins.CroppedLines : Video.VisibleLines;

        public float Scroll(float camera)
        {
            float travel = Math.Clamp(camera, Math.Min(CameraFrom, CameraTo), Math.Max(CameraFrom, CameraTo)) - CameraFrom;
            float center = CameraTo == CameraFrom ? CenterFrom : CenterFrom + ((CenterTo - CenterFrom) * travel / (CameraTo - CameraFrom));
            return Math.Clamp(FirstLine ?? (center - (Lines / 2)), 0, (2 * Video.VisibleLines) - Lines);
        }
    }
}
