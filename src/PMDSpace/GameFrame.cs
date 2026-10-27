using LibRecomp;
using NDSRenderer;
using NTRModern.ARM9;
using RecompiledFuncs.ARM9;

namespace PMDSpace;

/// <summary>
/// <c>GameFrame</c> is the frame the game is drawing right now. Usually you may need it to add your
/// own tiles to the widescreen margins.
/// </summary>
/// <example>
/// <code>
/// GameFrame.Finished += ctx =>
/// {
///     // Carry engine A's BG3 on past both edges of the screen
///     for (int i = 0; i &lt; GameFrame.Margins.ColumnsShown; i++)
///     {
///         CopyMapColumn(GameFrame.Margins.Supply(0, 3, -i), firstColumn - i);
///         CopyMapColumn(GameFrame.Margins.Supply(0, 3, 32 + i), firstColumn + 32 + i);
///     }
/// };
/// </code>
/// </example>
public static class GameFrame
{
    internal const int ScreenColumns = FrameRenderer.Width / 8;

    /// <summary>
    /// Runs once a frame, right after the game updates the screens. This is where you supply your own columns.
    /// Whatever you do in here doesn't change the game's timing.
    /// </summary>
    public static event Action<RecompContext>? Finished;

    /// <summary>The margins of the frame the game is drawing. Supply your columns from <see cref="Finished"/>.</summary>
    public static FrameMargins Margins { get; } = new();

    /// <summary>
    /// Which screens the frame shows. It goes back to <see cref="ScreenSizing.Auto"/> at the start of every frame, so if
    /// a scene needs something else, set it from <see cref="Finished"/> on every frame of that scene.
    /// </summary>
    public static ScreenSizing Sizing { get; set; } = ScreenSizing.Auto;

    /// <summary>
    /// How many lines down <see cref="ScreenSizing.Scrolled"/> and <see cref="ScreenSizing.Cropped"/> start, from 0 to
    /// 192. It doesn't have to be a whole number. Just like <see cref="Sizing"/>, it goes back to 0 at the start of
    /// every frame.
    /// </summary>
    public static float Scroll { get; set; }

    /// <summary>
    /// Parts of the screens to draw again somewhere else, at their own size. Add them from <see cref="Finished"/>, the
    /// same way as the margins.
    /// </summary>
    public static FrameInsets Insets { get; } = new();

    internal static void Install()
    {
        var applyState = Funcs.Patches.GraphicsEngine_ApplyState;
        Funcs.Patches.GraphicsEngine_ApplyState = ctx =>
        {
            applyState(ctx);

            Margins.Clear();
            Insets.Clear();
            Sizing = ScreenSizing.Auto;
            Scroll = 0;
            if (Finished is { } finished)
            {
                Scheduler.RunUntimed(() => finished(ctx));
            }
        };
    }
}
