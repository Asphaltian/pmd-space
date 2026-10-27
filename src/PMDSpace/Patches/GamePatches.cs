using ARM7 = RecompiledFuncs.ARM7;
using ARM9 = RecompiledFuncs.ARM9;

namespace PMDSpace.Patches;

internal static class GamePatches
{
    public static void Install()
    {
        ARM9.Funcs.Patches.OS_SaveContext = ctx => LibRecomp.ARM9.Recomp.SaveContext(ctx, ARM9.Funcs.Original.OS_SaveContext);
        ARM9.Funcs.Patches.Cardi_ReadRom = ctx => LibRecomp.ARM9.Recomp.ReadRom(ctx, ARM9.Funcs.Original.Cardi_ReadRom);
        ARM7.Funcs.Patches.OS_SaveContext = ctx => LibRecomp.ARM7.Recomp.SaveContext(ctx, ARM7.Funcs.Original.OS_SaveContext);

        DungeonTasks.Install();
        GameFrame.Install();
        TopScreenWindows.Install();
        BootScreen.Install();
        FullScreenPictures.Install();
        GroundMargins.Install();
        CompanyScreens.Install();
        FullScreenRectangles.Install();
        StackedScenes.Install();
        OffScreenActors.Install();
        TitleScreens.Install();
        PersonalityTestBow.Install();
        DungeonMargins.Install();
        DungeonDarkness.Install();
        MarginSprites.Install();
    }
}
