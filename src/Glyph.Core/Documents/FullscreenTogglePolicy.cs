namespace Glyph.Core.Documents;

/// <summary>
/// Full-screen presenter toggle labels and next mode (F02-09).
/// </summary>
public static class FullscreenTogglePolicy
{
    public static bool ShouldExitFullscreen(bool currentlyFullscreen) => currentlyFullscreen;

    public static string StatusAfterToggle(bool currentlyFullscreen) =>
        currentlyFullscreen
            ? "Exited fullscreen."
            : "Fullscreen — press Fullscreen again or Esc via window chrome to exit.";
}
