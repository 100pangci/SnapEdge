using SnapEdge.Core;

namespace SnapEdge.App.Services;

public sealed class AppSettings
{
    public string SaveDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapEdge");
    public bool CropClientArea { get; set; } = true;
    public bool AutoCropBlackBorders { get; set; } = true;
    public byte BlackBrightnessThreshold { get; set; } = 18;
    public byte BlackColorTolerance { get; set; } = 20;
    public bool CopyToClipboard { get; set; }
    public bool ShowCaptureFeedback { get; set; } = true;
    public double ButtonOpacity { get; set; } = 0.9;
    public double ButtonSize { get; set; } = 62;
    public bool AutoSnap { get; set; } = true;
    public bool AutoPeek { get; set; } = true;
    public bool LaunchAtStartup { get; set; }
    public string? MonitorDeviceName { get; set; }
    public string SnapSide { get; set; } = "Right";
    public double HorizontalPositionFraction { get; set; } = 0.98;
    public double VerticalPositionFraction { get; set; } = 0.5;

    public BlackBorderOptions ToBlackBorderOptions() => new()
    {
        BrightnessThreshold = BlackBrightnessThreshold,
        ColorTolerance = BlackColorTolerance,
    };

    public void Normalize()
    {
        SaveDirectory = string.IsNullOrWhiteSpace(SaveDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapEdge")
            : Environment.ExpandEnvironmentVariables(SaveDirectory.Trim());
        ButtonOpacity = Math.Clamp(ButtonOpacity, 0.45, 1.0);
        ButtonSize = Math.Clamp(ButtonSize, 48, 96);
        HorizontalPositionFraction = Math.Clamp(HorizontalPositionFraction, 0, 1);
        VerticalPositionFraction = Math.Clamp(VerticalPositionFraction, 0, 1);
        SnapSide = string.Equals(SnapSide, "Left", StringComparison.OrdinalIgnoreCase) ? "Left" : "Right";
        BlackBrightnessThreshold = (byte)Math.Clamp((int)BlackBrightnessThreshold, 0, 128);
        BlackColorTolerance = (byte)Math.Clamp((int)BlackColorTolerance, 0, 128);
    }
}
