using NDSRenderer;
using NTRModern;
using SDL;
using static SDL.SDL3;

namespace PMDSpace;

internal static unsafe class Input
{
    private const int StickRange = 32767;

    private static readonly Dictionary<SDL_Scancode, Buttons> Keys = [];
    private static readonly Dictionary<SDL_GamepadButton, Buttons> GamepadButtons = [];
    private static readonly HashSet<SDL_Scancode> HeldKeys = [];
    private static readonly HashSet<SDL_GamepadButton> HeldGamepadButtons = [];

    private static int _stickThreshold;
    private static bool _isTouching;

    private static Buttons _keyboard;
    private static Buttons _gamepad;
    private static Buttons _stick;

    public static void Configure(Settings settings, Action<string> showError)
    {
        Keys.Clear();
        GamepadButtons.Clear();
        var unknown = new List<string>();
        Bind(settings.Keyboard, name => SDL_GetScancodeFromName(name), SDL_Scancode.SDL_SCANCODE_UNKNOWN, "a key", unknown,
            (key, button) => Keys[key] = Keys.GetValueOrDefault(key) | button);
        Bind(settings.Controller, name => SDL_GetGamepadButtonFromString(name), SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, "a controller button", unknown,
            (pad, button) => GamepadButtons[pad] = GamepadButtons.GetValueOrDefault(pad) | button);

        _stickThreshold = Math.Clamp(settings.StickDeadzone, 1, 100) * StickRange / 100;
        SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, settings.BackgroundInput ? "1" : "0");
        if (unknown.Count > 0)
        {
            showError(string.Join(Environment.NewLine, unknown));
        }
    }

    public static void HandleEvent(in SDL_Event e, SDL_Window* window, FrameRenderer renderer)
    {
        switch (e.Type)
        {
            case SDL_EventType.SDL_EVENT_KEY_DOWN or SDL_EventType.SDL_EVENT_KEY_UP:
                _keyboard = Held(HeldKeys, Keys, e.key.scancode, e.key.down);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN or SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                _gamepad = Held(HeldGamepadButtons, GamepadButtons, (SDL_GamepadButton)e.gbutton.button, e.gbutton.down);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                HandleStick((SDL_GamepadAxis)e.gaxis.axis, e.gaxis.value);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                SDL_OpenGamepad(e.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                SDL_CloseGamepad(SDL_GetGamepadFromID(e.gdevice.which));
                HeldGamepadButtons.Clear();
                _gamepad = Buttons.None;
                _stick = Buttons.None;
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN when e.button.button == SDL_BUTTON_LEFT:
                _isTouching = true;
                Touch(e.button.x, e.button.y, window, renderer);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_MOTION when _isTouching:
                Touch(e.motion.x, e.motion.y, window, renderer);
                break;

            case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP when e.button.button == SDL_BUTTON_LEFT:
                _isTouching = false;
                TouchScreen.Release();
                break;
        }

        Keypad.Pressed = _keyboard | _gamepad | _stick;
    }

    private static void Touch(float windowX, float windowY, SDL_Window* window, FrameRenderer renderer)
    {
        int width, height;
        SDL_GetWindowSize(window, &width, &height);
        if (renderer.TryFindPixel(windowX, windowY, width, height, out int x, out int y) && y >= Video.VisibleLines)
        {
            TouchScreen.Touch(x, y - Video.VisibleLines);
        }
        else
        {
            TouchScreen.Release();
        }
    }

    private static void Bind<TAction, TInput>(Dictionary<TAction, string[]> names, Func<string, TInput> find, TInput invalid, string kind, List<string> unknown, Action<TInput, TAction> add)
        where TAction : notnull
        where TInput : struct, Enum
    {
        foreach (var (action, namesForAction) in names)
        {
            foreach (string name in namesForAction)
            {
                var input = find(name);
                if (input.Equals(invalid))
                {
                    unknown.Add($"\"{name}\" isn't {kind}.");
                    continue;
                }

                add(input, action);
            }
        }
    }

    private static Buttons Held<T>(HashSet<T> held, Dictionary<T, Buttons> bindings, T input, bool isDown)
        where T : notnull
    {
        if (isDown)
        {
            held.Add(input);
        }
        else
        {
            held.Remove(input);
        }

        return held.Aggregate(Buttons.None, (buttons, each) => buttons | bindings.GetValueOrDefault(each));
    }

    private static void HandleStick(SDL_GamepadAxis axis, int value)
    {
        var (negative, positive) = axis switch
        {
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => (Buttons.Left, Buttons.Right),
            SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => (Buttons.Up, Buttons.Down),
            _ => (Buttons.None, Buttons.None),
        };

        _stick &= ~(negative | positive);
        if (value <= -_stickThreshold)
        {
            _stick |= negative;
        }
        else if (value >= _stickThreshold)
        {
            _stick |= positive;
        }
    }
}
