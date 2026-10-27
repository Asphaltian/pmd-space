using NTRModern;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class FullScreenRectangles
{
    private const uint Translation = 0x00;    // render_3d_element_64.vec
    private const uint Size = 0x04;           // render_3d_element_64.vec
    private const uint Type = 0x3C;           // render_3d_element_64.type
    private const byte Render64Rectangle = 2; // RENDER64_RECTANGLE
    private const int ScreenWidth = 0x100;

    public static void Install()
    {
        var render = Funcs.Patches.sub_0201F2A0;
        Funcs.Patches.sub_0201F2A0 = ctx =>
        {
            uint element = ctx.R0;
            int width = GameFrame.Margins.Width;
            if (width == 0 || !IsFullScreen(element))
            {
                render(ctx);
                return;
            }

            Memory.Poke<short>(element + Translation) = (short)-width;
            Memory.Poke<short>(element + Size) = (short)(ScreenWidth + (2 * width));
            render(ctx);
            Memory.Poke<short>(element + Translation) = 0;
            Memory.Poke<short>(element + Size) = ScreenWidth;
        };
    }

    public static bool IsFullScreen(uint element)
    {
        return Memory.Peek<byte>(element + Type) == Render64Rectangle
            && Memory.Peek<short>(element + Translation) == 0
            && Memory.Peek<short>(element + Translation + 2) == 0
            && Memory.Peek<short>(element + Size) == ScreenWidth
            && Memory.Peek<short>(element + Size + 2) == Video.VisibleLines;
    }
}
