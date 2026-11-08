namespace SnapEdge.Core;

public sealed record BlackBorderOptions
{
    /// <summary>Maximum per-channel value considered black, from 0 to 255.</summary>
    public byte BrightnessThreshold { get; init; } = 18;

    /// <summary>Maximum difference between the brightest and darkest RGB channels.</summary>
    public byte ColorTolerance { get; init; } = 20;

    /// <summary>Minimum continuous border width to remove, in pixels.</summary>
    public int MinimumBorderWidth { get; init; } = 12;

    /// <summary>Fraction of pixels in each scan line that must look black.</summary>
    public double RequiredBlackFraction { get; init; } = 0.995;

    /// <summary>Largest fraction of an image dimension that may be removed from any side.</summary>
    public double MaximumTrimFraction { get; init; } = 0.25;
}
