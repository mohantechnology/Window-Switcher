namespace WindowSwitcher.Lib.Data.Platform.WindowAccess.PreviewFrames.Abstractions;

/// <summary>
/// Process-wide preview capture pacing. Lower values use less CPU/GPU and memory bandwidth.
/// </summary>
internal static class PreviewFrameTiming
{
    internal const int MinFramesPerSecond = 0;
    internal const int MaxFramesPerSecond = 30;
    internal const int DefaultFramesPerSecond = 1;

    private static int _framesPerSecond = DefaultFramesPerSecond;

    /// <summary>
    /// Current preview capture rate. <c>0</c> means snapshot-only (first frame, no refresh).
    /// </summary>
    internal static int FramesPerSecond => Volatile.Read(ref _framesPerSecond);

    /// <summary>
    /// Updates the process-wide preview capture rate.
    /// </summary>
    internal static void SetFramesPerSecond(int framesPerSecond)
    {
        Volatile.Write(ref _framesPerSecond, Clamp(framesPerSecond));
    }

    /// <summary>
    /// Clamps a frame-rate value to the supported range.
    /// </summary>
    internal static int Clamp(int framesPerSecond) =>
        Math.Clamp(framesPerSecond, MinFramesPerSecond, MaxFramesPerSecond);

    /// <summary>
    /// Returns the delay between captures, or <see langword="null"/> for snapshot-only mode.
    /// </summary>
    internal static TimeSpan? TryGetFrameInterval()
    {
        int framesPerSecond = FramesPerSecond;
        if (framesPerSecond <= 0)
            return null;

        return TimeSpan.FromMilliseconds(1000d / framesPerSecond);
    }

    /// <summary>
    /// PipeWire rejects a zero maximum framerate; snapshot mode still needs a positive native cap.
    /// </summary>
    internal static uint NativeMaximumFramerate => (uint)Math.Max(1, FramesPerSecond);
}
