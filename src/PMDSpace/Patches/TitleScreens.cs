using System.Text;
using NDSRenderer;
using NTRModern;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class TitleScreens
{
    private const short TitleCardMap = 0x126;
    private const short TitleMap = 0x154;
    private const short LogoMap = 0x11F;                  // S13P01A
    private const short LogoMapB = 0x120;                 // S13P01B
    private const int EngineA = 0;
    private const int EngineB = 1;
    private const string Subtitle = "GROUND/s05p02c1.wan";
    private const uint WanTable = 0x020AFC68;             // WAN_TABLE
    private const uint WanTableEntrySize = 0x38;          // wan_table_entry
    private const int WanPathLength = 32;
    private const uint LoadedSpriteId = 0x7C;             // animation.ctrl.loaded_sprite_id
    private const uint ObjGraphicsControls = 0x020AFC4C;  // OBJ_GRAPHICS_CONTROLS_PTR
    private const uint CurrentObject = 0x28;              // obj_graphics_control.oam_info.cur_obj_idx
    private const uint Objects = 0x30;                    // obj_graphics_control.oam_info.ungrouped_oam_objs
    private const uint ObjectSize = 8;                    // ungrouped_oam_obj
    private const int ProhibitedShape = 3;
    private const int ScreenLines = 192;
    private const int LineWrap = 256;
    private const float CardLogoScale = 2f / 3f;
    private const int CardLogoTop = 6;
    private const int CardSubtitleTop = 91;
    private const int SubtitleTop = 52;
    private const int TitleSubtitleTop = 152;
    private const float TitleSubtitleScale = 0.75f;
    private const int TitleGap = 2;

    private static readonly Piece Logo = new(5, 32, 245, 112);
    private static readonly Piece TitleSubtitle = new(49, TitleSubtitleTop, 160, 32);
    private static readonly Piece GearAndPressStart = new(0, 48, FrameRenderer.Width, 97);

    private static short _renderedMap;
    private static bool _isLogoRendered;

    public static void Install()
    {
        var render = Funcs.Patches.ov11_022EF70C;
        Funcs.Patches.ov11_022EF70C = ctx =>
        {
            uint groundBg = ctx.R0;
            short map = Memory.Peek<GroundBg>(groundBg).BackgroundId;
            if (map is LogoMap or LogoMapB)
            {
                _isLogoRendered = true;
            }
            else if (groundBg == GroundBg.Main)
            {
                _renderedMap = map;
            }

            render(ctx);
        };

        var drawSprite = Funcs.Patches.ov11_022F5460;
        Funcs.Patches.ov11_022F5460 = ctx =>
        {
            short shown = ShownMap;
            if (shown == 0 || SpriteFile(Memory.Peek<ushort>(ctx.R0 + LoadedSpriteId)) != Subtitle)
            {
                drawSprite(ctx);
                return;
            }

            uint control = Memory.Peek<uint>(ObjGraphicsControls);
            int first = Memory.Peek<int>(control + CurrentObject);
            drawSprite(ctx);
            int last = Memory.Peek<int>(control + CurrentObject);
            MoveObjects(Memory.Peek<uint>(control + Objects), first, last, shown == TitleCardMap ? CardSubtitleTop : TitleSubtitleTop);
        };

        GameFrame.Finished += _ =>
        {
            short shown = ShownMap;
            if (shown == TitleCardMap)
            {
                ShowCard();
            }
            else if (shown == TitleMap)
            {
                ShowTitle();
            }

            (_renderedMap, _isLogoRendered) = (0, false);
        };
    }

    private static short ShownMap => _isLogoRendered && StackedScenes.IsInTopMenu && _renderedMap is TitleCardMap or TitleMap ? _renderedMap : (short)0;

    private static void MoveObjects(uint objects, int first, int last, int top)
    {
        int highest = int.MaxValue;
        for (int index = first; index < last; index++)
        {
            ushort attribute0 = Memory.Peek<ushort>(objects + ((uint)index * ObjectSize));
            if (attribute0 >> 14 != ProhibitedShape)
            {
                highest = Math.Min(highest, Line(attribute0));
            }
        }

        for (int index = first; index < last; index++)
        {
            ref ushort attribute0 = ref Memory.Poke<ushort>(objects + ((uint)index * ObjectSize));
            if (attribute0 >> 14 != ProhibitedShape)
            {
                attribute0 = (ushort)((attribute0 & 0xFF00) | ((top + Line(attribute0) - highest) & 0xFF));
            }
        }
    }

    private static int Line(ushort attribute0) => (attribute0 & 0xFF) >= ScreenLines ? (attribute0 & 0xFF) - LineWrap : attribute0 & 0xFF;

    private static void ShowCard()
    {
        Add(EngineB, ScreenLayers.BG2, Logo, FrameRenderer.Width + (2 * GameFrame.Margins.Width), CardLogoTop, CardLogoScale);
    }

    private static void ShowTitle()
    {
        float width = FrameRenderer.Width + (2 * GameFrame.Margins.CroppedWidth);
        float top = Video.VisibleLines - GameFrame.Scroll;
        float subtitleY = top + SubtitleTop + ((1 - TitleSubtitleScale) * TitleSubtitle.Height);
        float logoScale = Math.Min(1, (subtitleY - (2 * TitleGap)) / Logo.Height);
        GameFrame.Insets.Hide(EngineA, ScreenLayers.Objects);
        Add(EngineB, ScreenLayers.BG2, Logo, width, (subtitleY - (Logo.Height * logoScale)) / 2, logoScale);
        Add(EngineA, ScreenLayers.Objects, GearAndPressStart, width, top + GearAndPressStart.Y, 1);
        Add(EngineA, ScreenLayers.Objects, TitleSubtitle, width, subtitleY, TitleSubtitleScale);
    }

    private static void Add(int engine, ScreenLayers layers, Piece piece, float viewWidth, float y, float scale)
    {
        GameFrame.Insets.Add(engine, layers, piece.X, piece.Y, piece.Width, piece.Height, (viewWidth - (piece.Width * scale)) / 2, y, scale);
    }

    private static string SpriteFile(ushort sprite)
    {
        uint entry = Memory.Peek<uint>(WanTable) + (sprite * WanTableEntrySize);
        var path = new StringBuilder();
        for (uint i = 0; i < WanPathLength; i++)
        {
            byte c = Memory.Peek<byte>(entry + i);
            if (c == 0)
            {
                break;
            }

            path.Append((char)c);
        }

        return path.ToString();
    }

    private sealed record Piece(int X, int Y, int Width, int Height);
}
