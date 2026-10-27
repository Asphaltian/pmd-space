using System.Runtime.InteropServices;
using NTRModern.ARM9;

namespace PMDSpace.Patches;

[StructLayout(LayoutKind.Explicit)]
internal struct DisplayData
{
    private const uint DungeonPointer = 0x02353538; // DUNGEON_PTR
    private const uint Offset = 0x1A21C;            // dungeon.display_data

    [FieldOffset(0x00)] // display_data.camera_pos
    public Position CameraPos;

    [FieldOffset(0x08)] // display_data.camera_pixel_pos
    public Position CameraPixelPos;

    public static uint Address => Memory.Peek<uint>(DungeonPointer) + Offset;

    public static ref readonly DisplayData Current => ref Memory.Peek<DisplayData>(Address);

    public static ref DisplayData Changeable => ref Memory.Poke<DisplayData>(Address);

    [StructLayout(LayoutKind.Sequential)]
    public struct Position // position
    {
        public short X;
        public short Y;
    }
}
