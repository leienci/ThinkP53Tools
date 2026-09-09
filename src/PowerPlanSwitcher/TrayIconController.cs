namespace PowerPlanSwitcher;

public sealed class TrayIconController : IDisposable
{
    private NotifyIcon _notifyIcon;
    private readonly Settings _settings;
    private readonly PowerPlanService _service;
    private Guid? _currentActive;
    private bool _disposed;
    private EventHandler? _onDoubleClick;
    private MouseEventHandler? _onMouseClick;

    public event EventHandler? MenuRequested;
    public event EventHandler? ExitRequested;

    public TrayIconController(Settings settings, PowerPlanService service)
    {
        _settings = settings;
        _service = service;
        _notifyIcon = new NotifyIcon();
        _onDoubleClick = (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        _onMouseClick = OnNotifyIconMouseClick;
    }

    public void Initialize()
    {
        SetupNotifyIcon();
    }

    private void SetupNotifyIcon()
    {
        _notifyIcon.Icon = PlanIconLibrary.GetIcon(_currentActive is null ? "Balanced" : GetPlanSystemName(_currentActive.Value), SystemInformation.SmallIconSize.Width);
        _notifyIcon.Text = "Power Plan Switcher";
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += _onDoubleClick;
        _notifyIcon.MouseClick += _onMouseClick;
        _notifyIcon.ContextMenuStrip = BuildContextMenu();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var m = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("显示电源方案") { Image = PlanIconLibrary.GetImage("Balanced", 16, true) };
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
                var matchedPlan = plans.FirstOrDefault(p => p.Guid == active.Value) ?? plans.FirstOrDefault();
                var icon = PlanIconLibrary.GetIcon(matchedPlan?.SystemName ?? "Balanced", SystemInformation.SmallIconSize.Width);
                _notifyIcon.Icon = icon;
            }

            var activePlan = plans.FirstOrDefault(p => p.IsActive) ?? plans.FirstOrDefault();
            _notifyIcon.Text = activePlan?.DisplayName ?? "Power Plan Switcher";
        }
        catch
        {
        }
    }

    public void ApplyThemeChange()
    {
        try
        {
            PlanIconLibrary.ClearCache();
            _currentActive = null;
            RefreshPlans();
            RebuildContextMenuImage();
        }
        catch
        {
        }
    }

    public void RecreateNotifyIcon()
    {
        try
        {
            if (_disposed) return;

            var contextMenu = _notifyIcon.ContextMenuStrip;
            _notifyIcon.Visible = false;
            _notifyIcon.DoubleClick -= _onDoubleClick;
            _notifyIcon.MouseClick -= _onMouseClick;
            _notifyIcon.Dispose();
            contextMenu?.Dispose();

            _notifyIcon = new NotifyIcon();
            SetupNotifyIcon();
            RefreshPlans();
        }
        catch
        {
        }
    }

    private void RebuildContextMenuImage()
    {
        if (_notifyIcon.ContextMenuStrip is null) return;
        foreach (ToolStripItem item in _notifyIcon.ContextMenuStrip.Items)
        {
            if (item is ToolStripMenuItem mi && mi.Text == "显示电源方案")
            {
                mi.Image = PlanIconLibrary.GetImage("Balanced", 16, true);
                return;
            }
        }
    }

    public void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private string GetPlanSystemName(Guid guid)
    {
        try
        {
            return _service.EnumeratePlans().FirstOrDefault(p => p.Guid == guid)?.SystemName ?? "Balanced";
        }
        catch
        {
            return "Balanced";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.DoubleClick -= _onDoubleClick;
        _notifyIcon.MouseClick -= _onMouseClick;
        _notifyIcon.Dispose();
    }
}