namespace PowerPlanSwitcher;

public sealed class OSDForm : Form
{
    private const int CornerRadius = 16;
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
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16, 8, 16, 8),
        };
        _layout = layout;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _label = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        layout.Controls.Add(_label, 1, 0);
        layout.Paint += DrawIcon;
        Controls.Add(layout);

        Size = new Size(260, 48);
        StartPosition = FormStartPosition.Manual;
        UpdateRoundedRegion();

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
            _layout.Padding.Left + (32 - iconSize) / 2,
            (_layout.ClientSize.Height - iconSize) / 2,
            iconSize,
            iconSize);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_icon, iconArea);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRoundedRegion();
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var rect = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        int d = CornerRadius * 2;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        int d = CornerRadius * 2;
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillPath(brush, path);
    }
}
