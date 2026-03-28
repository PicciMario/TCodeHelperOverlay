using System.Drawing;
using System.Windows.Forms;

namespace TCodeLaunchpad.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService(Action openLauncher, Action hideLauncher, Action reloadData, Action exitApp)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => openLauncher());
        menu.Items.Add("Hide", null, (_, _) => hideLauncher());
        menu.Items.Add("Reload Data", null, (_, _) => reloadData());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exitApp());

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "TCode Launchpad",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => openLauncher();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
