using System.Text.Json;
using System.Text.Json.Serialization;
using LibRecomp;
using NTRModern;

namespace PMDSpace;

internal enum WindowMode
{
    Windowed,
    Borderless,
    Fullscreen,
}

internal enum AspectRatio
{
    Original,
    Expand,
}

internal sealed class Settings
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public WindowMode WindowMode { get; set; }

    public AspectRatio AspectRatio { get; set; } = AspectRatio.Expand;

    public int Volume { get; set; } = 100;

    public int StickDeadzone { get; set; } = 50;

    public bool BackgroundInput { get; set; } = true;

    public Model Model { get; set; } = Model.DSLite;

    public UserSettings User { get; set; } = new("", "", Language.English, FavoriteColor: 0, BirthdayMonth: 1, BirthdayDay: 1);

    public Dictionary<Buttons, string[]> Keyboard { get; set; } = new()
    {
        [Buttons.A] = ["X"],
        [Buttons.B] = ["Z"],
        [Buttons.X] = ["S"],
        [Buttons.Y] = ["A"],
        [Buttons.Select] = ["Backspace"],
        [Buttons.Start] = ["Return"],
        [Buttons.Right] = ["Right"],
        [Buttons.Left] = ["Left"],
        [Buttons.Up] = ["Up"],
        [Buttons.Down] = ["Down"],
        [Buttons.R] = ["W"],
        [Buttons.L] = ["Q"],
    };

    public Dictionary<Buttons, string[]> Controller { get; set; } = new()
    {
        [Buttons.A] = ["b"],
        [Buttons.B] = ["a"],
        [Buttons.X] = ["y"],
        [Buttons.Y] = ["x"],
        [Buttons.Select] = ["back"],
        [Buttons.Start] = ["start"],
        [Buttons.Right] = ["dpright"],
        [Buttons.Left] = ["dpleft"],
        [Buttons.Up] = ["dpup"],
        [Buttons.Down] = ["dpdown"],
        [Buttons.R] = ["rightshoulder"],
        [Buttons.L] = ["leftshoulder"],
    };

    private static string FilePath => Path.Combine(GamePaths.UserData, "settings.json");

    public static Settings Load()
    {
        if (BackedUpFile.TryRead(FilePath, Parse, out var settings))
        {
            return settings;
        }

        settings = new Settings();
        settings.Save();
        return settings;
    }

    public void Save()
    {
        BackedUpFile.Write(FilePath, JsonSerializer.SerializeToUtf8Bytes(this, Options));
    }

    private static Settings Parse(byte[] json)
    {
        var settings = JsonSerializer.Deserialize<Settings>(json, Options);
        if (settings is null || HasNullBinding(settings.Keyboard) || HasNullBinding(settings.Controller))
        {
            throw new InvalidDataException("The settings have a null binding.");
        }

        return settings;
    }

    private static bool HasNullBinding<T>(Dictionary<T, string[]> bindings)
        where T : notnull
    {
        return bindings.Values.Any(names => names is null || names.Contains(null));
    }
}
