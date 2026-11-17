using Tmds.DBus;
using WindowSwitcher.Lib.Data.Platform.WindowAccess.Accessors.KWin;

namespace WindowSwitcher.Lib.Data.Platform.WindowAccess.Accessors.KWin.Abstractions;

/// <summary>
/// Defines the D-Bus object that KWin scripts call back to report windows and activations.
/// </summary>
[DBusInterface(KWinScripts.BridgeInterfaceName)]
public interface IKWinWindowBridge : IDBusObject
{
    /// <summary>Receives the complete list of KWin windows.</summary>
    /// <param name="snapshotJson">JSON array describing every window known to KWin.</param>
    Task PushSnapshotAsync(string snapshotJson);

    /// <summary>Receives the outcome of an activation script.</summary>
    /// <param name="requestId">Identifier of the activation request.</param>
    /// <param name="activated"><see langword="true" /> when the window was found and activated.</param>
    Task CompleteActivationAsync(string requestId, bool activated);

    /// <summary>Receives the outcome of a foreground-window query script.</summary>
    /// <param name="requestId">Identifier of the foreground query request.</param>
    /// <param name="windowId">
    /// KWin <c>internalId</c> of the focused native window, or an empty string when none applies.
    /// </param>
    Task CompleteForegroundQueryAsync(string requestId, string windowId);
}
