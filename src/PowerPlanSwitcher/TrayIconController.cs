namespace PowerPlanSwitcher;

public sealed class TrayIconController : IDisposable
{
    private NotifyIcon _notifyIcon;
    private readonly Settings _settings;
    private readonly PowerPlanService _service;
    private Guid? _currentActive;
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
        _notifyIcon.Icon = PlanIconLibrary.GetIcon("Balanced", SystemInformation.SmallIconSize.Width);
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
        var showItem = new ToolStripMenuItem("显示电源方案") { Image = PlanIconLibrary.GetImage("Balanced", 16) };
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

    public void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private Image GetIconImage(Guid guid)
    {
        return PlanIconLibrary.GetImage(guid);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.DoubleClick -= (_, _) => MenuRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.Dispose();
    }
}
