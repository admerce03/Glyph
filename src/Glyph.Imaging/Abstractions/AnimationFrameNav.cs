namespace Glyph.Imaging.Abstractions;

/// <summary>
/// Frame stepping and playback wrap logic for animated images (F27).
/// </summary>
public static class AnimationFrameNav
{
    /// <summary>
    /// Wraps <paramref name="currentIndex"/> + <paramref name="delta"/> into [0, frameCount).
    /// Returns -1 when there are no frames.
    /// </summary>
    public static int WrapStep(int currentIndex, int delta, int frameCount)
    {
        if (frameCount <= 0)
        {
            return -1;
        }

        return ((currentIndex + delta) % frameCount + frameCount) % frameCount;
    }

    public static string FormatLabel(int zeroBasedIndex, int frameCount) =>
        $"Frame {zeroBasedIndex + 1}/{frameCount}";

    /// <summary>
    /// Computes the next frame during timed playback. Returns <c>null</c> when playback should stop.
    /// </summary>
    /// <param name="loopsCompleted">In/out: incremented when a finite loop restarts.</param>
    public static int? NextPlaybackFrame(
        int currentIndex,
        int frameCount,
        bool loopEnabled,
        int animationIterations,
        ref int loopsCompleted)
    {
        if (frameCount <= 1)
        {
            return null;
        }

        var next = currentIndex + 1;
        if (next < frameCount)
        {
            return next;
        }

        if (!loopEnabled)
        {
            return null;
        }

        if (animationIterations > 0)
        {
            loopsCompleted++;
            if (loopsCompleted >= animationIterations)
            {
                return null;
            }
        }

        return 0;
    }

    public const string Paused = "Animation paused.";
    public const string Restarted = "Animation restarted.";
    public const string SaveCancelled = "Save frame cancelled.";

    public static string Finished(int frameCount) =>
        $"Animation finished · frame {frameCount}/{frameCount}.";

    public static string SavedFrame(int oneBasedFrame, string fileName) =>
        $"Saved frame {oneBasedFrame} → {fileName}";

    public static string SuggestedFileName(string baseName, int oneBasedFrame) =>
        $"{baseName}-frame{oneBasedFrame}.png";

    public static string PlayButtonLabel(bool playing) => playing ? "Pause" : "Play";
}
