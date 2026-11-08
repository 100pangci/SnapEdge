using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using SnapEdge.App.Services;

namespace SnapEdge.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Action _refreshFloatingButton;
    private readonly DispatcherTimer _saveTimer;
    private bool _initializing = true;

    public SettingsWindow(AppSettings settings, SettingsService settingsService, Action refreshFloatingButton)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
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

    private void ApplyDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var enabled = 1;
        if (DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)) != 0)
            _ = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int DwmSetWindowAttribute(nint windowHandle, int attribute, ref int value, int valueSize);

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
