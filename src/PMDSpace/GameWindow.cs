using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NDSRenderer;
using NTRModern;
using SDL;
using static SDL.SDL3;

namespace PMDSpace;

internal sealed unsafe class GameWindow : IDisposable
{
    private const int InitialScale = 3;

    private readonly SDL_Window* _window;
    private readonly Settings _settings;
    private readonly string _title;
    private readonly int _mainThread = Environment.CurrentManagedThreadId;
    private readonly ConcurrentQueue<(string Message, ManualResetEventSlim Shown)> _errors = new();
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private bool _isOpen = true;

    public GameWindow(string title, Settings settings)
    {
        _settings = settings;
        _title = title;

        if (!SDL_Init(SDL_InitFlags.SDL_INIT_VIDEO | SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            throw new InvalidOperationException($"Could not initialize SDL: {SDL_GetError()}");
        }

        _window = SDL_CreateWindow(
            title,
            FrameRenderer.Width * InitialScale,
            Video.VisibleLines * InitialScale,
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_VULKAN);

        if (_window is null)
        {
            throw new InvalidOperationException($"Could not open a Vulkan window: {SDL_GetError()}");
        }

        Renderer = new FrameRenderer(InstanceExtensions(), CreateSurface);
        ApplyWindowMode();
    }

    public FrameRenderer Renderer { get; }

    public void ShowError(string message)
    {
        Console.Error.WriteLine(message);
        if (Environment.CurrentManagedThreadId == _mainThread)
        {
            SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, _title, message, _window);
            return;
        }

        using var shown = new ManualResetEventSlim();
        _errors.Enqueue((message, shown));
        shown.Wait();
    }

    public void ShowOpenFileDialog(string filterName, string pattern, Action<string> chosen)
    {
        var filter = (SDL_DialogFileFilter*)NativeMemory.Alloc((nuint)sizeof(SDL_DialogFileFilter));
        filter->name = (byte*)Marshal.StringToCoTaskMemUTF8(filterName);
        filter->pattern = (byte*)Marshal.StringToCoTaskMemUTF8(pattern);
        var request = GCHandle.Alloc(new FileDialogRequest(this, chosen, (nint)filter));
        SDL_ShowOpenFileDialog(&OnFileChosen, GCHandle.ToIntPtr(request), _window, filter, 1, (byte*)null, false);
    }

    public void Close() => _isOpen = false;

    public void Run()
    {
        while (_isOpen)
        {
            SDL_Event e;
            while (SDL_PollEvent(&e))
            {
                switch (e.Type)
                {
                    case SDL_EventType.SDL_EVENT_QUIT:
                        return;
                    case SDL_EventType.SDL_EVENT_KEY_DOWN when e.key.scancode == SDL_Scancode.SDL_SCANCODE_F12:
                        SaveScreenshot();
                        break;
                    case SDL_EventType.SDL_EVENT_KEY_DOWN when IsFullscreenToggle(e.key) && !e.key.repeat:
                        _settings.WindowMode = (WindowMode)(((int)_settings.WindowMode + 1) % Enum.GetValues<WindowMode>().Length);
                        _settings.Save();
                        ApplyWindowMode();
                        break;
                    default:
                        Input.HandleEvent(e, _window, Renderer);
                        break;
                }
            }

            while (_errors.TryDequeue(out var error))
            {
                SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, _title, error.Message, _window);
                error.Shown.Set();
            }

            while (_mainThreadActions.TryDequeue(out var action))
            {
                action();
            }

            int width, height;
            SDL_GetWindowSizeInPixels(_window, &width, &height);
            if (width > 0 && height > 0)
            {
                int widthAtScreenHeight = Video.VisibleLines * width / height;
                GameFrame.Margins.Width = _settings.AspectRatio == AspectRatio.Expand ? (widthAtScreenHeight - FrameRenderer.Width) / 2 : 0;
            }

            Renderer.Present(width, height);
        }
    }

    public void Dispose()
    {
        Renderer.Dispose();
        SDL_DestroyWindow(_window);
        SDL_Quit();
    }

    private static string[] InstanceExtensions()
    {
        uint count;
        byte** names = SDL_Vulkan_GetInstanceExtensions(&count);
        if (names is null)
        {
            throw new InvalidOperationException($"Could not load Vulkan: {SDL_GetError()}");
        }

        return [.. Enumerable.Range(0, (int)count).Select(i => Marshal.PtrToStringUTF8((nint)names[i])!)];
    }

    private ulong CreateSurface(nint instance)
    {
        VkSurfaceKHR_T* surface;
        if (!SDL_Vulkan_CreateSurface(_window, (VkInstance_T*)instance, null, &surface))
        {
            throw new InvalidOperationException($"Could not make a Vulkan surface for the window: {SDL_GetError()}");
        }

        return (ulong)surface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnFileChosen(nint userdata, byte** files, int filter)
    {
        var handle = GCHandle.FromIntPtr(userdata);
        var request = (FileDialogRequest)handle.Target!;
        handle.Free();
        var filters = (SDL_DialogFileFilter*)request.Filter;
        Marshal.FreeCoTaskMem((nint)filters->name);
        Marshal.FreeCoTaskMem((nint)filters->pattern);
        NativeMemory.Free(filters);
        if (files is not null && files[0] is not null)
        {
            string path = Marshal.PtrToStringUTF8((nint)files[0])!;
            request.Window._mainThreadActions.Enqueue(() => request.Chosen(path));
        }
    }

    private static bool IsFullscreenToggle(SDL_KeyboardEvent key)
    {
        return key.scancode == SDL_Scancode.SDL_SCANCODE_F11
            || (key.scancode == SDL_Scancode.SDL_SCANCODE_RETURN && (key.mod & SDL_Keymod.SDL_KMOD_ALT) != 0);
    }

    private void SaveScreenshot()
    {
        if (Renderer.TakeScreenshot() is not { } screenshot)
        {
            return;
        }

        string folder = Directory.CreateDirectory(Path.Combine(GamePaths.UserData, "screenshots")).FullName;
        string path = Path.Combine(folder, $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        fixed (byte* pixels = screenshot.Pixels)
        {
            var surface = SDL_CreateSurfaceFrom(screenshot.Width, screenshot.Height, SDL_PixelFormat.SDL_PIXELFORMAT_ABGR8888, (nint)pixels, screenshot.Width * 4);
            bool saved = SDL_SavePNG(surface, path);
            SDL_DestroySurface(surface);
            if (!saved)
            {
                ShowError($"Could not save {path}: {SDL_GetError()}");
            }
        }
    }

    private void ApplyWindowMode()
    {
        var exclusiveMode = _settings.WindowMode == WindowMode.Fullscreen
            ? SDL_GetDesktopDisplayMode(SDL_GetDisplayForWindow(_window))
            : null;

        SDL_SetWindowFullscreenMode(_window, exclusiveMode);
        SDL_SetWindowFullscreen(_window, _settings.WindowMode != WindowMode.Windowed);
    }

    private sealed record FileDialogRequest(GameWindow Window, Action<string> Chosen, nint Filter);
}
