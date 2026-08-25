namespace PowerPlanSwitcher;

public sealed class TrayIconController : IDisposable
{
    private NotifyIcon _notifyIcon;
    private readonly Settings _settings;
    private readonly PowerPlanService _service;
    private Guid? _currentActive;
    private readonly Dictionary<Guid, Icon> _icons = new();
    private bool _disposed;

    public event EventHandler? MenuRequested;
    public event EventHandler? ExitRequested;

    public TrayIconController(Settings settings, PowerPlanService service)
    {
        _settings = settings;
        _service = service;
        _notifyIcon = new NotifyIcon();
    }

    public void Initialize()
    {
        _notifyIcon.Icon = GetIcon(Guid.Empty);
        _notifyIcon.Text = "Power Plan Switcher";
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseClick += OnNotifyIconMouseClick;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        RefreshPlans();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var m = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("显示电源方案") { Image = GetIconImage(Guid.Empty) };
        showItem.Click += (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        m.Items.Add(showItem);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(exitItem);
        return m;
    }

    private void OnNotifyIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            MenuRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshPlans()
    {
        try
        {
            var plans = _service.EnumeratePlans();
            var active = _service.GetActiveGuid();

            if (active.HasValue && active != _currentActive)
            {
                _currentActive = active;
                var icon = GetIcon(active.Value);
                _notifyIcon.Icon = icon;
            }

            var activePlan = plans.FirstOrDefault(p => p.IsActive) ?? plans.FirstOrDefault();
            _notifyIcon.Text = activePlan?.DisplayName ?? "Power Plan Switcher";
        }
        catch
        {
        }
    }

    public void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private Icon GetIcon(Guid guid)
    {
        if (!_icons.TryGetValue(guid, out var icon))
        {
            icon = guid == Guid.Empty
                ? LoadIcon("generic.ico")
                : LoadPlanIcon(guid);
            _icons[guid] = icon;
        }
        return icon;
    }

    private Icon LoadPlanIcon(Guid guid)
    {
        return GetKnownPlanIcon(guid) ?? LoadIcon("generic.ico");
    }

    private Icon? GetKnownPlanIcon(Guid guid)
    {
        if (guid == new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"))
            return SystemIcons.Information;
        if (guid == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"))
            return SystemIcons.WinLogo;
        if (guid == new Guid("a1841308-3541-4fab-bc81-f71556f20b4a"))
            return SystemIcons.Shield;
        if (guid == new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61"))
            return SystemIcons.Application;
        return null;
    }

    private Icon LoadIcon(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "balanced.ico" or "balanced" => SystemIcons.Information,
            "highperf.ico" or "highperf" => SystemIcons.WinLogo,
            "powersaver.ico" or "powersaver" => SystemIcons.Shield,
            "ultimate.ico" or "ultimate" => SystemIcons.Application,
            _ => SystemIcons.Application
        };
    }

    private Image GetIconImage(Guid guid)
    {
        return GetIcon(guid).ToBitmap();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.DoubleClick -= (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.Dispose();
        foreach (var ic in _icons.Values)
            ic.Dispose();
    }
}
