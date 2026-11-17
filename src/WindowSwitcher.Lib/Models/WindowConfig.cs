using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowSwitcher.Lib.Models;

public class WindowConfig : INotifyPropertyChanged
{
    public string WindowId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;

    public string WindowTitle
    {
        get;
        set
        {
            // A null title from a hand-edited config.json must not prevent loading it.
            string title = value ?? string.Empty;
            if (field == title)
                return;
            field = title;
            ShortWindowTitle = field.Length > 40 ? $"{field[..40]}..." : field;
            OnPropertyChanged();
        }
    } = string.Empty;

    public string ShortWindowTitle
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = string.Empty;

    public int WindowWidth
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = FloatingWindowInitialSize.FallbackWidth;

    public int WindowHeight
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = FloatingWindowInitialSize.FallbackHeight;

    public int WindowLeft
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = 100;

    public int WindowTop
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = 100;

    public double PreviewOpacity
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    } = 1.0;

    /// <summary>
    /// Per-window live preview choice. <see langword="null"/> follows the global setting.
    /// Changing the global Enable previews setting clears this override.
    /// </summary>
    public bool? LivePreviewOverride
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public WindowConfig Clone()
    {
        return (WindowConfig)this.MemberwiseClone();
    }
}
