using System.Runtime.InteropServices;

namespace PowerPlanSwitcher;

public sealed class PlanMenuForm : Form
{
    private readonly List<PowerPlan> _plans;
    private int _highlightedIndex;
    private readonly int _activeIndex;
    private readonly bool _hotkeySelection;
    private readonly List<Button> _buttons = new();
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;

    public event EventHandler<PowerPlan>? PlanClicked;
    public event EventHandler? Canceled;

    public PlanMenuForm(List<PowerPlan> plans, int activeIndex, bool hotkeySelection)
    {
        _plans = plans;
        _activeIndex = activeIndex;
        _highlightedIndex = activeIndex;
        _hotkeySelection = hotkeySelection;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(8);
        StartPosition = FormStartPosition.Manual;

        var rowHeight = 40;
        var maxWidth = 280;
        foreach (var p in plans)
        {
            var w = TextRenderer.MeasureText(p.DisplayName, Font).Width + 60;
            if (w > maxWidth) maxWidth = w;
        }
        ClientSize = new Size(maxWidth, plans.Count * rowHeight + 16);

        for (var i = 0; i < plans.Count; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Text = (idx == _activeIndex ? "● " : "  ") + plans[idx].DisplayName,
                Dock = DockStyle.Top,
                Height = rowHeight,
                FlatStyle = FlatStyle.Flat,
                BackColor = idx == _highlightedIndex ? Color.FromArgb(70, 130, 200) : Color.Transparent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 100, 160);
            btn.Click += (_, _) => PlanClicked?.Invoke(this, _plans[idx]);
            btn.MouseEnter += (_, _) => SetHighlight(idx);
            _buttons.Add(btn);
            Controls.Add(btn);
        }

        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    public void SetHighlight(int index)
    {
        if (index < 0 || index >= _plans.Count)
            return;
        _highlightedIndex = index;
        for (var i = 0; i < _buttons.Count; i++)
            _buttons[i].BackColor = i == index ? Color.FromArgb(70, 130, 200) : Color.Transparent;
    }

    public void CycleHighlight(int delta)
    {
        var n = _plans.Count;
        if (n == 0) return;
        var next = (_highlightedIndex + delta + n) % n;
        SetHighlight(next);
    }

    public PowerPlan? SelectedPlan => _plans.Count > 0 ? _plans[_highlightedIndex] : null;

    public void PositionAtWorkArea(Rectangle workArea)
    {
        Location = new Point(workArea.Right - Width - 8, workArea.Bottom - Height - 8);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoactivate | WsExToolwindow | WsExTopmost;
            return cp;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Canceled?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (e.KeyCode == Keys.Up)
        {
            CycleHighlight(-1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            CycleHighlight(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            if (SelectedPlan is { } p)
                PlanClicked?.Invoke(this, p);
        }
    }
}
