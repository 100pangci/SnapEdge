using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using SnapEdge.App.Services;
using SnapEdge.App.Windows;
using SnapEdge.Core;

namespace SnapEdge.App;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private AppSettings _settings = new();
    private SettingsService? _settingsService;
    private WindowTracker? _windowTracker;
    private FloatingButtonWindow? _floatingButton;
    private SettingsWindow? _settingsWindow;
    private TrayService? _tray;
    private readonly IWindowCaptureSource _captureService = new GraphicsCaptureService();
    private readonly ScreenshotStorage _screenshotStorage = new();
    private int _captureInProgress;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(initiallyOwned: true, name: @"Local\SnapEdge.Instance", createdNew: out _ownsMutex);
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

        try
        {
            _settingsService = new SettingsService();
            _settings = await _settingsService.LoadAsync();
            _windowTracker = new WindowTracker();
            _tray = new TrayService(OpenSettings, Shutdown);
            _floatingButton = new FloatingButtonWindow(_settings, _settingsService, CaptureFromButtonAsync, OpenSettings);
            _floatingButton.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"SnapEdge 启动失败：{exception.Message}", "SnapEdge", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private async Task CaptureFromButtonAsync()
    {
        if (Interlocked.Exchange(ref _captureInProgress, 1) != 0)
            return;

        try
        {
            var hWnd = _windowTracker?.GetCaptureTarget() ?? 0;
            if (hWnd == 0)
            {
                _tray?.ShowCaptureFailure("没有可用的前台窗口。");
                return;
            }

            var frame = await _captureService.CaptureAsync(hWnd);
            var pixels = frame.BgraPixels;
            var width = frame.Width;
            var height = frame.Height;

            if (_settings.CropClientArea)
            {
                var clientCrop = ClientAreaCropper.GetCropRectangle(hWnd, width, height);
                if (clientCrop != CropRectangle.Full(width, height))
                    pixels = PixelBufferCropper.Crop(pixels, width, height, clientCrop);
                width = clientCrop.Width;
                height = clientCrop.Height;
            }

            if (_settings.AutoCropBlackBorders)
            {
                var blackCrop = BlackBorderDetector.Detect(pixels, width, height, _settings.ToBlackBorderOptions());
                if (blackCrop != CropRectangle.Full(width, height))
                    pixels = PixelBufferCropper.Crop(pixels, width, height, blackCrop);
                width = blackCrop.Width;
                height = blackCrop.Height;
            }

            var savedPath = await _screenshotStorage.SavePngAsync(_settings.SaveDirectory, pixels, width, height);
            if (_settings.CopyToClipboard)
            {
                try
                {
                    var bitmap = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, checked(width * 4));
                    bitmap.Freeze();
                    Clipboard.SetImage(bitmap);
                }
                catch (ExternalException)
                {
                    _tray?.ShowCaptureFailure("截图已保存，但剪贴板当前不可用。");
                }
            }

            if (_settings.ShowCaptureFeedback)
                _tray?.ShowCaptureSuccess(savedPath);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _tray?.ShowCaptureFailure(Shorten(exception.Message));
        }
        finally
        {
            Interlocked.Exchange(ref _captureInProgress, 0);
        }
    }

    private void OpenSettings()
    {
        if (_settingsService is null)
            return;
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _settingsService, () => _floatingButton?.ApplySettings());
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            _floatingButton?.ApplySettings();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private static string Shorten(string message) => message.Length <= 60 ? message : message[..57] + "…";

    protected override void OnExit(ExitEventArgs e)
    {
        _windowTracker?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex)
        {
            try
            {
                _instanceMutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
