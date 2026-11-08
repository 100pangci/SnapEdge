using System.Drawing;
using Forms = System.Windows.Forms;

namespace SnapEdge.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayService(Action openSettings, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => openSettings());
        menu.Items.Add("退出 SnapEdge", null, (_, _) => exit());
        _icon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
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
    }
}
