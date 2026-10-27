using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class PersonalityTestBow
{
    private const uint PersonalityTestPointer = 0x0238CEA0; // PERSONALITY_TEST_PTR
    private const uint LoadedOverlayGroups = 0x020AF230;   // LOADED_OVERLAY_GROUP_0
    private const int OverlayGroupSlots = 3;
    private const int Overlay13Group = 17;                 // OGROUP_OVERLAY_13
    private const uint PositionY = 0x04;
    private const int Raise = 16 << 8;

    private static bool _isDrawing;

    public static void Install()
    {
        var draw = Funcs.Patches.ov11_022FD510;
        Funcs.Patches.ov11_022FD510 = ctx =>
        {
            _isDrawing = IsOverlay13Loaded() && Memory.Peek<uint>(PersonalityTestPointer) != 0;
            draw(ctx);
            _isDrawing = false;
        };

        var animate = Funcs.Patches.AnimRelatedFunction__022F6F14;
        Funcs.Patches.AnimRelatedFunction__022F6F14 = ctx =>
        {
            if (!_isDrawing)
            {
                animate(ctx);
                return;
            }

            ref int y = ref Memory.Poke<int>(ctx.R1 + PositionY);
            y -= Raise;
            uint position = ctx.R1;
            animate(ctx);
            Memory.Poke<int>(position + PositionY) += Raise;
        };
    }

    private static bool IsOverlay13Loaded()
    {
        for (uint slot = 0; slot < OverlayGroupSlots; slot++)
        {
            if (Memory.Peek<int>(LoadedOverlayGroups + (slot * sizeof(int))) == Overlay13Group)
            {
                return true;
            }
        }

        return false;
    }
}
