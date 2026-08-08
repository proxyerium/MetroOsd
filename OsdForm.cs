using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Borderless, non-activating, top-most overlay. Plain solid rectangle: #111212 background,
/// white text. Auto-hides after a short delay.
/// </summary>
internal sealed class OsdForm : Form
{
    private const int HideDelayMs = 1500;
    private const int PaddingX = 24;
    private const int PaddingY = 16;

    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _timer;

    public OsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(0x11, 0x12, 0x12);
        ForeColor = Color.White;

        _label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Font = new Font("Segoe UI", 14f, FontStyle.Regular),
            BackColor = BackColor,
            ForeColor = ForeColor,
        };
        Controls.Add(_label);

        _timer = new System.Windows.Forms.Timer { Interval = HideDelayMs };
        _timer.Tick += (_, _) => Hide();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE | (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    public void ShowOsd(string text, Point location)
    {
        _label.Text = text;
        Size content = TextRenderer.MeasureText(text, _label.Font);
        ClientSize = new Size(content.Width + 2 * PaddingX, content.Height + 2 * PaddingY);

        MoveTo(location);

        _timer.Stop();
        Show();
        _timer.Start();
    }

    /// <summary>Repositions an already-visible overlay (keeps size and hide timer).</summary>
    public void MoveTo(Point location)
    {
        // Keep the overlay inside the working area in case the native OSD sits near an edge.
        Rectangle wa = Screen.FromPoint(location).WorkingArea;
        int x = Math.Clamp(location.X, wa.Left, Math.Max(wa.Left, wa.Right - Width));
        int y = Math.Clamp(location.Y, wa.Top, Math.Max(wa.Top, wa.Bottom - Height));
        Location = new Point(x, y);
    }
}
