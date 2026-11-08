using System.Drawing;
using Forms = System.Windows.Forms;

namespace SnapEdge.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _applicationIcon;

    public TrayService(Action openSettings, Action exit)
    {
        _applicationIcon = LoadApplicationIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => openSettings());
        menu.Items.Add("退出 SnapEdge", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "SnapEdge",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => openSettings();
    }

    public void ShowCaptureSuccess(string path)
    {
        var fileName = Path.GetFileName(path);
        _icon.ShowBalloonTip(1800, "SnapEdge 截图已保存", fileName, Forms.ToolTipIcon.Info);
    }

    public void ShowCaptureFailure(string message)
    {
        _icon.ShowBalloonTip(2500, "SnapEdge 截图失败", message, Forms.ToolTipIcon.Error);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _applicationIcon.Dispose();
    }

    private static Icon LoadApplicationIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/SnapEdge;component/Assets/SnapEdge.ico", UriKind.Absolute));
        if (resource is null)
            return (Icon)SystemIcons.Application.Clone();

        using (resource.Stream)
            return new Icon(resource.Stream);
    }
}
