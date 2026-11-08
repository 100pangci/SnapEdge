using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using WinRT;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace SnapEdge.App.Services;

public sealed record CapturedFrame(int Width, int Height, byte[] BgraPixels);

/// <summary>Capture extension point for adding another window-source strategy.</summary>
public interface IWindowCaptureSource
{
    Task<CapturedFrame> CaptureAsync(nint hWnd, CancellationToken cancellationToken = default);
}

/// <summary>Captures one HWND frame through Windows.Graphics.Capture and reads it back as tightly packed BGRA8.</summary>
public sealed class GraphicsCaptureService : IWindowCaptureSource
{
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid Direct3D11Texture2DIid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    public async Task<CapturedFrame> CaptureAsync(nint hWnd, CancellationToken cancellationToken = default)
    {
        if (hWnd == 0)
            throw new ArgumentException("A target window handle is required.", nameof(hWnd));
        if (!GraphicsCaptureSession.IsSupported())
            throw new NotSupportedException("Windows Graphics Capture is not available on this device.");

        var item = CreateCaptureItem(hWnd);
        using var d3dDevice = D3D11.D3D11CreateDevice(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        using var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
        var winrtDevice = CreateWinRtDevice(dxgiDevice.NativePointer);
        var size = item.Size;
        if (size.Width <= 0 || size.Height <= 0)
            throw new InvalidOperationException("The target window has no capturable content.");

        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            size);
        using var session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        var completion = new TaskCompletionSource<CapturedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackLock = new object();
        var frameClaimed = false;
        TypedEventHandler<Direct3D11CaptureFramePool, object> frameArrived = (sender, _) =>
        {
            Direct3D11CaptureFrame? frame;
            lock (callbackLock)
            {
                if (frameClaimed)
                    return;
                frame = sender.TryGetNextFrame();
                if (frame is null)
                    return;
                frameClaimed = true;
            }

            using (frame)
            {
                try
                {
                    completion.TrySetResult(ReadBgraFrame(frame.Surface, frame.ContentSize, d3dDevice));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }
        };

        framePool.FrameArrived += frameArrived;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.StartCapture();
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(6), cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException("No frame arrived from the target window within six seconds. It may be minimized or unsupported by Windows Graphics Capture.", exception);
        }
        finally
        {
            framePool.FrameArrived -= frameArrived;
        }
    }

    private static GraphicsCaptureItem CreateCaptureItem(nint hWnd)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var itemIid = GraphicsCaptureItemIid;
        var result = interop.CreateForWindow(hWnd, ref itemIid, out var abiPointer);
        Marshal.ThrowExceptionForHR(result);
        try
        {
            return MarshalInspectable<GraphicsCaptureItem>.FromAbi(abiPointer);
        }
        finally
        {
            Marshal.Release(abiPointer);
        }
    }

    private static IDirect3DDevice CreateWinRtDevice(nint dxgiDevice)
    {
        var result = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var abiPointer);
        Marshal.ThrowExceptionForHR(result);
        try
        {
            return MarshalInspectable<IDirect3DDevice>.FromAbi(abiPointer);
        }
        finally
        {
            Marshal.Release(abiPointer);
        }
    }

    private static CapturedFrame ReadBgraFrame(IDirect3DSurface surface, SizeInt32 contentSize, ID3D11Device device)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        var textureIid = Direct3D11Texture2DIid;
        var result = access.GetInterface(ref textureIid, out var texturePointer);
        Marshal.ThrowExceptionForHR(result);
        if (texturePointer == 0)
            throw new InvalidOperationException("Windows Graphics Capture returned no D3D11 texture.");

        ID3D11Texture2D source;
        try
        {
            source = new ID3D11Texture2D(texturePointer);
            texturePointer = 0;
        }
        finally
        {
            if (texturePointer != 0)
                Marshal.Release(texturePointer);
        }

        using (source)
        {
            var sourceDescription = source.Description;
            if (sourceDescription.Format != Format.B8G8R8A8_UNorm)
                throw new NotSupportedException($"Unsupported capture texture format: {sourceDescription.Format}.");
            if (contentSize.Width <= 0 || contentSize.Height <= 0 || contentSize.Width > sourceDescription.Width || contentSize.Height > sourceDescription.Height)
                throw new InvalidOperationException("The captured frame dimensions do not fit its D3D texture.");

            var width = contentSize.Width;
            var height = contentSize.Height;
            var stagingDescription = new Texture2DDescription
            {
                Width = sourceDescription.Width,
                Height = sourceDescription.Height,
                MipLevels = 1,
                ArraySize = 1,
                Format = sourceDescription.Format,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None,
            };
            using var staging = device.CreateTexture2D(stagingDescription);
            using var context = device.ImmediateContext;
            context.CopyResource(staging, source);
            var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var rowBytes = checked(width * 4);
                if (mapped.RowPitch < rowBytes)
                    throw new InvalidOperationException("The D3D11 row pitch is smaller than the captured pixel row.");
                var pixels = new byte[checked(rowBytes * height)];
                for (var y = 0; y < height; y++)
                    Marshal.Copy(IntPtr.Add(mapped.DataPointer, checked((int)(y * mapped.RowPitch))), pixels, y * rowBytes, rowBytes);
                return new CapturedFrame(width, height, pixels);
            }
            finally
            {
                context.Unmap(staging, 0);
            }
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(nint window, ref Guid iid, out nint result);
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig]
        int GetInterface(ref Guid iid, out nint result);
    }

    [DllImport("d3d11.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);
}
