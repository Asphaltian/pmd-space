using LibRecomp;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class DungeonDarkness
{
    private const uint ov29_0235359C = 0x0235359C;
    private const uint ov29_0237C99C = 0x0237C99C;
    private const uint _020AFC70 = 0x020AFC70;

    private const int ScreenWidth = 0x100;
    private const int ScreenHeight = 0xC0;
    private const int CornerSize = 0x20;
    private const int CornerReachLeft = -0x20;
    private const int CornerReachRight = 0x120;
    private const int CornerReachBottom = 0xE0;
    private const byte NoRoom = 0xFF;

    private const uint ElementSize = 0x40;
    private const uint Vec = 0x00;                    // render_3d_element_64.vec
    private const uint TextureParams = 0x14;          // render_3d_element_64.texture_params
    private const uint TextureTopLeft = 0x16;         // render_3d_element_64.texture_top_left
    private const uint TextureSize = 0x1A;            // render_3d_element_64.texture_size
    private const uint TextureVRAMOffset = 0x20;      // render_3d_element_64.texture_vram_offset
    private const uint PaletteBaseAddr = 0x24;        // render_3d_element_64.palette_base_addr
    private const uint ZIndex = 0x2A;                 // render_3d_element_64.z_index
    private const uint Alpha = 0x2F;                  // render_3d_element_64.color
    private const uint Type = 0x3C;                   // render_3d_element_64.type
    private const uint PolygonID = 0x3E;              // render_3d_element_64.polygon_id
    private const byte Render64Tiling = 5;            // RENDER64_TILING
    private const short BandTextureSize = 4;
    private const short CornerTextureSize = 0x30;

    private const uint RoomSize = 0x1C;               // room_data
    private const uint RoomBounds = 0x0C;
    private const uint DisplayCameraPixelPos = 0x08;  // display_data.camera_pixel_pos
    private const uint DisplayLuminous = 0x23;        // display_data.luminous
    private const uint DisplayNaturalLighting = 0x24; // display_data.natural_lighting
    private const uint DisplayField30 = 0x30;         // display_data.field_0x30
    private const uint DisplayDropeye = 0x34;         // display_data.dropeye

    private static bool _isBuilding;

    public static void Install()
    {
        var build = Funcs.Patches.ov29_022ED184;
        Funcs.Patches.ov29_022ED184 = ctx =>
        {
            var (rooms, room, display) = (ctx.R0, ctx.R1, ctx.R2);
            _isBuilding = true;
            build(ctx);
            _isBuilding = false;

            if (GameFrame.Margins.Width > 0 && IsRoomDrawn((byte)room, display))
            {
                Scheduler.RunUntimed(() => AddMarginPieces(ctx, rooms + ((byte)room * RoomSize), display));
            }
        };

        var draw = Funcs.Patches.sub_0201F2A0;
        Funcs.Patches.sub_0201F2A0 = ctx =>
        {
            if (_isBuilding)
            {
                ExtendBand(ctx.R0);
            }

            draw(ctx);
        };
    }

    private static bool IsRoomDrawn(byte room, uint display)
    {
        bool isLit = Memory.Peek<byte>(display + DisplayLuminous) != 0 || Memory.Peek<byte>(display + DisplayNaturalLighting) != 0 || Memory.Peek<byte>(display + DisplayField30) != 0;
        return Memory.Peek<byte>(ov29_0235359C) != 0 && Memory.Peek<byte>(display + DisplayDropeye) == 0 && !isLit && room != NoRoom;
    }

    private static void ExtendBand(uint element)
    {
        if (Memory.Peek<short>(element + TextureSize) != BandTextureSize || Memory.Peek<short>(element + TextureSize + 2) != BandTextureSize)
        {
            return;
        }

        int width = GameFrame.Margins.Width;
        for (uint vertex = 0; vertex < 4; vertex++)
        {
            ref short x = ref Memory.Poke<short>(element + Vec + (vertex * 4));
            x = x switch
            {
                0 => (short)-width,
                ScreenWidth => (short)(ScreenWidth + width),
                _ => x,
            };
        }
    }

    private static void AddMarginPieces(RecompContext ctx, uint room, uint display)
    {
        int width = GameFrame.Margins.Width;
        short cameraX = Memory.Peek<short>(display + DisplayCameraPixelPos);
        short cameraY = Memory.Peek<short>(display + DisplayCameraPixelPos + 2);
        int left = Memory.Peek<short>(room + RoomBounds) - cameraX;
        int top = Memory.Peek<short>(room + RoomBounds + 4) - cameraY;
        int right = Memory.Peek<short>(room + RoomBounds + 8) - cameraX;
        int bottom = Memory.Peek<short>(room + RoomBounds + 12) - cameraY;

        int litTop = Math.Max(top, 0);
        int litHeight = Math.Min(bottom - litTop, ScreenHeight);
        if (left <= 0 && left > -width)
        {
            Enqueue(ctx, -width, litTop, left, litTop + litHeight, BandTextureSize, 0, 0);
        }

        if (right >= ScreenWidth && right < ScreenWidth + width)
        {
            Enqueue(ctx, right, litTop, ScreenWidth + width, litTop + litHeight, BandTextureSize, 0, 0);
        }

        (int X, int Y, short TextureX, short TextureY)[] corners =
        [
            (left, top, 0, 0),
            (right - CornerSize, top, CornerTextureSize, 0),
            (left, bottom - CornerSize, 0, CornerTextureSize),
            (right - CornerSize, bottom - CornerSize, CornerTextureSize, CornerTextureSize),
        ];

        foreach (var (x, y, textureX, textureY) in corners)
        {
            bool isInMargin = (x <= CornerReachLeft && x > CornerReachLeft - width) || (x >= CornerReachRight && x < CornerReachRight + width);
            if (isInMargin && y > CornerReachLeft && y < CornerReachBottom)
            {
                Enqueue(ctx, x, y, x + CornerSize, y + CornerSize, CornerTextureSize, textureX, textureY);
            }
        }
    }

    private static void Enqueue(RecompContext ctx, int x0, int y0, int x1, int y1, short textureSize, short textureX, short textureY)
    {
        ctx.R13 -= ElementSize;
        uint element = ctx.R13;
        ctx.R0 = element;
        Funcs.InitRender3dElement64(ctx);

        Memory.Poke<ushort>(element + TextureParams) = Memory.Peek<ushort>(ov29_0237C99C);
        Memory.Poke<int>(element + TextureVRAMOffset) = 0x400;
        Memory.Poke<uint>(element + PaletteBaseAddr) = Memory.Peek<uint>(Memory.Peek<uint>(_020AFC70) + 0xE0) + 0x3A00;
        Memory.Poke<byte>(element + Alpha) = Memory.Peek<byte>(ov29_0235359C + 4);
        Memory.Poke<short>(element + TextureTopLeft) = textureX;
        Memory.Poke<short>(element + TextureTopLeft + 2) = textureY;
        Memory.Poke<short>(element + TextureSize) = textureSize;
        Memory.Poke<short>(element + TextureSize + 2) = textureSize;
        Memory.Poke<ushort>(element + ZIndex) = 0x30;
        Memory.Poke<byte>(element + Type) = Render64Tiling;
        Memory.Poke<byte>(element + PolygonID) = 0x22;

        short[] vertices = [(short)x0, (short)y0, (short)x1, (short)y0, (short)x0, (short)y1, (short)x1, (short)y1];
        for (int i = 0; i < vertices.Length; i++)
        {
            Memory.Poke<short>(element + Vec + (uint)(i * 2)) = vertices[i];
        }

        ctx.R0 = element;
        Funcs.sub_0201F2A0(ctx);
        ctx.R13 += ElementSize;
    }
}
