using System.Runtime.InteropServices;
using NTRModern.ARM9;

namespace PMDSpace.Patches;

[StructLayout(LayoutKind.Explicit)]
internal struct GroundBg // ground_bg
{
    private const uint MapState = 0x02324CC0;     // GROUND_STATE_MAP
    private const uint TopScreenState = 0x02324E00; // ov11_02324E00
    private const uint TopScreenMap = 0x04;
    private const uint TilemapTable = 0x02324CBC; // ov11_02324CBC
    private const uint ScreenStride = 0x38;
    private const uint TilemapSize = 0x1C;
    private const uint MapRenders = 0x210;       // ground_bg.map_render
    private const uint MapRenderSize = 0x54;

    [FieldOffset(0x1BE)] // ground_bg.field_0x1be
    public short BackgroundId;

    [FieldOffset(0x1FC)] // ground_bg.field_0x1fc
    public int CameraCount;

    [FieldOffset(0x200)] // ground_bg.camera_pixel_position
    public PixelPosition Camera;

    [FieldOffset(0x208)] // ground_bg.camera_pixel_position
    public PixelPosition SecondCamera;

    [FieldOffset(0x2BC)] // ground_bg_sub_struct_2bc.field_0x0
    public byte Screen;

    [FieldOffset(0x2BE)] // ground_bg_sub_struct_2bc.field_0x2
    public short FirstTilemap;

    [FieldOffset(0x2C0)] // ground_bg_sub_struct_2bc.num_layers
    public short LayerCount;

    public static uint Main => Memory.Peek<uint>(MapState);

    public static uint TopScreen => Memory.Peek<uint>(TopScreenState + TopScreenMap);

    public static uint MapRenderAddress(uint groundBg, int index) => groundBg + MapRenders + ((uint)index * MapRenderSize);

    public void MoveCameras(int y)
    {
        Camera.Y += y;
        if (CameraCount > 1)
        {
            SecondCamera.Y += y;
        }
    }

    public readonly uint TilemapAddress(int layer)
    {
        return Memory.Peek<uint>(TilemapTable) + (Screen * ScreenStride) + ((uint)(FirstTilemap + layer) * TilemapSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PixelPosition // pixel_position
    {
        public int X;
        public int Y;
    }
}

[StructLayout(LayoutKind.Explicit)]
internal struct MapRender // map_render
{
    public const short Chunks3x3 = 1;

    public const int ChunkSize = 3;
    private const int ChunkMapWidth = 64;
    private const uint ChunkTilesOffset = 0x1C;  // MapRender.unk1C
    private const uint ChunkMapOffset = 0x24;    // MapRender.unk24

    [FieldOffset(0x00)] // map_render.chunk_dimensions
    public short ChunkDimensions;

    [FieldOffset(0x02)] // map_render.field_0x2
    public short FirstTilemap;

    [FieldOffset(0x04)] // map_render.num_bgs
    public short BackgroundCount;

    [FieldOffset(0x06)] // map_render.wrap_around
    public byte WrapAround;

    [FieldOffset(0x08)] // map_render.width_chunks
    public int WidthChunks;

    [FieldOffset(0x0C)] // map_render.height_chunks
    public int HeightChunks;

    [FieldOffset(0x34)]
    public GroundBg.PixelPosition Camera;

    public static ushort Entry(uint mapRender, int background, int column, int row)
    {
        ref readonly var map = ref Memory.Peek<MapRender>(mapRender);
        var (chunkColumn, tileColumn) = InChunks(column);
        var (chunkRow, tileRow) = InChunks(row);
        if (map.WrapAround != 0)
        {
            chunkColumn = ((chunkColumn % map.WidthChunks) + map.WidthChunks) % map.WidthChunks;
            chunkRow = ((chunkRow % map.HeightChunks) + map.HeightChunks) % map.HeightChunks;
        }
        else if (chunkColumn < 0 || chunkColumn >= map.WidthChunks || chunkRow < 0 || chunkRow >= map.HeightChunks)
        {
            return 0;
        }

        uint chunks = Memory.Peek<uint>(mapRender + ChunkMapOffset + ((uint)background * 4));
        uint chunkTiles = Memory.Peek<uint>(mapRender + ChunkTilesOffset + ((uint)background * 4));
        ushort chunk = Memory.Peek<ushort>(chunks + ((uint)((chunkRow * ChunkMapWidth) + chunkColumn) * 2));
        return Memory.Peek<ushort>(chunkTiles + ((uint)((chunk * ChunkSize * ChunkSize) + (tileRow * ChunkSize) + tileColumn) * 2));
    }

    public static ((int First, int Last) Columns, (int First, int Last) Rows) ArtBounds(uint mapRender)
    {
        ref readonly var map = ref Memory.Peek<MapRender>(mapRender);
        var columns = (First: int.MaxValue, Last: int.MinValue);
        var rows = (First: int.MaxValue, Last: int.MinValue);
        for (int background = 0; background < map.BackgroundCount; background++)
        {
            for (int column = 0; column < map.WidthChunks * ChunkSize; column++)
            {
                for (int row = 0; row < map.HeightChunks * ChunkSize; row++)
                {
                    if (Entry(mapRender, background, column, row) != 0)
                    {
                        columns = (Math.Min(columns.First, column), Math.Max(columns.Last, column));
                        rows = (Math.Min(rows.First, row), Math.Max(rows.Last, row));
                    }
                }
            }
        }

        return (columns, rows);
    }

    private static (int Chunk, int Tile) InChunks(int tiles)
    {
        int chunk = Math.DivRem(tiles, ChunkSize, out int tile);
        return tile < 0 ? (chunk - 1, tile + ChunkSize) : (chunk, tile);
    }
}
