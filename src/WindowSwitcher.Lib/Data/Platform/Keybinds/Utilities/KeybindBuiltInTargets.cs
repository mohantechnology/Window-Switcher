namespace WindowSwitcher.Lib.Data.Platform.Keybinds.Utilities;

/// <summary>
/// Built-in keybind targets that are not tied to one specific window id.
/// </summary>
public static class KeybindBuiltInTargets
{
    /// <summary>
    /// Target id for cycling to the next client.
    /// </summary>
    public const string NextClientTargetId = "action:next-client";

    /// <summary>
    /// Target id for cycling to the previous client.
    /// </summary>
    public const string PreviousClientTargetId = "action:previous-client";

    /// <summary>
    /// Target id for focusing the currently active client.
    /// </summary>
    public const string FocusActiveClientTargetId = "action:focus-active-client";

    /// <summary>
    /// Target id for pinning the currently focused OS window into the preview list.
    /// </summary>
    public const string PinFocusedWindowTargetId = "action:pin-focused-window";

    /// <summary>
    /// Target id for changing floating preview size while hovering with the mouse wheel.
    /// </summary>
    public const string ChangeFloatingSizeOnHoverTargetId =
        "action:change-floating-size-on-hover";

    /// <summary>
    /// Target id for changing floating preview opacity while hovering with the mouse wheel.
    /// </summary>
    public const string ChangeFloatingOpacityOnHoverTargetId =
        "action:change-floating-opacity-on-hover";

    /// <summary>
    /// Display label for next selected client action.
    /// </summary>
    public const string NextClientDisplayName = "Next client";

    /// <summary>
    /// Display label for previous selected client action.
    /// </summary>
    public const string PreviousClientDisplayName = "Previous client";

    /// <summary>
    /// Display label for focus active client action.
    /// </summary>
    public const string FocusActiveClientDisplayName = "Focus active client";

    /// <summary>
    /// Display label for pin focused window action.
    /// </summary>
    public const string PinFocusedWindowDisplayName = "Pin focused window";

    /// <summary>
    /// Display label for change floating size on hover action.
    /// </summary>
    public const string ChangeFloatingSizeOnHoverDisplayName = "Change size on hover";

    /// <summary>
    /// Display label for change floating opacity on hover action.
    /// </summary>
    public const string ChangeFloatingOpacityOnHoverDisplayName = "Change opacity on hover";

    /// <summary>
    /// Returns whether a target id is one of the built-in action targets.
    /// </summary>
    public static bool IsBuiltInTarget(string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        string normalized = targetId.Trim().ToLowerInvariant();
        return normalized == NextClientTargetId
            || normalized == PreviousClientTargetId
            || normalized == FocusActiveClientTargetId
            || normalized == PinFocusedWindowTargetId
            || normalized == ChangeFloatingSizeOnHoverTargetId
            || normalized == ChangeFloatingOpacityOnHoverTargetId;
    }

    /// <summary>
    /// Returns whether a built-in target is handled locally on a hovered floating preview
    /// instead of through the global keyboard activator.
    /// </summary>
    public static bool IsHoverLocalTarget(string? targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return false;

        string normalized = targetId.Trim().ToLowerInvariant();
        return normalized == ChangeFloatingSizeOnHoverTargetId
            || normalized == ChangeFloatingOpacityOnHoverTargetId;
    }
}
