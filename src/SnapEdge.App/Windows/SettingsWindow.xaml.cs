using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Point = System.Windows.Point;
using MessageBox = System.Windows.MessageBox;
using SnapEdge.App.Services;

namespace SnapEdge.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Action _refreshFloatingButton;
    private const int WmNcHitTest = 0x0084;
    private const int HtMaxButton = 9;

    private readonly DispatcherTimer _saveTimer;
    private bool _initializing = true;

    public SettingsWindow(AppSettings settings, SettingsService settingsService, Action refreshFloatingButton)
    {
        InitializeComponent();
        // Register before WindowChrome so this hook can expose HTMAXBUTTON for Snap Layouts.
        SourceInitialized += OnSourceInitialized;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 46,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });
        StateChanged += OnWindowStateChanged;
        Loaded += (_, _) => SettingsScrollViewer.ScrollToTop();
        _settings = settings;
        _settingsService = settingsService;
        _refreshFloatingButton = refreshFloatingButton;
        _saveTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
        _saveTimer.Tick += OnSaveTimerTick;
        LoadSettingsIntoControls();
        _initializing = false;
        Closed += (_, _) => _ = SaveNowAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(OnWindowMessage);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmNcHitTest || !MaximizeRestoreButton.IsVisible ||
            MaximizeRestoreButton.ActualWidth <= 0 || MaximizeRestoreButton.ActualHeight <= 0)
            return IntPtr.Zero;

        var packedCoordinates = lParam.ToInt64();
        var screenPoint = new Point(
            unchecked((short)(packedCoordinates & 0xFFFF)),
            unchecked((short)((packedCoordinates >> 16) & 0xFFFF)));
        var windowPoint = PointFromScreen(screenPoint);
        var buttonOrigin = MaximizeRestoreButton.TranslatePoint(new Point(), this);
        var buttonBounds = new Rect(buttonOrigin, MaximizeRestoreButton.RenderSize);
        if (!buttonBounds.Contains(windowPoint))
            return IntPtr.Zero;

        handled = true;
        return new IntPtr(HtMaxButton);
    }

    private void OnSystemMenuClick(object sender, RoutedEventArgs e)
    {
        var button = (FrameworkElement)sender;
        SystemCommands.ShowSystemMenu(this, button.PointToScreen(new Point(0, button.ActualHeight)));
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        MaximizeGlyph.Data = Geometry.Parse(WindowState == WindowState.Maximized
            ? "M 6,2 H 13 V 9 M 3,5 H 10 V 12 H 3 Z"
            : "M 3,3 H 13 V 13 H 3 Z");
    }

    private void LoadSettingsIntoControls()
    {
        SaveDirectoryTextBox.Text = _settings.SaveDirectory;
        CropClientAreaCheckBox.IsChecked = _settings.CropClientArea;
        AutoCropBlackCheckBox.IsChecked = _settings.AutoCropBlackBorders;
        BrightnessSlider.Value = _settings.BlackBrightnessThreshold;
        ToleranceSlider.Value = _settings.BlackColorTolerance;
        CopyToClipboardCheckBox.IsChecked = _settings.CopyToClipboard;
        CaptureFeedbackCheckBox.IsChecked = _settings.ShowCaptureFeedback;
        AutoSnapCheckBox.IsChecked = _settings.AutoSnap;
        AutoPeekCheckBox.IsChecked = _settings.AutoPeek;
        OpacitySlider.Value = _settings.ButtonOpacity;
        ButtonSizeSlider.Value = _settings.ButtonSize;
        LaunchAtStartupCheckBox.IsChecked = _settings.LaunchAtStartup;
        UpdateValueLabels();
    }

    private void OnBrowseDirectory(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择截图保存文件夹",
            UseDescriptionForTitle = true,
            SelectedPath = _settings.SaveDirectory,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
            return;
        _settings.SaveDirectory = dialog.SelectedPath;
        SaveDirectoryTextBox.Text = dialog.SelectedPath;
        ScheduleSave();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;
        _settings.CropClientArea = CropClientAreaCheckBox.IsChecked == true;
        _settings.AutoCropBlackBorders = AutoCropBlackCheckBox.IsChecked == true;
        _settings.CopyToClipboard = CopyToClipboardCheckBox.IsChecked == true;
        _settings.ShowCaptureFeedback = CaptureFeedbackCheckBox.IsChecked == true;
        _settings.AutoSnap = AutoSnapCheckBox.IsChecked == true;
        _settings.AutoPeek = AutoPeekCheckBox.IsChecked == true;
        _refreshFloatingButton();
        ScheduleSave();
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateValueLabels();
        if (_initializing)
            return;
        _settings.BlackBrightnessThreshold = (byte)Math.Round(BrightnessSlider.Value);
        _settings.BlackColorTolerance = (byte)Math.Round(ToleranceSlider.Value);
        ScheduleSave();
    }

    private void OnAppearanceChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateValueLabels();
        if (_initializing)
            return;
        _settings.ButtonOpacity = OpacitySlider.Value;
        _settings.ButtonSize = ButtonSizeSlider.Value;
        _refreshFloatingButton();
        ScheduleSave();
    }

    private void OnStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;
        var enabled = LaunchAtStartupCheckBox.IsChecked == true;
        try
        {
            StartupRegistration.SetEnabled(enabled);
            _settings.LaunchAtStartup = enabled;
            ScheduleSave();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            LaunchAtStartupCheckBox.IsChecked = !enabled;
            MessageBox.Show(this, $"无法更新开机启动设置：{exception.Message}", "SnapEdge", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateValueLabels()
    {
        if (BrightnessValue is not null)
            BrightnessValue.Text = Math.Round(BrightnessSlider.Value).ToString("0");
        if (ToleranceValue is not null)
            ToleranceValue.Text = Math.Round(ToleranceSlider.Value).ToString("0");
        if (OpacityValue is not null)
            OpacityValue.Text = $"{OpacitySlider.Value:P0}";
        if (ButtonSizeValue is not null)
            ButtonSizeValue.Text = $"{Math.Round(ButtonSizeSlider.Value):0} px";
    }

    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        await SaveNowAsync();
    }

    private async Task SaveNowAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (IsLoaded)
                MessageBox.Show(this, $"无法保存设置：{exception.Message}", "SnapEdge", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
