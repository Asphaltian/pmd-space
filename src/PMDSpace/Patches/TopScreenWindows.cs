using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class TopScreenWindows
{
    private const uint UpdateDialogueBox = 0x0202F488;
    private const uint TemplateSize = 0x10;           // window_params
    private const uint Update = 0x00;                 // window_params.update
    private const uint Screen = 0x08;                 // window_params.screen
    private const byte ScreenMain = 0;                // SCREEN_MAIN
    private const byte ScreenSub = 1;                 // SCREEN_SUB
    private const uint WindowList = 0x022A88DC;       // WINDOW_LIST
    private const uint WindowSize = 0xE0;             // window
    private const uint Valid = 0xB6;                  // window.valid
    private const uint MasterBrightA = 0x0400006C;
    private const uint SubScreenFade = 0x02389018;    // ov11_02389018
    private const uint DeltaBrightness = 0x14;        // screen_fade.delta_brightness
    private const uint ReturnValue = 0x0E;            // script_routine.states[0].last_return_val
    private const uint CurrentOpcode = 0x34;          // script_routine.states[0].current_opcode_addr
    private const short DeferredOpcode = 5;
    private const ushort BackSetGround = 9;           // OPCODE_BACK_SET_GROUND
    private const int Darkest = -16;

    private static readonly List<int> Darkening = [];

    private static uint? _movedWindow;
    private static Stage _stage;
    private static short? _litMap;
    private static int _fadeFrame;

    private enum Stage { None, Lit, Fading, Done }

    public static void Install()
    {
        var newWindow = Funcs.Patches.NewWindowScreenCheck;
        Funcs.Patches.NewWindowScreenCheck = ctx =>
        {
            uint template = ctx.R0;
            if (Memory.Peek<uint>(template + Update) != UpdateDialogueBox || Memory.Peek<byte>(template + Screen) != ScreenSub)
            {
                newWindow(ctx);
                return;
            }

            ctx.R13 -= TemplateSize;
            uint copy = ctx.R13;
            for (uint offset = 0; offset < TemplateSize; offset += 4)
            {
                Memory.Poke<uint>(copy + offset) = Memory.Peek<uint>(template + offset);
            }

            Memory.Poke<byte>(copy + Screen) = ScreenMain;
            ctx.R0 = copy;
            newWindow(ctx);
            ctx.R13 += TemplateSize;
            _movedWindow = ctx.R0;
        };

        var setMasterBrightness = Funcs.Patches.GXx_SetMasterBrightness_;
        Funcs.Patches.GXx_SetMasterBrightness_ = ctx =>
        {
            if (ctx.R0 == MasterBrightA)
            {
                ctx.R1 = (uint)Brightness((int)ctx.R1);
            }

            setMasterBrightness(ctx);
        };

        var runDeferredOpcode = Funcs.Patches.ov11_022DD2C0;
        Funcs.Patches.ov11_022DD2C0 = ctx =>
        {
            uint routine = ctx.R0;
            bool isSettingGround = Memory.Peek<short>(routine + ReturnValue) == DeferredOpcode && Memory.Peek<ushort>(Memory.Peek<uint>(routine + CurrentOpcode)) == BackSetGround;
            if (isSettingGround && _stage == Stage.Lit)
            {
                (_stage, _fadeFrame) = (Stage.Fading, 0);
            }

            if (_stage == Stage.Fading && _fadeFrame < Darkening.Count)
            {
                _fadeFrame++;
                return;
            }

            _stage = _stage == Stage.Fading ? Stage.Done : _stage;
            runDeferredOpcode(ctx);
        };
    }

    private static int Brightness(int requested)
    {
        if (_stage == Stage.Fading)
        {
            return Darkening[Math.Max(_fadeFrame, 1) - 1];
        }

        if (requested == 0 || !IsMovedWindowOpen() || Memory.Peek<short>(SubScreenFade + DeltaBrightness) != 0 || GroundBg.Main == 0)
        {
            (_stage, _litMap) = (requested == 0 || _stage != Stage.Done ? Stage.None : Stage.Done, null);
            Darkening.Clear();
            return requested;
        }

        short map = Memory.Peek<GroundBg>(GroundBg.Main).BackgroundId;
        if (_stage == Stage.None)
        {
            (_stage, _litMap) = (Stage.Lit, map);
        }

        if (_stage != Stage.Lit || map != _litMap)
        {
            return requested;
        }

        if (Darkening.Count == 0 || Darkening[^1] != Darkest)
        {
            Darkening.Add(requested);
        }

        return 0;
    }

    private static bool IsMovedWindowOpen()
    {
        if (_movedWindow is not { } id)
        {
            return false;
        }

        uint window = WindowList + (id * WindowSize);
        return Memory.Peek<sbyte>(window + Valid) != 0 && Memory.Peek<uint>(window + Update) == UpdateDialogueBox && Memory.Peek<byte>(window + Screen) == ScreenMain;
    }
}
