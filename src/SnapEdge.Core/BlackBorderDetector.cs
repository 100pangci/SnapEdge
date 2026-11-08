namespace SnapEdge.Core;

/// <summary>
/// Conservatively finds uniform near-black bands around a four-channel image. The input buffer is not modified.
/// </summary>
public static class BlackBorderDetector
{
    /// <summary>
    /// Returns the crop rectangle in source pixels. Ambiguous or all-dark images are kept intact.
    /// </summary>
    public static CropRectangle Detect(ReadOnlySpan<byte> pixels, int width, int height, BlackBorderOptions? options = null)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length != checked(width * height * 4))
            throw new ArgumentException("The buffer must contain exactly width × height four-channel pixels.", nameof(pixels));

        options ??= new BlackBorderOptions();
        ValidateOptions(options);

        var full = CropRectangle.Full(width, height);
        if (width < options.MinimumBorderWidth * 2 + 2 || height < options.MinimumBorderWidth * 2 + 2)
            return full;

        // An image with no visible non-black content has no reliable signal that a dark edge is a bar.
        if (!HasVisibleContent(pixels, width, height, options))
            return full;

        var maxXTrim = Math.Min((int)(width * options.MaximumTrimFraction), (width - 1) / 2);
        var maxYTrim = Math.Min((int)(height * options.MaximumTrimFraction), (height - 1) / 2);
        var left = ScanColumns(pixels, width, height, 0, 1, maxXTrim, options);
        var right = ScanColumns(pixels, width, height, width - 1, -1, maxXTrim, options);
        var top = ScanRows(pixels, width, height, 0, 1, maxYTrim, options);
        var bottom = ScanRows(pixels, width, height, height - 1, -1, maxYTrim, options);

        // Preserve the image if the detected bands would leave too little usable content.
        if (left + right >= width / 2 || top + bottom >= height / 2)
            return full;

        return left == 0 && right == 0 && top == 0 && bottom == 0
            ? full
            : new CropRectangle(left, top, width - left - right, height - top - bottom);
    }

    private static int ScanColumns(ReadOnlySpan<byte> pixels, int width, int height, int start, int step, int maximum, BlackBorderOptions options)
    {
        var count = 0;
        for (var offset = 0; offset < maximum; offset++)
        {
            var x = start + offset * step;
            if (!LineIsBlack(pixels, width, height, x, isColumn: true, options))
                break;
            count++;
        }
        return count >= options.MinimumBorderWidth ? count : 0;
    }

    private static int ScanRows(ReadOnlySpan<byte> pixels, int width, int height, int start, int step, int maximum, BlackBorderOptions options)
    {
        var count = 0;
        for (var offset = 0; offset < maximum; offset++)
        {
            var y = start + offset * step;
            if (!LineIsBlack(pixels, width, height, y, isColumn: false, options))
                break;
            count++;
        }
        return count >= options.MinimumBorderWidth ? count : 0;
    }

    private static bool LineIsBlack(ReadOnlySpan<byte> pixels, int width, int height, int line, bool isColumn, BlackBorderOptions options)
    {
        var length = isColumn ? height : width;
        var black = 0;
        for (var i = 0; i < length; i++)
        {
            var x = isColumn ? line : i;
            var y = isColumn ? i : line;
            var index = (y * width + x) * 4;
            var r = pixels[index];
            var g = pixels[index + 1];
            var b = pixels[index + 2];
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var luminance = Luminance(r, g, b);
            if (luminance <= options.BrightnessThreshold && max - min <= options.ColorTolerance)
                black++;
        }
        return black / (double)length >= options.RequiredBlackFraction;
    }

    private static bool HasVisibleContent(ReadOnlySpan<byte> pixels, int width, int height, BlackBorderOptions options)
    {
        var stride = Math.Max(1, Math.Min(width, height) / 128);
        var startX = width / 4;
        var endX = width - startX;
        var startY = height / 4;
        var endY = height - startY;
        var visible = 0;
        var sampled = 0;
        for (var y = startY + stride / 2; y < endY; y += stride)
        {
            for (var x = startX + stride / 2; x < endX; x += stride)
            {
                var index = (y * width + x) * 4;
                var r = pixels[index];
                var g = pixels[index + 1];
                var b = pixels[index + 2];
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                sampled++;
                if (Luminance(r, g, b) > options.BrightnessThreshold + options.ColorTolerance || max - min > options.ColorTolerance)
                    visible++;
            }
        }
        return sampled > 0 && visible >= Math.Max(1, sampled / 20);
    }

    private static double Luminance(byte channel0, byte channel1, byte channel2) => (channel0 + channel1 + channel2) / 3.0;

    private static void ValidateOptions(BlackBorderOptions options)
    {
        if (options.MinimumBorderWidth < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumBorderWidth must be positive.");
        if (options.RequiredBlackFraction is <= 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "RequiredBlackFraction must be greater than zero and at most one.");
        if (options.MaximumTrimFraction is <= 0 or >= 0.5)
            throw new ArgumentOutOfRangeException(nameof(options), "MaximumTrimFraction must be greater than zero and less than 0.5.");
    }
}
