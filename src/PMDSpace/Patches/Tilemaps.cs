using System.Runtime.InteropServices;
using NTRModern.ARM9;

namespace PMDSpace.Patches;

[StructLayout(LayoutKind.Explicit)]
internal struct Tilemap // unkStruct_0200B330
{
    public const byte Size512x256 = 1;

    private const uint EngineBRegisters = 0x1000;
    private const uint BG0HOFS = 0x10;

    [FieldOffset(0x00)] // unkStruct_0200B330.unk0
    public byte Size;

    [FieldOffset(0x01)] // unkStruct_0200B330.unk1
    public byte IsCopyScheduled;

    [FieldOffset(0x04)]
    public int ScrollX;

    [FieldOffset(0x08)]
    public int ScrollY;

    [FieldOffset(0x0C)]
    public uint ScrollRegister;

    [FieldOffset(0x10)]
    public uint VRAM;

    [FieldOffset(0x14)]
    public uint VRAMSize;

    [FieldOffset(0x18)]
    public uint Entries;

    public readonly int Engine => (ScrollRegister & EngineBRegisters) != 0 ? 1 : 0;

    public readonly int Background => (int)(((ScrollRegister & 0xFF) - BG0HOFS) / 4);
}

internal static class Tilemaps
{
    private const uint Table = 0x020B09B0; // _020B09B0
    private const uint ScreenStride = 0x30;
    private const uint LayerStride = 0x18;
    private const uint FirstTilemap = 0x08;

    public static uint Address(int screen, int layer)
    {
        return Memory.Peek<uint>(Memory.Peek<uint>(Table) + FirstTilemap + ((uint)screen * ScreenStride) + ((uint)layer * LayerStride));
    }

    public static ref ushort Entry(in Tilemap tilemap, int column, int row)
    {
        if (tilemap.Size != Tilemap.Size512x256)
        {
            throw new NotSupportedException($"The game drew on a tilemap of size {tilemap.Size}, which isn't supported.");
        }

        uint offset = ((column & 32) != 0 ? 0x800u : 0) + ((uint)(row & 31) * 64) + ((uint)(column & 31) * 2);
        return ref Memory.Poke<ushort>(tilemap.Entries + offset);
    }
}

internal sealed class TilemapCopy
{
    private Tilemap _tilemap;
    private ushort[] _entries = [];

    public int Engine => _tilemap.Engine;

    public int Background => _tilemap.Background;

    public void Copy(uint address)
    {
        _tilemap = Memory.Peek<Tilemap>(address);
        if (_entries.Length != EntryCount(_tilemap))
        {
            _entries = new ushort[EntryCount(_tilemap)];
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            _entries[i] = Memory.Peek<ushort>(_tilemap.Entries + (uint)(i * sizeof(ushort)));
        }
    }

    public bool IsUnchanged(uint address)
    {
        ref readonly var tilemap = ref Memory.Peek<Tilemap>(address);
        if (tilemap.Entries != _tilemap.Entries
            || tilemap.ScrollRegister != _tilemap.ScrollRegister
            || tilemap.ScrollX != _tilemap.ScrollX
            || tilemap.ScrollY != _tilemap.ScrollY
            || EntryCount(tilemap) != _entries.Length)
        {
            return false;
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            if (Memory.Peek<ushort>(tilemap.Entries + (uint)(i * sizeof(ushort))) != _entries[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int EntryCount(in Tilemap tilemap) => tilemap.Entries == 0 ? 0 : (int)tilemap.VRAMSize / sizeof(ushort);
}
