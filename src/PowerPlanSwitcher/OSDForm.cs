namespace PowerPlanSwitcher;

public sealed class OSDForm : Form
{
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly TableLayoutPanel _layout;
    private Image? _icon;

    public OSDForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(20, 20, 20);
        ForeColor = Color.White;
        Opacity = 0.82;
        AutoScaleMode = AutoScaleMode.Dpi;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Padding = new Padding(48, 8, 16, 8),
        };
        _layout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _label = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        layout.Controls.Add(_label, 0, 0);
        layout.Paint += DrawIcon;
        Controls.Add(layout);

        Size = new Size(260, 48);
        StartPosition = FormStartPosition.Manual;

        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += (_, _) => Hide();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;
            return cp;
        }
    }

    public void ShowMessage(string text, int durationMs, double opacity, string? planSystemName = null)
    {
        _label.Text = text;
        Opacity = opacity;

        _icon = !string.IsNullOrEmpty(planSystemName)
            ? PlanIconLibrary.GetImage(planSystemName, 24)
            : null;
        Invalidate();

        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            screen.Left + (screen.Width - Width) / 2,
            screen.Bottom - Height - 64);

        if (!Visible)
            Show();
        else
        {
            _layout.Invalidate();
            Refresh();
        }

        _timer.Stop();
        _timer.Interval = Math.Max(200, durationMs);
        _timer.Start();
    }

    private void DrawIcon(object? sender, PaintEventArgs e)
    {
        if (_icon is null)
            return;

        const int iconSize = 24;
        var iconArea = new Rectangle(
            (48 - iconSize) / 2,
            (Height - iconSize) / 2,
            iconSize,
            iconSize);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_icon, iconArea);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
    }
}
