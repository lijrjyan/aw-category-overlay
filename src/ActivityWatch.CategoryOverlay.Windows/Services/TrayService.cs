using System.Drawing;

namespace ActivityWatch.CategoryOverlay.Windows.Services;

public sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.ToolStripMenuItem _visibilityItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _editItem;
    private readonly System.Windows.Forms.ToolStripMenuItem _autostartItem;

    public TrayService(
        Func<Task> toggleVisibility,
        Func<Task> refresh,
        Action toggleEditMode,
        Action openSettings,
        Func<Task> toggleAutostart,
        Action exit)
    {
        _visibilityItem = new("Hide overlay");
        _visibilityItem.Click += async (_, _) => await toggleVisibility();

        var refreshItem = new System.Windows.Forms.ToolStripMenuItem("Refresh now");
        refreshItem.Click += async (_, _) => await refresh();

        _editItem = new System.Windows.Forms.ToolStripMenuItem("Edit layout")
        {
            CheckOnClick = false,
        };
        _editItem.Click += (_, _) => toggleEditMode();

        var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => openSettings();

        _autostartItem = new System.Windows.Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = false,
        };
        _autostartItem.Click += async (_, _) => await toggleAutostart();

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => exit();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.AddRange(
        [
            _visibilityItem,
            refreshItem,
            _editItem,
            settingsItem,
            _autostartItem,
            new System.Windows.Forms.ToolStripSeparator(),
            exitItem,
        ]);

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "ActivityWatch Category Overlay",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += async (_, _) => await toggleVisibility();
    }

    public void Update(bool isVisible, bool isEditMode, bool autostartEnabled)
    {
        _visibilityItem.Text = isVisible ? "Hide overlay" : "Show overlay";
        _editItem.Checked = isEditMode;
        _autostartItem.Checked = autostartEnabled;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}

