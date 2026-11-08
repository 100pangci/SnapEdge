using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapEdge.App.Services;

public sealed class ScreenshotStorage
{
    public async Task<string> SavePngAsync(string directory, byte[] bgraPixels, int width, int height, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(bgraPixels);
        if (width <= 0 || height <= 0 || bgraPixels.Length != checked(width * height * 4))
            throw new ArgumentException("Pixel data must be a tightly packed BGRA32 image.", nameof(bgraPixels));

        return await Task.Run(() => SavePng(directory, bgraPixels, width, height, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static string SavePng(string directory, byte[] pixels, int width, int height, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, checked(width * 4));
        bitmap.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var stem = "SnapEdge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        for (var suffix = 0; suffix < 10_000; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = suffix == 0 ? $"{stem}.png" : $"{stem}_{suffix}.png";
            var path = Path.Combine(directory, name);
            var created = false;
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
                created = true;
                encoder.Save(stream);
                stream.Flush(flushToDisk: true);
                return path;
            }
            catch (IOException exception) when (!created && File.Exists(path) && IsNameCollision(exception))
            {
                // A previous screenshot or another process already took this timestamped name.
            }
            catch
            {
                if (created)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                throw;
            }
        }

        throw new IOException("A unique screenshot filename could not be allocated.");
    }

    private static bool IsNameCollision(IOException exception)
    {
        var code = exception.HResult & 0xFFFF;
        return code is 80 or 183;
    }
}
