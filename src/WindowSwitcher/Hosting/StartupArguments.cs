using System;
using System.Linq;

namespace WindowSwitcher.Hosting;

/// <summary>
/// Process launch arguments parsed once at startup.
/// </summary>
internal static class StartupArguments
{
    public const string BackgroundArgument = "--background";

    /// <summary>
    /// Gets whether the process was launched to stay hidden in the tray.
    /// </summary>
    public static bool StartInBackground { get; private set; }

    /// <summary>
    /// Parses and stores startup arguments for the current process.
    /// </summary>
    public static void Initialize(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        StartInBackground = args.Any(argument =>
            string.Equals(argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase)
        );
    }
}
