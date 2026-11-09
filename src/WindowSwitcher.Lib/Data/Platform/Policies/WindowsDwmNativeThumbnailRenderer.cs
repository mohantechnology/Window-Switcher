using System.Runtime.Versioning;
using WindowSwitcher.Lib.Data.Platform.Interop;
using WindowSwitcher.Lib.Data.Platform.SystemInfo.Abstractions;
using WindowSwitcher.Lib.Models;

namespace WindowSwitcher.Lib.Data.Platform.Policies;

/// <summary>
/// Windows DWM implementation for native window thumbnail rendering.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDwmNativeThumbnailRenderer : INativeThumbnailRenderer
{
    /// <inheritdoc />
    public bool TryRegister(
        nint destinationWindowHandle,
        nint sourceWindowHandle,
        NativeThumbnailBounds destinationBounds,
        byte opacity,
        out nint thumbnailHandle
    )
    {
        thumbnailHandle = 0;
        if (destinationWindowHandle == 0 || sourceWindowHandle == 0)
            return false;

        int result = DwmFunctions.DwmRegisterThumbnail(
            destinationWindowHandle,
            sourceWindowHandle,
            out nint registeredThumbnailHandle
        );
        if (result != 0)
            return false;

        int flags =
            DwmFunctions.DWM_TNP_SOURCECLIENTAREAONLY
            | DwmFunctions.DWM_TNP_VISIBLE
            | DwmFunctions.DWM_TNP_OPACITY
            | DwmFunctions.DWM_TNP_RECTDESTINATION;
        var source = new DwmFunctions.Rect(0, 0, 0, 0);
        if (
            DwmFunctions.DwmQueryThumbnailSourceSize(
                registeredThumbnailHandle,
                out DwmFunctions.PSIZE sourceSize
            ) == 0
            && sourceSize.x > 0
            && sourceSize.y > 0
        )
        {
            // Sample the full source frame so the thumbnail is not taken from a small region.
            flags |= DwmFunctions.DWM_TNP_RECTSOURCE;
            source = new DwmFunctions.Rect(0, 0, sourceSize.x, sourceSize.y);
        }

        var properties = new DwmFunctions.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = flags,
            fSourceClientAreaOnly = false,
            fVisible = true,
            opacity = opacity,
            rcDestination = new DwmFunctions.Rect
            {
                Left = destinationBounds.Left,
                Top = destinationBounds.Top,
                Right = destinationBounds.Right,
                Bottom = destinationBounds.Bottom,
            },
            rcSource = source,
        };

        DwmFunctions.DwmUpdateThumbnailProperties(registeredThumbnailHandle, ref properties);
        thumbnailHandle = registeredThumbnailHandle;
        return true;
    }

    /// <inheritdoc />
    public bool TryUpdateOpacity(nint thumbnailHandle, byte opacity)
    {
        if (thumbnailHandle == 0)
            return false;

        var properties = new DwmFunctions.DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DwmFunctions.DWM_TNP_OPACITY,
            opacity = opacity,
        };

        return DwmFunctions.DwmUpdateThumbnailProperties(thumbnailHandle, ref properties) == 0;
    }

    /// <inheritdoc />
    public void Unregister(nint thumbnailHandle)
    {
        if (thumbnailHandle == 0)
            return;

        DwmFunctions.DwmUnregisterThumbnail(thumbnailHandle);
    }
}
