using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SnapEdge.App.Interop;
using SnapEdge.App.Services;

namespace SnapEdge.App.Windows;

public partial class FloatingButtonWindow : Window
{
    private const double DragThresholdDip = 8;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Func<Task> _capture;
    private readonly Action _openSettings;
    private readonly DispatcherTimer _longPressTimer;
    private readonly DispatcherTimer _autoHideTimer;
    private readonly DispatcherTimer _topmostTimer;
    private readonly DispatcherTimer _displayChangeTimer;
    private nint _hWnd;
    private NativeMethods.NativePoint _pressScreenPoint;
    private NativeMethods.NativeRect _pressWindowRect;
    private int? _touchId;
    private bool _pressed;
    private bool _dragging;
    private bool _longPressTriggered;
    private bool _isTouchPress;
    private bool _releasingMouseCapture;
    private bool _releasingTouchCapture;
    private bool _isTucked;
    private Stopwatch? _pressWatch;
    private DispatcherTimer? _snapAnimation;
    private Stopwatch? _snapWatch;
    private NativeMethods.NativeRect _snapStartRect;
    private NativeMethods.NativePoint _snapEndPoint;

    public FloatingButtonWindow(AppSettings settings, SettingsService settingsService, Func<Task> capture, Action openSettings)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _capture = capture;
        _openSettings = openSettings;
        _longPressTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _longPressTimer.Tick += OnLongPressTimerTick;
        _autoHideTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1600),
        };
        _autoHideTimer.Tick += OnAutoHideTimerTick;
        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(750),
        };
        _topmostTimer.Tick += (_, _) => EnsureTopmost();
        _displayChangeTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _displayChangeTimer.Tick += OnDisplayChangeTimerTick;
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            ApplyStoredPosition();
            EnsureTopmost();
            _topmostTimer.Start();
            RevealAndRestartAutoHide();
        };
        Unloaded += (_, _) => StopTimers();
        Closed += (_, _) => StopTimers();
        Activated += (_, _) => EnsureTopmost();
        Deactivated += (_, _) => EnsureTopmost();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
                EnsureTopmost();
        };
        MouseEnter += (_, _) => RevealAndRestartAutoHide();
        TouchEnter += (_, _) => RevealAndRestartAutoHide();
        LostMouseCapture += OnMouseLostCapture;
        LostTouchCapture += OnTouchLostCapture;
        ApplyAppearance();
    }

    public void ApplySettings()
    {
        ApplyAppearance();
        if (_hWnd != 0 && IsLoaded)
        {
            ApplyStoredPosition();
            RevealAndRestartAutoHide();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hWnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(_hWnd, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow | NativeMethods.WsExTopmost;
        NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GwlExStyle, new nint(style));
        NativeMethods.SetWindowPos(_hWnd, NativeMethods.HwndTopmost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder | NativeMethods.SwpFrameChanged);
        HwndSource.FromHwnd(_hWnd)?.AddHook(WindowProc);
    }

    private void EnsureTopmost()
    {
        if (_hWnd == 0 || !IsLoaded || !IsVisible || !NativeMethods.IsWindow(_hWnd))
            return;

        var style = NativeMethods.GetWindowLongPtr(_hWnd, NativeMethods.GwlExStyle).ToInt64();
        if ((style & NativeMethods.WsExTopmost) == 0)
            NativeMethods.SetWindowLongPtr(_hWnd, NativeMethods.GwlExStyle, new nint(style | NativeMethods.WsExTopmost));

        NativeMethods.SetWindowPos(_hWnd, NativeMethods.HwndTopmost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);

        if (!_pressed && !_dragging && IsFullyOffScreen())
            RepositionForDisplayChange();
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmMouseActivate)
        {
            handled = true;
            return NativeMethods.MaNoActivate;
        }
        if (message == NativeMethods.WmDisplayChange || message == NativeMethods.WmDpiChanged)
        {
            // WPF and the monitor layout settle after these messages; reflow shortly afterwards.
            _displayChangeTimer.Stop();
            _displayChangeTimer.Start();
        }
        return 0;
    }

    private void OnDisplayChangeTimerTick(object? sender, EventArgs e)
    {
        _displayChangeTimer.Stop();
        if (!RepositionForDisplayChange())
            _displayChangeTimer.Start();
    }

    private bool RepositionForDisplayChange()
    {
        if (_hWnd == 0 || !IsLoaded || !NativeMethods.IsWindow(_hWnd))
            return true;
        if (_pressed || _dragging)
            return false;
        if (!NativeMethods.GetWindowRect(_hWnd, out var current))
            return true;

        StopSnapAnimation();
        var monitor = MonitorService.FindByDeviceName(_settings.MonitorDeviceName)
            ?? MonitorService.GetPrimaryOrFirst();
        if (monitor is null)
            return true;

        var (x, y) = GetStoredPosition(current, monitor);
        NativeMethods.SetWindowPos(_hWnd, NativeMethods.HwndTopmost, x, y, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);

        if (_isTucked)
            MoveToDockedPosition(tuck: true, animate: false);
        else if (_settings.AutoPeek && _settings.AutoSnap && IsLoaded && !_pressed)
            _autoHideTimer.Start();
        return true;
    }

    private bool IsFullyOffScreen()
    {
        if (!NativeMethods.GetWindowRect(_hWnd, out var rect))
            return false;
        foreach (var monitor in MonitorService.Enumerate())
        {
            if (rect.Left < monitor.Bounds.Right && rect.Right > monitor.Bounds.Left
                && rect.Top < monitor.Bounds.Bottom && rect.Bottom > monitor.Bounds.Top)
                return false;
        }
        return true;
    }

    private void StopTimers()
    {
        _topmostTimer.Stop();
        _displayChangeTimer.Stop();
    }

    private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (_touchId is not null || e.ChangedButton != MouseButton.Left)
            return;
        BeginPress(GetCursorPoint(), isTouch: false, touchId: null);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (_touchId is not null || !_pressed || _isTouchPress || e.ChangedButton != MouseButton.Left)
            return;
        _releasingMouseCapture = true;
        ReleaseMouseCapture();
        _releasingMouseCapture = false;
        CompletePress(GetCursorPoint());
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_pressed || _isTouchPress)
        {
            RevealAndRestartAutoHide();
            return;
        }
        UpdatePress(GetCursorPoint());
    }

    private void OnMouseRightUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Right)
            return;
        RevealAndRestartAutoHide();
        _openSettings();
        e.Handled = true;
    }

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        if (_pressed)
        {
            e.Handled = true;
            return;
        }
        if (!e.TouchDevice.Capture(this, CaptureMode.Element))
        {
            e.Handled = true;
            return;
        }
        _touchId = e.TouchDevice.Id;
        BeginPress(ToScreenPoint(e.GetTouchPoint(this).Position), isTouch: true, _touchId);
        e.Handled = true;
    }

    private void OnTouchMove(object sender, TouchEventArgs e)
    {
        if (!_pressed || !_isTouchPress || e.TouchDevice.Id != _touchId)
            return;
        UpdatePress(ToScreenPoint(e.GetTouchPoint(this).Position));
        e.Handled = true;
    }

    private void OnTouchUp(object sender, TouchEventArgs e)
    {
        if (!_pressed || !_isTouchPress || e.TouchDevice.Id != _touchId)
            return;
        _releasingTouchCapture = true;
        e.TouchDevice.Capture(null);
        _releasingTouchCapture = false;
        CompletePress(ToScreenPoint(e.GetTouchPoint(this).Position));
        _touchId = null;
        e.Handled = true;
    }

    private void BeginPress(NativeMethods.NativePoint screenPoint, bool isTouch, int? touchId)
    {
        StopSnapAnimation();
        SetOpacity(_settings.ButtonOpacity);
        _autoHideTimer.Stop();
        _pressed = true;
        _dragging = false;
        _longPressTriggered = false;
        _isTouchPress = isTouch;
        _touchId = touchId;
        _pressScreenPoint = screenPoint;
        _pressWatch = Stopwatch.StartNew();
        NativeMethods.GetWindowRect(_hWnd, out _pressWindowRect);
        if (isTouch)
            _longPressTimer.Start();
    }

    private void UpdatePress(NativeMethods.NativePoint screenPoint)
    {
        if (_longPressTriggered)
            return;
        var dx = screenPoint.X - _pressScreenPoint.X;
        var dy = screenPoint.Y - _pressScreenPoint.Y;
        if (!_dragging)
        {
            var scale = Math.Max(1, (NativeMethods.GetDpiForWindow(_hWnd) / 96.0));
            if (dx * dx + dy * dy < Math.Pow(DragThresholdDip * scale, 2))
                return;
            _dragging = true;
            _longPressTimer.Stop();
        }

        NativeMethods.SetWindowPos(
            _hWnd,
            0,
            _pressWindowRect.Left + dx,
            _pressWindowRect.Top + dy,
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private void CompletePress(NativeMethods.NativePoint screenPoint)
    {
        if (!_pressed)
            return;
        _longPressTimer.Stop();
        var touchLongPress = _isTouchPress && (_longPressTriggered || _pressWatch?.Elapsed >= TimeSpan.FromMilliseconds(500));
        _pressed = false;
        if (_dragging)
        {
            if (_settings.AutoSnap)
                SnapToNearestEdge(screenPoint);
            else
                PersistCurrentPosition();
        }
        else if (touchLongPress)
        {
            if (!_longPressTriggered)
            {
                _longPressTriggered = true;
                _openSettings();
            }
        }
        else if (!_longPressTriggered)
        {
            _ = _capture();
        }
        _pressWatch?.Stop();
        _pressWatch = null;
        _dragging = false;
        _isTouchPress = false;
        RevealAndRestartAutoHide();
    }

    private void OnMouseLostCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_releasingMouseCapture && _pressed && !_isTouchPress)
            CancelPress();
    }

    private void OnTouchLostCapture(object? sender, TouchEventArgs e)
    {
        if (_releasingTouchCapture || !_pressed || !_isTouchPress || e.TouchDevice.Id != _touchId)
            return;
        CancelPress();
    }

    private void CancelPress()
    {
        _longPressTimer.Stop();
        if (_dragging)
        {
            if (_settings.AutoSnap && NativeMethods.GetWindowRect(_hWnd, out var rect))
                SnapToNearestEdge(new NativeMethods.NativePoint(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2));
            else
                PersistCurrentPosition();
        }
        _pressed = false;
        _dragging = false;
        _longPressTriggered = false;
        _isTouchPress = false;
        _touchId = null;
        _pressWatch?.Stop();
        _pressWatch = null;
        RevealAndRestartAutoHide();
    }

    private void OnLongPressTimerTick(object? sender, EventArgs e)
    {
        _longPressTimer.Stop();
        if (!_pressed || _dragging)
            return;
        _longPressTriggered = true;
        _openSettings();
    }

    private void SnapToNearestEdge(NativeMethods.NativePoint pointer)
    {
        var monitor = MonitorService.FindNearest(pointer) ?? MonitorService.GetPrimaryOrFirst();
        if (monitor is null || !NativeMethods.GetWindowRect(_hWnd, out var current))
        {
            PersistCurrentPosition();
            return;
        }
        var work = monitor.WorkArea;
        var leftDistance = Math.Abs(current.Left - work.Left);
        var rightDistance = Math.Abs(work.Right - current.Right);
        var side = leftDistance <= rightDistance ? "Left" : "Right";
        _isTucked = false;
        var x = side == "Left" ? work.Left : work.Right - current.Width;
        var y = Math.Clamp(current.Top, work.Top, Math.Max(work.Top, work.Bottom - current.Height));
        _settings.MonitorDeviceName = monitor.DeviceName;
        _settings.SnapSide = side;
        _settings.HorizontalPositionFraction = side == "Left" ? 0 : 1;
        _settings.VerticalPositionFraction = ToFraction(y, work.Top, work.Bottom - current.Height);
        AnimateSnap(current, x, y);
        _ = SavePositionAsync();
    }

    private void PersistCurrentPosition()
    {
        _isTucked = false;
        if (!NativeMethods.GetWindowRect(_hWnd, out var current))
            return;
        var center = new NativeMethods.NativePoint(current.Left + current.Width / 2, current.Top + current.Height / 2);
        var monitor = MonitorService.FindNearest(center);
        if (monitor is null)
            return;
        _settings.MonitorDeviceName = monitor.DeviceName;
        _settings.HorizontalPositionFraction = ToFraction(current.Left, monitor.WorkArea.Left, monitor.WorkArea.Right - current.Width);
        _settings.VerticalPositionFraction = ToFraction(current.Top, monitor.WorkArea.Top, monitor.WorkArea.Bottom - current.Height);
        _ = SavePositionAsync();
    }

    private void ApplyStoredPosition()
    {
        if (_hWnd == 0 || !NativeMethods.GetWindowRect(_hWnd, out var current))
            return;
        _autoHideTimer.Stop();
        StopSnapAnimation();
        _isTucked = false;
        var monitor = MonitorService.FindByDeviceName(_settings.MonitorDeviceName) ?? MonitorService.GetPrimaryOrFirst();
        if (monitor is null)
            return;
        var (x, y) = GetStoredPosition(current, monitor);
        NativeMethods.SetWindowPos(_hWnd, 0, x, y, 0, 0, NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    private (int X, int Y) GetStoredPosition(NativeMethods.NativeRect current, MonitorDetails monitor)
    {
        var work = monitor.WorkArea;
        var x = _settings.AutoSnap
            ? string.Equals(_settings.SnapSide, "Left", StringComparison.OrdinalIgnoreCase) ? work.Left : work.Right - current.Width
            : work.Left + (int)((work.Width - current.Width) * _settings.HorizontalPositionFraction);
        var y = work.Top + (int)((work.Height - current.Height) * _settings.VerticalPositionFraction);
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - current.Width));
        y = Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - current.Height));
        return (x, y);
    }

    private void AnimateSnap(NativeMethods.NativeRect current, int targetX, int targetY)
    {
        StopSnapAnimation();
        _snapStartRect = current;
        _snapEndPoint = new NativeMethods.NativePoint(targetX, targetY);
        _snapWatch = Stopwatch.StartNew();
        _snapAnimation = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _snapAnimation.Tick += OnSnapAnimationTick;
        _snapAnimation.Start();
    }

    private void OnSnapAnimationTick(object? sender, EventArgs e)
    {
        if (_snapWatch is null || _hWnd == 0)
        {
            StopSnapAnimation();
            return;
        }
        var t = Math.Clamp(_snapWatch.Elapsed.TotalMilliseconds / 140, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        var x = _snapStartRect.Left + (int)((_snapEndPoint.X - _snapStartRect.Left) * eased);
        var y = _snapStartRect.Top + (int)((_snapEndPoint.Y - _snapStartRect.Top) * eased);
        NativeMethods.SetWindowPos(_hWnd, 0, x, y, 0, 0, NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
        if (t >= 1)
            StopSnapAnimation();
    }

    private void StopSnapAnimation()
    {
        if (_snapAnimation is not null)
        {
            _snapAnimation.Stop();
            _snapAnimation.Tick -= OnSnapAnimationTick;
            _snapAnimation = null;
        }
        _snapWatch = null;
    }

    private void ApplyAppearance()
    {
        _settings.Normalize();
        Width = _settings.ButtonSize;
        Height = _settings.ButtonSize;
        SetOpacity(_settings.ButtonOpacity);
        if (!_settings.AutoPeek || !_settings.AutoSnap || !IsLoaded || _pressed)
            _autoHideTimer.Stop();
        else
            _autoHideTimer.Start();
    }

    private void RevealAndRestartAutoHide()
    {
        SetOpacity(_settings.ButtonOpacity);
        _autoHideTimer.Stop();
        if (_isTucked)
            MoveToDockedPosition(tuck: false, animate: true);
        if (_settings.AutoPeek && _settings.AutoSnap && IsLoaded && !_pressed)
            _autoHideTimer.Start();
    }

    private void OnAutoHideTimerTick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();
        if (!_settings.AutoPeek || !_settings.AutoSnap || !IsLoaded || _pressed)
            return;
        if (IsMouseOver)
        {
            _autoHideTimer.Start();
            return;
        }
        MoveToDockedPosition(tuck: true, animate: true);
    }

    private void MoveToDockedPosition(bool tuck, bool animate)
    {
        if (_hWnd == 0 || !IsLoaded || !NativeMethods.GetWindowRect(_hWnd, out var current))
            return;

        var center = new NativeMethods.NativePoint(current.Left + current.Width / 2, current.Top + current.Height / 2);
        var monitor = MonitorService.FindByDeviceName(_settings.MonitorDeviceName) ?? MonitorService.FindNearest(center);
        if (monitor is null)
        {
            _isTucked = false;
            return;
        }

        var leftSide = string.Equals(_settings.SnapSide, "Left", StringComparison.OrdinalIgnoreCase);
        var workArea = monitor.WorkArea;
        var targetX = leftSide ? workArea.Left : workArea.Right - current.Width;
        var targetY = Math.Clamp(current.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - current.Height));
        if (tuck)
        {
            if ((leftSide && workArea.Left > monitor.Bounds.Left)
                || (!leftSide && workArea.Right < monitor.Bounds.Right))
            {
                _isTucked = false;
                return;
            }
            var hiddenWidth = (current.Width + 1) / 2;
            targetX = leftSide
                ? monitor.Bounds.Left - hiddenWidth
                : monitor.Bounds.Right - (current.Width - hiddenWidth);
            if (!CanTuckOutsideMonitor(monitor, leftSide, targetX, targetY, current.Width, current.Height))
            {
                _isTucked = false;
                return;
            }
        }

        _isTucked = tuck;
        if (current.Left == targetX && current.Top == targetY)
            return;
        if (animate)
        {
            AnimateSnap(current, targetX, targetY);
            return;
        }

        StopSnapAnimation();
        NativeMethods.SetWindowPos(_hWnd, 0, targetX, targetY, 0, 0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);
    }

    private bool CanTuckOutsideMonitor(MonitorDetails monitor, bool leftSide, int targetX, int targetY, int width, int height)
    {
        var hiddenLeft = leftSide ? targetX : monitor.Bounds.Right;
        var hiddenRight = leftSide ? monitor.Bounds.Left : targetX + width;
        foreach (var other in MonitorService.Enumerate())
        {
            if (other.Handle == monitor.Handle)
                continue;
            if (hiddenLeft < other.Bounds.Right && hiddenRight > other.Bounds.Left
                && targetY < other.Bounds.Bottom && targetY + height > other.Bounds.Top)
                return false;
        }
        return hiddenRight > hiddenLeft;
    }

    private void SetOpacity(double opacity)
    {
        Opacity = Math.Clamp(opacity, 0.25, 1.0);
    }

    private NativeMethods.NativePoint GetCursorPoint()
    {
        NativeMethods.GetCursorPos(out var point);
        return point;
    }

    private NativeMethods.NativePoint ToScreenPoint(System.Windows.Point point)
    {
        var screen = PointToScreen(point);
        return new NativeMethods.NativePoint((int)Math.Round(screen.X), (int)Math.Round(screen.Y));
    }

    private static double ToFraction(int value, int minimum, int maximum) =>
        maximum <= minimum ? 0 : Math.Clamp((value - minimum) / (double)(maximum - minimum), 0, 1);

    private async Task SavePositionAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
