using SnapEdge.App.Interop;
using SnapEdge.Core;

namespace SnapEdge.App.Services;

public static class ClientAreaCropper
{
    public static CropRectangle GetCropRectangle(nint hWnd, int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameWidth));
        if (!NativeMethods.GetClientRect(hWnd, out var client) || client.Width <= 0 || client.Height <= 0)
            throw new InvalidOperationException("The target window has no client area.");

        var clientOrigin = new NativeMethods.NativePoint(0, 0);
        if (!NativeMethods.ClientToScreen(hWnd, ref clientOrigin))
            throw new InvalidOperationException("The target client area could not be mapped to screen coordinates.");
        if (Math.Abs(frameWidth - client.Width) <= 1 && Math.Abs(frameHeight - client.Height) <= 1)
            return CropRectangle.Full(frameWidth, frameHeight);

        var clientScreen = new NativeMethods.NativeRect(
            clientOrigin.X,
            clientOrigin.Y,
            clientOrigin.X + client.Width,
            clientOrigin.Y + client.Height);
        var bounds = GetPossibleWindowBounds(hWnd)
            .Where(candidate => candidate.Width >= client.Width && candidate.Height >= client.Height)
            .Select(candidate => (Bounds: candidate, Score: GetScaleScore(candidate, frameWidth, frameHeight)))
            .Where(candidate => candidate.Score.IsValid)
            .OrderBy(candidate => candidate.Score.Value)
            .FirstOrDefault();

        if (bounds.Bounds.Width <= 0)
            throw new NotSupportedException("The window frame could not be matched safely to the captured image; no file was saved.");

        var scaleX = frameWidth / (double)bounds.Bounds.Width;
        var scaleY = frameHeight / (double)bounds.Bounds.Height;
        var x = (int)Math.Round((clientScreen.Left - bounds.Bounds.Left) * scaleX);
        var y = (int)Math.Round((clientScreen.Top - bounds.Bounds.Top) * scaleY);
        var width = (int)Math.Round(client.Width * scaleX);
        var height = (int)Math.Round(client.Height * scaleY);
        var crop = new CropRectangle(x, y, width, height);

        if (crop.IsEmpty || crop.X < 0 || crop.Y < 0 || crop.X > frameWidth - crop.Width || crop.Y > frameHeight - crop.Height)
            throw new NotSupportedException("The client area does not fit the captured image; no file was saved.");
        return crop;
    }

    private static IEnumerable<NativeMethods.NativeRect> GetPossibleWindowBounds(nint hWnd)
    {
        if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DwmaExtendedFrameBounds, out NativeMethods.NativeRect extended, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NativeRect>()) == 0)
            yield return extended;
        if (NativeMethods.GetWindowRect(hWnd, out var window))
            yield return window;
    }

    private static (bool IsValid, double Value) GetScaleScore(NativeMethods.NativeRect bounds, int frameWidth, int frameHeight)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return (false, double.PositiveInfinity);
        var scaleX = frameWidth / (double)bounds.Width;
        var scaleY = frameHeight / (double)bounds.Height;
        var scaleMismatch = Math.Abs(scaleX - scaleY) / Math.Max(scaleX, scaleY);
        if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleMismatch > 0.04)
            return (false, double.PositiveInfinity);
        var scaleMagnitude = Math.Abs(Math.Log((scaleX + scaleY) / 2));
        return (true, scaleMismatch * 10 + scaleMagnitude);
    }
}
