namespace PowerPlanSwitcher;

public sealed class ApplicationHost : IDisposable
{
    private readonly Settings _settings;
    private readonly PowerPlanService _service;
    private readonly MainWindow _mainWindow;
    private readonly OSDForm _osd;
    private readonly TrayIconController _tray;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _displayRestoreTimer;
    private PlanMenuForm? _menu;
    private Guid? _activeGuid;
    private int _activeIndex = -1;
    private List<PowerPlan> _plans = new();
    private bool _hotkeySelection;
    private bool _disposed;
    private volatile bool _exiting;

    public ApplicationHost(Settings settings)
    {
        _settings = settings;
        _service = new PowerPlanService(_settings);
        _mainWindow = new MainWindow();
        _osd = new OSDForm();
        _tray = new TrayIconController(_settings, _service);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = _settings.Behavior.RefreshIntervalMs };
        _refreshTimer.Tick += (_, _) => OnRefresh();

        _displayRestoreTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        _displayRestoreTimer.Tick += (_, _) =>
        {
            _displayRestoreTimer.Stop();
            _tray.RecreateNotifyIcon();
        };

        _mainWindow.HotkeyPressed += OnHotkeyPressed;
        _mainWindow.WinKeyReleased += OnWinKeyReleased;
        _mainWindow.EscapePressed += OnEscapePressed;
        _mainWindow.SystemThemeChanged += (_, _) => _tray.ApplyThemeChange();
        _mainWindow.DisplayChanged += (_, _) => ScheduleTrayRestore();
        _mainWindow.TaskbarCreated += (_, _) =>
        {
            _displayRestoreTimer.Stop();
            _tray.RecreateNotifyIcon();
        };
        _mainWindow.ShutdownRequested += OnShutdownRequested;

        _tray.MenuRequested += (_, _) => ShowMenu(false);
        _tray.ExitRequested += (_, _) => Exit();

        if (!_settings.Hotkey.Enabled || !_mainWindow.RegisterHotkey(_settings.Hotkey.Key))
        {
            _tray.Notify("热键注册失败", $"请检查 {_settings.FilePath} 中的 Key 配置，避免冲突。", ToolTipIcon.Error);
        }

        _tray.Initialize();
        _refreshTimer.Start();
        OnRefresh();
    }

    private void ScheduleTrayRestore()
    {
        if (_exiting) return;
        _displayRestoreTimer.Stop();
        _displayRestoreTimer.Start();
    }

    private void OnRefresh()
    {
        if (_exiting) return;
        try
        {
            _plans = _service.EnumeratePlans();
            _activeGuid = _service.GetActiveGuid();
            var newActive = _plans.FindIndex(p => p.IsActive);

            _tray.RefreshPlans();

            if (_menu is not null && _menu.IsHandleCreated && _menu.Visible)
            {
                if (newActive != _activeIndex && !_hotkeySelection)
                    _menu.SetHighlight(newActive >= 0 ? newActive : 0);
            }

            _activeIndex = newActive;
        }
        catch
        {
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_hotkeySelection && _menu is not null && !_menu.IsDisposed && _menu.Visible)
        {
            _menu.CycleHighlight(1);
            return;
        }

        ShowMenu(true);
    }

    private void ShowMenu(bool hotkeySelection)
    {
        if (_plans.Count == 0)
        {
            _tray.Notify("电源计划不可用", "未检测到可用计划。", ToolTipIcon.Warning);
            return;
        }

        if (_menu is not null && !_menu.IsDisposed && _menu.Visible)
        {
            if (_hotkeySelection && hotkeySelection)
                return;
            _menu.Close();
        }

        _hotkeySelection = hotkeySelection;
        if (_activeIndex < 0) _activeIndex = 0;
        if (_activeIndex >= _plans.Count) _activeIndex = _plans.Count - 1;

        var menu = new PlanMenuForm(_plans, _activeIndex, hotkeySelection);
        menu.PlanClicked += (_, plan) => Confirm(plan);
        menu.Canceled += (_, _) => CloseMenu();
        _menu = menu;

        var workArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        menu.PositionAtWorkArea(workArea);
        menu.Show();
        menu.Activate();
    }

    private void CloseMenu()
    {
        var menu = _menu;
        _menu = null;
        _hotkeySelection = false;

        if (menu is null || menu.IsDisposed)
            return;

        menu.SetClosing();
        menu.Close();
        menu.Dispose();
    }

    private void OnWinKeyReleased(object? sender, EventArgs e)
    {
        if (!_hotkeySelection || _menu is null)
            return;

        var plan = _menu.SelectedPlan;
        CloseMenu();

        if (plan is not null)
            Activate(plan);
    }

    private void OnEscapePressed(object? sender, EventArgs e)
    {
        if (!_hotkeySelection || _menu is null)
            return;
        if (_settings.Behavior.CancelOnEscape)
            CloseMenu();
    }

    private void Confirm(PowerPlan plan)
    {
        if (!_hotkeySelection)
        {
            CloseMenu();
            Activate(plan);
        }
    }

    private void Activate(PowerPlan plan)
    {
        CloseMenu();

        var previousGuid = _service.GetActiveGuid();
        if (previousGuid.HasValue && previousGuid.Value == plan.Guid)
        {
            _activeGuid = plan.Guid;
            _activeIndex = _plans.FindIndex(p => p.Guid == plan.Guid);
            _tray.RefreshPlans();
            return;
        }

        try
        {
            if (_service.SetActive(plan.Guid))
            {
                _activeGuid = plan.Guid;
                _activeIndex = _plans.FindIndex(p => p.Guid == plan.Guid);
                _tray.RefreshPlans();
                if (_settings.Osd.Enabled)
                    _osd.ShowMessage(plan.DisplayName, _settings.Osd.DurationMs, _settings.Osd.Opacity, plan.SystemName);
            }
            else
            {
                _tray.Notify("切换失败", $"无法激活电源方案：{plan.DisplayName}", ToolTipIcon.Error);
                if (_settings.Osd.Enabled)
                    _osd.ShowMessage($"切换失败：{plan.DisplayName}", _settings.Osd.DurationMs, _settings.Osd.Opacity);
            }
        }
        catch
        {
            _tray.Notify("切换失败", "发生异常。", ToolTipIcon.Error);
            if (_settings.Osd.Enabled)
                _osd.ShowMessage($"切换失败：{plan.DisplayName}", _settings.Osd.DurationMs, _settings.Osd.Opacity);
        }
    }

    public void Run()
    {
        Application.Run(_mainWindow);
    }

    public void Exit()
    {
        if (_exiting) return;
        _exiting = true;

        try { _refreshTimer.Stop(); } catch { }
        try { _displayRestoreTimer.Stop(); } catch { }
        _mainWindow.UnregisterHotkey();
        CloseMenu();
        Dispose();
        Application.ExitThread();
        Environment.Exit(0);
    }

    private void OnShutdownRequested(object? sender, EventArgs e)
    {
        Exit();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Dispose();
        _displayRestoreTimer.Dispose();
        _tray.Dispose();
        _mainWindow.Dispose();
        _osd.Dispose();
        GC.SuppressFinalize(this);
    }
}