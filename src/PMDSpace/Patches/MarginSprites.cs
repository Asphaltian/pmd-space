using LibRecomp;
using NDSRenderer;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class MarginSprites
{
    private const uint PixelPosX = 0x0C; // entity.pixel_pos
    private const ushort ObjectX = 0x1FF;
    private const int PieceReachLeft = -64;
    private const int LowestObjectX = -128;

    private static readonly (int Left, int Right) ItemReach = (-32, 287);
    private static readonly (int Left, int Right) ShadowReach = (-16, 270);
    private static readonly (int Left, int Right) MonsterReach = (-64, 319);

    private static int _shift;

    public static void Install()
    {
        var addSimpleObj = Funcs.Patches.AddSimpleObjToOam;
        Funcs.Patches.AddSimpleObjToOam = ctx =>
        {
            if (_shift == 0)
            {
                addSimpleObj(ctx);
                return;
            }

            ref ushort attribute1 = ref Memory.Poke<ushort>(ctx.R1 + 2);
            ushort placed = attribute1;
            attribute1 = (ushort)((placed & ~ObjectX) | ((placed + _shift) & ObjectX));
            addSimpleObj(ctx);
            attribute1 = placed;
        };

        var displayItem = Funcs.Patches.DisplayItem;
        Funcs.Patches.DisplayItem = ctx => Draw(ctx, displayItem, EntityScreenX(ctx.R0), ItemReach);

        var displayShadow = Funcs.Patches.DisplayThrownItemShadow;
        Funcs.Patches.DisplayThrownItemShadow = ctx => Draw(ctx, displayShadow, (int)ctx.R0 - DisplayData.Current.CameraPixelPos.X, ShadowReach);

        var shouldDisplayMonster = Funcs.Patches.ov29_022E273C;
        Funcs.Patches.ov29_022E273C = ctx =>
        {
            uint entity = ctx.R0;
            shouldDisplayMonster(ctx);
            int shift = ShiftInto(EntityScreenX(entity), MonsterReach);
            if (ctx.R0 == 0 && shift != 0)
            {
                ctx.R0 = entity;
                WithCameraShifted(shift, () => shouldDisplayMonster(ctx));
            }
        };

        var isPieceOffScreen = Funcs.Patches.sub_0201B9B0;
        Funcs.Patches.sub_0201B9B0 = ctx =>
        {
            int x = (int)ctx.R0;
            isPieceOffScreen(ctx);
            int width = GameFrame.Margins.Width;
            if (width != 0)
            {
                ctx.R0 = x < Math.Max(PieceReachLeft - width, LowestObjectX) || x >= FrameRenderer.Width + width ? 1u : 0u;
            }
        };
    }

    private static int EntityScreenX(uint entity)
    {
        return (Memory.Peek<int>(entity + PixelPosX) >> 8) - DisplayData.Current.CameraPixelPos.X;
    }

    private static void Draw(RecompContext ctx, RecompFunc display, int x, (int Left, int Right) reach)
    {
        var (r0, r1, r2, r3) = (ctx.R0, ctx.R1, ctx.R2, ctx.R3);
        display(ctx);
        int shift = ShiftInto(x, reach);
        if (ctx.R0 != 0 || shift == 0)
        {
            return;
        }

        WithCameraShifted(shift, () =>
        {
            _shift = shift;
            (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (r0, r1, r2, r3);
            display(ctx);
            _shift = 0;
        });
        ctx.R0 = 0;
    }

    private static int ShiftInto(int x, (int Left, int Right) reach)
    {
        int width = GameFrame.Margins.Width;
        if (x < reach.Left - width || x > reach.Right + width)
        {
            return 0;
        }

        return x < reach.Left ? -width : x > reach.Right ? width : 0;
    }

    private static void WithCameraShifted(int shift, Action run)
    {
        Scheduler.RunUntimed(() =>
        {
            ref short cameraX = ref DisplayData.Changeable.CameraPixelPos.X;
            cameraX += (short)shift;
            run();
            cameraX -= (short)shift;
        });
    }
}
