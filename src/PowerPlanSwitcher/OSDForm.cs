namespace PowerPlanSwitcher;

public sealed class OSDForm : Form
{
    private readonly Label _label;
    private readonly Panel _iconPanel;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PictureBox _iconPicture;

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
            Padding = new Padding(12, 8, 16, 8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _iconPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
        };

        _iconPicture = new PictureBox
        {
            Size = new Size(24, 24),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
        };
        _iconPanel.Controls.Add(_iconPicture);
        _iconPicture.Dock = DockStyle.Fill;

        _label = new Label
        {
            Text = string.Empty,
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        layout.Controls.Add(_iconPanel, 0, 0);
        layout.Controls.Add(_label, 1, 0);
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

        _iconPicture.Image = !string.IsNullOrEmpty(planSystemName)
            ? PlanIconLibrary.GetImage(planSystemName, 24)
            : null;

        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            screen.Left + (screen.Width - Width) / 2,
            screen.Bottom - Height - 64);

        if (!Visible)
            Show();
        else
            Refresh();

        _timer.Stop();
        _timer.Interval = Math.Max(200, durationMs);
        _timer.Start();
    }
}
