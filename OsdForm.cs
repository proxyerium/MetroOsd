using System.Resources;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Borderless, non-activating, top-most overlay. Plain solid rectangle: #111212 background,
/// white text, Segoe MDL2 Assets icon left of the text. Stays up HideDelayMs then fades out.
/// </summary>
internal sealed class OsdForm : Form
{
    private const int HideDelayMs = 2000;
    private const int FadeDurationMs = 500;
    private const int FadeTickMs = 25;
    private const int PaddingX = 24;
    private const int PaddingY = 16;
    private const int IconTextGap = 10;
    private const int IconWidthPad = 4;

    // Icon glyphs are stored in OsdForm.resx so they can be extended/tuned without touching code.
    private static readonly ResourceManager Icons = new("MetroOsd.OsdForm", typeof(OsdForm).Assembly);

    /// <summary>Caps Lock ON glyph (Lock) from OsdForm.resx.</summary>
    internal static string CapsLockOnIcon => Icons.GetString("CapsLockOnIcon") ?? string.Empty;

    /// <summary>Caps Lock OFF glyph (Unlock) from OsdForm.resx.</summary>
    internal static string CapsLockOffIcon => Icons.GetString("CapsLockOffIcon") ?? string.Empty;

    private readonly Label _iconLabel;
    private readonly Label _label;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _fadeTimer;

    public OsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(0x11, 0x12, 0x12);
        ForeColor = Color.White;

        _iconLabel = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe MDL2 Assets", 16f, FontStyle.Regular),
            BackColor = BackColor,
            ForeColor = ForeColor,
        };
        Controls.Add(_iconLabel);

        _label = new Label
        {
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            BackColor = BackColor,
            ForeColor = ForeColor,
        };
        Controls.Add(_label);

        _timer = new System.Windows.Forms.Timer { Interval = HideDelayMs };
        _timer.Tick += (_, _) => BeginFadeOut();

        _fadeTimer = new System.Windows.Forms.Timer { Interval = FadeTickMs };
        _fadeTimer.Tick += OnFadeTick;
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

    public void ShowOsd(string text, string icon, Point location)
    {
        _label.Text = text;
        _iconLabel.Text = icon;

        // Measure the icon tightly (single glyph) and the text as before; center the group.
        // IconWidthPad leaves room for the glyph's right-side overhang so it is not clipped.
        Size iconSize = TextRenderer.MeasureText(icon, _iconLabel.Font, Size.Empty, TextFormatFlags.NoPadding);
        Size content = TextRenderer.MeasureText(text, _label.Font);

        int iconWidth = icon.Length == 0 ? 0 : iconSize.Width + IconWidthPad;
        int gap = iconWidth == 0 ? 0 : IconTextGap;
        int groupWidth = iconWidth + gap + content.Width;
        int groupHeight = Math.Max(iconSize.Height, content.Height);

        ClientSize = new Size(groupWidth + 2 * PaddingX, groupHeight + 2 * PaddingY);

        int left = (ClientSize.Width - groupWidth) / 2;
        int top = (ClientSize.Height - groupHeight) / 2;
        _iconLabel.SetBounds(left, top, iconWidth, groupHeight);
        _label.SetBounds(left + iconWidth + gap, top, content.Width, groupHeight);

        MoveTo(location);

        // A new show cancels any in-flight fade-out and restores full opacity.
        _fadeTimer.Stop();
        Opacity = 1f;

        _timer.Stop();
        Show();
        _timer.Start();
    }

    /// <summary>Starts the fade-out after the display timeout elapses.</summary>
    private void BeginFadeOut()
    {
        _timer.Stop();
        _fadeTimer.Stop();
        _fadeTimer.Start();
    }

    private void OnFadeTick(object? sender, EventArgs e)
    {
        float step = (float)FadeTickMs / FadeDurationMs;
        Opacity = Math.Max(0f, Opacity - step);
        if (Opacity <= 0f)
        {
            _fadeTimer.Stop();
            Opacity = 1f; // reset for the next show
            Hide();       // make the window invisible
        }
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