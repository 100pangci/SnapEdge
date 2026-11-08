namespace SnapEdge.Core;

public static class PixelBufferCropper
{
    /// <summary>Copies a rectangle of an four-channel pixel buffer without scaling or altering pixel values.</summary>
    public static byte[] Crop(ReadOnlySpan<byte> pixels, int width, int height, CropRectangle crop)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length != checked(width * height * 4))
            throw new ArgumentException("The buffer must contain exactly width × height RGBA pixels.", nameof(pixels));
        if (crop.IsEmpty || crop.X < 0 || crop.Y < 0 || crop.X > width - crop.Width || crop.Y > height - crop.Height)
            throw new ArgumentOutOfRangeException(nameof(crop), "The crop rectangle must be contained by the source image.");

        var result = new byte[checked(crop.Width * crop.Height * 4)];
        var sourceStride = checked(width * 4);
        var rowBytes = checked(crop.Width * 4);
        for (var y = 0; y < crop.Height; y++)
        {
            var sourceOffset = checked((crop.Y + y) * sourceStride + crop.X * 4);
            pixels.Slice(sourceOffset, rowBytes).CopyTo(result.AsSpan(y * rowBytes, rowBytes));
        }
        return result;
    }
}
