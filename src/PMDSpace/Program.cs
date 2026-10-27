using LibRecomp;
using NDSRenderer;
using NTRModern;
using PMDSpace;
using PMDSpace.Patches;

const string Title = "Pokémon Mystery Dungeon: Explorers of Space";
const string GameId = "pmd_space";

Game.RegisterConfigPath(GamePaths.UserData);
Game.Register(new GameEntry(
    GameId: GameId,
    InternalName: "POKEDUN SORA",
    ROMHash: "5FA96CA8D8DD6405D6CD2BAD73ED68BC73A9D152",
    SaveType: SaveType.EEPROM128K));
LibRecomp.ARM9.Recomp.RegisterSections(RecompiledFuncs.ARM9.Funcs.Sections, RecompiledFuncs.ARM9.Funcs.OverlaySections);
LibRecomp.ARM7.Recomp.RegisterSections(RecompiledFuncs.ARM7.Funcs.Sections, RecompiledFuncs.ARM7.Funcs.OverlaySections);
GamePatches.Install();

var settings = Settings.Load();
using var window = new GameWindow(Title, settings);
Input.Configure(settings, window.ShowError);
using var audio = new AudioOutput(settings.Volume);
Sound.SamplesReady += audio.Play;
Video.FrameFinished += frame => window.Renderer.Submit(frame, GameFrame.Sizing, GameFrame.Margins, GameFrame.Scroll, GameFrame.Insets);

if (Game.IsROMValid(GameId))
{
    StartGame();
}
else if (args.Length > 0)
{
    if (SelectROM(args[0]))
    {
        StartGame();
    }
}
else
{
    ChooseROM();
}

window.Run();
Game.Quit();
return 0;

void ChooseROM()
{
    window.ShowOpenFileDialog("DS ROM", "nds", path =>
    {
        if (SelectROM(path))
        {
            StartGame();
        }
        else
        {
            ChooseROM();
        }
    });
}

void StartGame()
{
    if (!Game.Start(GameId, settings.Model, settings.User, window.ShowError))
    {
        window.Close();
    }
}

bool SelectROM(string path)
{
    string problem = Game.SelectROM(path, GameId) switch
    {
        ROMValidationError.Good => "",
        ROMValidationError.FailedToOpen => "The ROM couldn't be opened.",
        ROMValidationError.NotAROM => "That file isn't a DS ROM.",
        ROMValidationError.IncorrectROM => "That's a ROM of another game.",
        ROMValidationError.IncorrectVersion => "That's another version of the game.",
        _ => "The ROM couldn't be stored.",
    };

    if (problem.Length > 0)
    {
        window.ShowError($"{problem} Select a Pokémon Mystery Dungeon: Explorers of Sky (USA) ROM.");
    }

    return problem.Length == 0;
}
