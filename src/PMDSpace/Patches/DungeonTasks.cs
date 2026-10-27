using LibRecomp;
using RecompiledFuncs.ARM9;
using Memory = NTRModern.ARM9.Memory;

namespace PMDSpace.Patches;

internal static class DungeonTasks
{
    private const uint ov29_023510B8 = 0x023510B8;
    private const uint ov29_023735EC = 0x023735EC;
    private const uint ov29_023735F0 = 0x023735F0;
    private const uint ov29_023735F4 = 0x023735F4;
    private const uint ov29_023735F8 = 0x023735F8;
    private const uint ov29_0237360C = 0x0237360C;
    private const uint ov29_0237368C = 0x0237368C;
    private const uint PushedSize = 0x38;

    public static void Install()
    {
        Funcs.Patches.ov29_022DDE30 = ov29_022DDE30;
        Funcs.Patches.ov29_022DDEF8 = ov29_022DDEF8;
    }

    private static void ov29_022DDE30(RecompContext ctx)
    {
        (uint resumePC, uint resumeSP) = (ctx.R14, ctx.R13);
        Push(ctx);
        ctx.R0 = ov29_023735F8;
        ctx.R1 = ctx.R13;
        Memory.Write32(ctx.R0, ctx.R1);
        ctx.R1 = ov29_023735EC;
        ctx.R2 = 0;
        Memory.Write32(ctx.R1, ctx.R2);
        ctx.R3 = ov29_023510B8;
        ctx.R4 = Memory.Read32(ctx.R3);
        ctx.R3 = ov29_0237360C;
        while (true)
        {
            ctx.R5 = Memory.Read16(ctx.R3);
            if (ctx.R5 is 1 or 2)
            {
                break;
            }

            ctx.R2++;
            ctx.R3 += 0x20;
            if ((int)ctx.R2 >= (int)ctx.R4)
            {
                break;
            }
        }

        Memory.Write32(ctx.R1, ctx.R2);
        Compare(ctx, ctx.R2, ctx.R4);
        if ((int)ctx.R2 >= (int)ctx.R4)
        {
            LibRecomp.ARM9.Recomp.ContinueAt(ctx, Pop(ctx), resumePC, resumeSP);
            return;
        }

        ctx.R4 = ov29_0237368C;
        Memory.Write32(ctx.R4, ctx.R2);
        SwitchTo(ctx, resumePC, resumeSP);
    }

    private static void ov29_022DDEF8(RecompContext ctx)
    {
        (uint resumePC, uint resumeSP) = (ctx.R14, ctx.R13);
        Push(ctx);
        ctx.R0 = ov29_023735F4;
        Memory.Write32(ctx.R0, ctx.R13);
        ctx.R1 = 0x20;
        ctx.R3 = ov29_023735EC;
        ctx.R2 = Memory.Read32(ctx.R3);
        ctx.R1 *= ctx.R2;
        ctx.R3 = ov29_0237360C;
        ctx.R4 = ctx.R3 + ctx.R1;
        ctx.R5 = Memory.Read16(ctx.R4);
        if (ctx.R5 == 1)
        {
            ctx.R5 = 2;
            Memory.Write32(ctx.R4, ctx.R5);
        }

        ctx.R5 = Memory.Read32(ctx.R0);
        Memory.Write32(ctx.R4 + 0xC, ctx.R5);
        ctx.R5 = ov29_023735F0;
        ctx.R5 = Memory.Read32(ctx.R5);
        Memory.Write32(ctx.R4 + 0x10, ctx.R5);
        ctx.R3 = ctx.R4;
        ctx.R1 = ov29_023510B8;
        ctx.R4 = Memory.Read32(ctx.R1);
        while (true)
        {
            ctx.R2++;
            ctx.R3 += 0x20;
            if ((int)ctx.R2 >= (int)ctx.R4)
            {
                break;
            }

            ctx.R5 = Memory.Read16(ctx.R3);
            if (ctx.R5 is 1 or 2)
            {
                break;
            }
        }

        ctx.R6 = ov29_023735EC;
        Memory.Write32(ctx.R6, ctx.R2);
        Compare(ctx, ctx.R2, ctx.R4);
        if ((int)ctx.R2 >= (int)ctx.R4)
        {
            ctx.R0 = ov29_023735F8;
            ctx.R13 = Memory.Read32(ctx.R0);
            LibRecomp.ARM9.Recomp.ContinueAt(ctx, Pop(ctx), resumePC, resumeSP);
            return;
        }

        SwitchTo(ctx, resumePC, resumeSP);
    }

    private static void SwitchTo(RecompContext ctx, uint resumePC, uint resumeSP)
    {
        ctx.R6 = ov29_023735F4;
        ctx.R7 = Memory.Read32(ctx.R3 + 0xC);
        Memory.Write32(ctx.R6, ctx.R7);
        ctx.R6 = ov29_023735F0;
        ctx.R7 = Memory.Read32(ctx.R3 + 0x10);
        Memory.Write32(ctx.R6, ctx.R7);
        Compare(ctx, ctx.R5, 2);
        ctx.R6 = ov29_023735F4;
        ctx.R13 = Memory.Read32(ctx.R6);
        if (ctx.R5 == 2)
        {
            LibRecomp.ARM9.Recomp.ContinueAt(ctx, Pop(ctx), resumePC, resumeSP);
            return;
        }

        ctx.R7 = ov29_023735F0;
        ctx.R2 = Memory.Read32(ctx.R7);
        LibRecomp.ARM9.Recomp.ContinueAt(ctx, ctx.R2, resumePC, resumeSP);
    }

    private static void Compare(RecompContext ctx, uint left, uint right)
    {
        uint result = left - right;
        (ctx.N, ctx.Z, ctx.C, ctx.V) = ((int)result < 0, result == 0, left >= right, (int)((left ^ right) & (left ^ result)) < 0);
    }

    private static void Push(RecompContext ctx)
    {
        uint sp = ctx.R13 - PushedSize;
        uint[] registers = [ctx.R0, ctx.R1, ctx.R2, ctx.R3, ctx.R4, ctx.R5, ctx.R6, ctx.R7, ctx.R8, ctx.R9, ctx.R10, ctx.R11, ctx.R12, ctx.R14];
        for (int index = 0; index < registers.Length; index++)
        {
            Memory.Write32(sp + (uint)(index * 4), registers[index]);
        }

        ctx.R13 = sp;
    }

    private static uint Pop(RecompContext ctx)
    {
        uint sp = ctx.R13;
        (ctx.R0, ctx.R1, ctx.R2, ctx.R3) = (Memory.Read32(sp), Memory.Read32(sp + 4), Memory.Read32(sp + 8), Memory.Read32(sp + 12));
        (ctx.R4, ctx.R5, ctx.R6, ctx.R7) = (Memory.Read32(sp + 16), Memory.Read32(sp + 20), Memory.Read32(sp + 24), Memory.Read32(sp + 28));
        (ctx.R8, ctx.R9, ctx.R10, ctx.R11, ctx.R12) = (Memory.Read32(sp + 32), Memory.Read32(sp + 36), Memory.Read32(sp + 40), Memory.Read32(sp + 44), Memory.Read32(sp + 48));
        uint pc = Memory.Read32(sp + 52);
        ctx.R13 = sp + PushedSize;
        return pc;
    }
}
