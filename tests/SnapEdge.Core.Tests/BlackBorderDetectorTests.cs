using SnapEdge.Core;
using Xunit;

namespace SnapEdge.Core.Tests;

public sealed class BlackBorderDetectorTests
{
    [Fact]
    public void KeepsAnAllBlackImageBecauseThereIsNoContentSignal()
    {
        var image = CreateImage(160, 120, (_, _) => (0, 0, 0));

        var crop = BlackBorderDetector.Detect(image, 160, 120);

        Assert.Equal(CropRectangle.Full(160, 120), crop);
    }

    [Fact]
    public void FindsUniformBandsOnAllFourSidesWithoutResampling()
    {
        const int width = 320;
        const int height = 240;
        const int border = 20;
        var image = CreateImage(width, height, (x, y) =>
            x < border || x >= width - border || y < border || y >= height - border
                ? ((byte)8, (byte)9, (byte)10)
                : ((byte)110, (byte)90, (byte)70));

        var crop = BlackBorderDetector.Detect(image, width, height);
        var pixels = PixelBufferCropper.Crop(image, width, height, crop);

        Assert.Equal(new CropRectangle(border, border, width - 2 * border, height - 2 * border), crop);
        Assert.Equal((width - 2 * border) * (height - 2 * border) * 4, pixels.Length);
        Assert.Equal((byte)110, pixels[0]);
        Assert.Equal((byte)70, pixels[2]);
    }

    [Fact]
    public void AcceptsSlightChannelVariationWhenLuminanceRemainsNearBlack()
    {
        const int width = 160;
        const int height = 120;
        const int border = 16;
        var image = CreateImage(width, height, (x, y) =>
            x < border || x >= width - border || y < border || y >= height - border
                ? ((byte)20, (byte)16, (byte)15)
                : ((byte)100, (byte)90, (byte)80));

        var crop = BlackBorderDetector.Detect(image, width, height);

        Assert.Equal(new CropRectangle(border, border, width - 2 * border, height - 2 * border), crop);
    }

    [Fact]
    public void DoesNotTreatDarkContentAsAConfirmedBorder()
    {
        var image = CreateImage(160, 120, (_, _) => (3, 4, 5));

        var crop = BlackBorderDetector.Detect(image, 160, 120);

        Assert.Equal(CropRectangle.Full(160, 120), crop);
    }

    [Fact]
    public void KeepsADarkImageWithOnlyASmallBrightHighlight()
    {
        const int width = 160;
        const int height = 120;
        var image = CreateImage(width, height, (_, _) => ((byte)3, (byte)4, (byte)5));
        for (var y = 55; y < 65; y++)
        {
            for (var x = 75; x < 85; x++)
            {
                var index = (y * width + x) * 4;
                image[index] = 240;
                image[index + 1] = 240;
                image[index + 2] = 240;
            }
        }

        var crop = BlackBorderDetector.Detect(image, width, height);

        Assert.Equal(CropRectangle.Full(width, height), crop);
    }

    [Fact]
    public void DoesNotCropWhenBorderLinesAreNotNearlyUniform()
    {
        const int width = 160;
        const int height = 120;
        var image = CreateImage(width, height, (x, y) =>
            y < 18 && x % 3 != 0 ? ((byte)0, (byte)0, (byte)0) : ((byte)90, (byte)80, (byte)70));

        var crop = BlackBorderDetector.Detect(image, width, height);

        Assert.Equal(CropRectangle.Full(width, height), crop);
    }

    private static byte[] CreateImage(int width, int height, Func<int, int, (byte R, byte G, byte B)> pixel)
    {
        var bytes = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = pixel(x, y);
                var index = (y * width + x) * 4;
                bytes[index] = color.R;
                bytes[index + 1] = color.G;
                bytes[index + 2] = color.B;
                bytes[index + 3] = 255;
            }
        }
        return bytes;
    }
}

public sealed class PixelBufferCropperTests
{
    [Fact]
    public void CopiesTheSelectedRowsAndColumnsExactly()
    {
        var source = new byte[]
        {
            1, 2, 3, 255, 4, 5, 6, 255,
            7, 8, 9, 255, 10, 11, 12, 255,
        };

        var result = PixelBufferCropper.Crop(source, 2, 2, new CropRectangle(1, 0, 1, 2));

        Assert.Equal(new byte[] { 4, 5, 6, 255, 10, 11, 12, 255 }, result);
    }
}
