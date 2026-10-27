using NDSRenderer;
using NTRModern.ARM9;

namespace PMDSpace.Patches;

internal static class OffScreenActors
{
    private const uint GroundStatePointers = 0x02324CF4; // GROUND_STATE_PTRS
    private const uint ActorList = 0x08;                  // main_ground_data.actors
    private const uint ActorCount = 24;                   // live_actor_list.actors
    private const uint ActorSize = 0x250;                 // live_actor
    private const uint Kind = 0x02;                       // live_entity.kind
    private const uint IsEnabled = 0x04;                  // live_entity.is_enabled
    private const uint HalfSize = 0x14;                   // live_entity.collision_box_size_div2
    private const uint InitialPosition = 0x20;            // live_entity.initial_pos
    private const uint LimitMin = 0x28;                   // live_entity.limit_min_pos
    private const uint LimitMax = 0x30;                   // live_entity.limit_max_pos
    private const uint IsMoving = 0x7C;                   // script_routine_state.field_0x38
    private const uint MoveStart = 0x88;                  // script_routine_state.field_0x44
    private const uint MoveTarget = 0x90;                 // script_routine_state.field_0x4c
    private const uint CoordMin = 0x15C;                  // live_actor.coord_min
    private const uint CoordMax = 0x164;                  // live_actor.coord_max
    private const uint ScreenCamera = 0x023891F8;         // ov11_023891F8
    private const uint CameraPixel = 0x48;
    private const short NoKind = -1;
    private const int FractionBits = 8;

    private static readonly Dictionary<uint, int> MovedTargets = [];

    public static void Install()
    {
        GameFrame.Finished += _ =>
        {
            int margin = StackedScenes.ActorMargin;
            uint actors = margin == 0 ? 0 : Memory.Peek<uint>(GroundStatePointers + ActorList);
            if (actors == 0)
            {
                MovedTargets.Clear();
                return;
            }

            int left = Memory.Peek<int>(ScreenCamera + CameraPixel) << FractionBits;
            var screen = (Left: left, Right: left + (FrameRenderer.Width << FractionBits));
            int shift = margin << FractionBits;
            for (uint index = 0; index < ActorCount; index++)
            {
                uint actor = actors + (index * ActorSize);
                if (Memory.Peek<byte>(actor + IsEnabled) != 0 && Memory.Peek<short>(actor + Kind) != NoKind)
                {
                    MoveOut(actor, screen, shift);
                }
            }
        };
    }

    public static bool IsAnyActorLive()
    {
        uint actors = Memory.Peek<uint>(GroundStatePointers + ActorList);
        for (uint index = 0; actors != 0 && index < ActorCount; index++)
        {
            uint actor = actors + (index * ActorSize);
            if (Memory.Peek<byte>(actor + IsEnabled) != 0 && Memory.Peek<short>(actor + Kind) != NoKind)
            {
                return true;
            }
        }

        return false;
    }

    private static void MoveOut(uint actor, (int Left, int Right) screen, int shift)
    {
        int half = Memory.Peek<int>(actor + HalfSize);
        if (Memory.Peek<short>(actor + IsMoving) != 0)
        {
            int start = Memory.Peek<int>(actor + MoveStart);
            int target = Memory.Peek<int>(actor + MoveTarget);
            int side = Side(target - half, target + half, screen);
            if (side != 0 && Side(start - half, start + half, screen) == 0 && !(MovedTargets.TryGetValue(actor, out int moved) && moved == target))
            {
                Memory.Poke<int>(actor + MoveTarget) = target + (side * shift);
                MovedTargets[actor] = target + (side * shift);
            }

            return;
        }

        ref int min = ref Memory.Poke<int>(actor + CoordMin);
        ref int max = ref Memory.Poke<int>(actor + CoordMax);
        int waiting = Side(min, max, screen);
        if (waiting != 0 && min + half == Memory.Peek<int>(actor + InitialPosition))
        {
            min += waiting * shift;
            max += waiting * shift;
            Memory.Poke<int>(actor + (waiting < 0 ? LimitMin : LimitMax)) += waiting * shift;
        }
    }

    private static int Side(int min, int max, (int Left, int Right) screen)
    {
        return max <= screen.Left ? -1 : min >= screen.Right ? 1 : 0;
    }
}
