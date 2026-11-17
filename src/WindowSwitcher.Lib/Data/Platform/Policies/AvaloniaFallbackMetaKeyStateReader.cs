using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// No-op Meta reader for platforms where Avalonia already reports Super/Meta reliably.
/// </summary>
public sealed class AvaloniaFallbackMetaKeyStateReader : IMetaKeyStateReader
{
    /// <inheritdoc />
    public bool IsMetaPressed() => false;
}