using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json;

namespace EncyPcCheck;

/// <summary>
/// Kleines schwebendes Fenster im ENCY-Stil: zeigt CPU, GPU, Arbeitsspeicher und Kernauslastung von ENCY und den Anmeldestatus
/// beim Kundenportal. Klick klappt es zu einem kleinen Icon ein, ziehen verschiebt es, rechte Maustaste oeffnet das Menue.
/// </summary>
internal sealed class MonitorWidget : Form
{
    private sealed class Pos { public int OffX { get; set; } public int OffY { get; set; } }

    private static string PosFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EncyPcCheck", "widget.json");

    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    private delegate bool EnumProc(IntPtr h, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private const double K = 0.72; // Gesamtgroesse des Widgets
    private int U(int v) => (int)Math.Round(v * K * DeviceDpi / 96.0);

    private int _offX = int.MinValue, _offY = int.MinValue; // Abstand zur oberen rechten Ecke des ENCY-Fensters
    private bool _attached;
    private readonly ResourceMonitor _mon = new();
    private readonly SupportApi _api = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _miToggle = new(), _miCheck = new(), _miSupport = new(), _miHide = new();
    private bool _collapsed;
    private int _unread, _pollTick;
    private bool _polling, _sessionOk;
    private Point _down, _winAtDown;
    private bool _dragging, _pressed;
    private Bitmap? _logo;

    public MonitorWidget()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        Theme.Init(this);
        Font = new Font("Segoe UI", 8f);
        Opacity = 0.97;
        Text = "ENCY PC-Check";
        LoadLogo();
        LoadPosition();
        _collapsed = true; // beim Start immer eingeklappt
        ApplySize();
        Location = DefaultLocation();

        _menu.BackColor = Theme.Surface; _menu.ForeColor = Theme.Ink;
        _menu.Items.AddRange(new ToolStripItem[] { _miToggle, _miCheck, _miSupport, new ToolStripSeparator(), _miHide });
        foreach (ToolStripItem i in _menu.Items) { i.BackColor = Theme.Surface; i.ForeColor = Theme.Ink; }
        _miToggle.Click += (_, _) => Toggle();
        _miCheck.Click += (_, _) => OpenCheck();
        _miSupport.Click += (_, _) => OpenSupport();
        _miHide.Click += (_, _) => Close();
        _menu.Opening += (_, _) => ApplyTexts();
        ContextMenuStrip = _menu;
        ApplyTexts();

        _timer.Tick += async (_, _) => await TickAsync();
        Shown += async (_, _) => { _mon.Sample(); _timer.Start(); await SyncSessionAsync(); };
        FormClosing += (_, _) => { _timer.Stop(); SavePosition(); };
        FormClosed += (_, _) => { _mon.Dispose(); _logo?.Dispose(); };
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; } // Toolfenster, nicht aktivieren
    }

    // ---------------- Daten ----------------

    private async Task TickAsync()
    {
        _mon.Sample();
        FollowEncy();
        Invalidate();
        if (++_pollTick % 60 == 0 && _sessionOk && !_polling)
        {
            _polling = true;
            try { _unread = await _api.ChatUnreadAsync(); } catch { /* naechster Versuch */ } finally { _polling = false; }
        }
    }

    /// <summary>Gespeicherte Anmeldung des Kundenportals erneuern: der Kunde bleibt angemeldet.</summary>
    private async Task SyncSessionAsync()
    {
        try
        {
            _api.LoadSavedSession();
            _sessionOk = _api.HasSession && await _api.RestoreAsync();
            _unread = _sessionOk ? await _api.ChatUnreadAsync() : 0;
        }
        catch { _sessionOk = false; }
        if (!IsDisposed) Invalidate();
    }

    // ---------------- Aktionen ----------------

    private void Toggle()
    {
        _collapsed = !_collapsed;
        var old = Bounds;
        ApplySize();
        if (!FollowEncy()) Location = ClampToScreen(new Point(old.Right - Width, old.Top));
        Invalidate();
    }

    private void OpenCheck()
    {
        try { using var f = new PcCheckForm(); f.ShowDialog(); } catch { }
    }

    private async void OpenSupport()
    {
        try
        {
            using var f = new SupportForm("", "ENCY Support", () => SupportForm.CaptureForm(this));
            f.ShowDialog();
        }
        catch { }
        await SyncSessionAsync();
    }

    private void ApplyTexts()
    {
        _miToggle.Text = _collapsed ? Loc.T("Ausklappen", "Expand") : Loc.T("Einklappen", "Collapse");
        _miCheck.Text = Loc.T("PC-Check öffnen", "Open PC check");
        _miSupport.Text = Loc.T("Support / Chat", "Support / chat");
        _miHide.Text = Loc.T("Ausblenden", "Hide");
    }

    // ---------------- Groesse und Lage ----------------

    private void ApplySize()
    {
        Size = _collapsed ? new Size(U(188), U(30)) : new Size(U(272), U(232));
        Region?.Dispose();
        using var p = Theme.Round(new Rectangle(0, 0, Width, Height), U(_collapsed ? 16 : 10));
        Region = new Region(p);
    }

    /// <summary>Hauptfenster von ENCY (dieser Prozess): sichtbares Fenster ohne Besitzer, groesste Flaeche, nicht unsere eigenen.</summary>
    private IntPtr FindEncyWindow()
    {
        uint me = (uint)Environment.ProcessId;
        IntPtr best = IntPtr.Zero; long bestArea = 0;
        EnumWindows((h, _) =>
        {
            if (h == Handle || !IsWindowVisible(h) || GetWindow(h, 4) != IntPtr.Zero) return true; // GW_OWNER
            GetWindowThreadProcessId(h, out uint pid);
            if (pid != me || (GetWindowLong(h, -20) & 0x80) != 0) return true; // kein Toolfenster
            if (!GetWindowRect(h, out var r)) return true;
            long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
            if (area > bestArea) { bestArea = area; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    /// <summary>Haengt das Widget an die obere rechte Ecke des ENCY-Fensters. Gibt false zurueck, wenn kein Fenster gefunden wurde.</summary>
    private bool FollowEncy()
    {
        IntPtr w = FindEncyWindow();
        if (w == IntPtr.Zero || IsIconic(w) || !GetWindowRect(w, out var r))
        {
            _attached = false;
            if (Visible && w != IntPtr.Zero) Hide();
            return false;
        }
        _attached = true;
        if (_offX == int.MinValue) { _offX = U(14); _offY = U(46); }
        GetWindowThreadProcessId(GetForegroundWindow(), out uint fg);
        bool ours = fg == (uint)Environment.ProcessId;
        if (!_dragging)
        {
            var pt = new Point(r.Right - Width - _offX, r.Top + _offY);
            if (Location != pt) Location = pt;
        }
        if (ours && !Visible) Show();
        else if (!ours && Visible && !_dragging) Hide();
        return true;
    }

    private Point DefaultLocation()
    {
        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        return new Point(wa.Right - Width - U(16), wa.Top + U(80));
    }

    private Point ClampToScreen(Point p)
    {
        var wa = Screen.FromPoint(p).WorkingArea;
        return new Point(Math.Clamp(p.X, wa.Left, Math.Max(wa.Left, wa.Right - Width)), Math.Clamp(p.Y, wa.Top, Math.Max(wa.Top, wa.Bottom - Height)));
    }

    private void LoadPosition()
    {
        try
        {
            if (!File.Exists(PosFile)) return;
            var r = JsonSerializer.Deserialize<Pos>(File.ReadAllText(PosFile));
            if (r != null) { _offX = r.OffX; _offY = r.OffY; }
        }
        catch { }
    }

    private void SavePosition()
    {
        try
        {
            if (_offX == int.MinValue) return;
            Directory.CreateDirectory(Path.GetDirectoryName(PosFile)!);
            File.WriteAllText(PosFile, JsonSerializer.Serialize(new Pos { OffX = _offX, OffY = _offY }));
        }
        catch { }
    }

    private void LoadLogo()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("EncyPcCheck_button.png");
            if (s != null) _logo = new Bitmap(s);
        }
        catch { }
    }

    // ---------------- Maus ----------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = true; _dragging = false;
        _down = Cursor.Position; _winAtDown = Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_pressed) return;
        var d = new Size(Cursor.Position.X - _down.X, Cursor.Position.Y - _down.Y);
        if (!_dragging && Math.Abs(d.Width) + Math.Abs(d.Height) > 4) _dragging = true;
        if (_dragging) Location = ClampToScreen(_winAtDown + d);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !_pressed) return;
        _pressed = false;
        if (_dragging)
        {
            _dragging = false;
            var w = FindEncyWindow();
            if (w != IntPtr.Zero && GetWindowRect(w, out var rr)) { _offX = rr.Right - Right; _offY = Top - rr.Top; SavePosition(); }
        }
        else if (_unread > 0 && SupportHit(e.Location)) OpenSupport();
        else Toggle();
    }

    private bool SupportHit(Point p) => !_collapsed && p.Y < U(30) && p.X > Width / 2;

    protected override void OnMouseDoubleClick(MouseEventArgs e) { base.OnMouseDoubleClick(e); }

    // ---------------- Zeichnen ----------------

    private static Color LoadColor(double pct) =>
        pct < 60 ? Theme.Green : pct < 85 ? Color.FromArgb(0xE0, 0xA2, 0x4A) : Theme.Err;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.Bg);
        if (_collapsed) PaintCollapsed(g); else PaintExpanded(g);
        using var pen = new Pen(Theme.Line);
        using var path = Theme.Round(new Rectangle(0, 0, Width - 1, Height - 1), U(_collapsed ? 16 : 10));
        g.DrawPath(pen, path);
    }

    private void PaintCollapsed(Graphics g)
    {
        int h = Height;
        DrawDot(g, U(14), h / 2, U(4), _sessionOk ? Theme.Green : Theme.Ink3);
        string gpu = _mon.EncyGpu >= 0 ? $"{_mon.EncyGpu:0}%" : "-";
        TextRenderer.DrawText(g, $"CPU {_mon.EncyCpu:0}%   GPU {gpu}", Font, new Rectangle(U(26), 0, Width - U(30), h), Theme.Ink,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.Left);
        if (_unread > 0) DrawBadge(g, Width - U(14), h / 2);
    }

    private void PaintExpanded(Graphics g)
    {
        int pad = U(14), w = Width - 2 * pad;
        // Kopf
        using (var b = new SolidBrush(Theme.Surface)) g.FillRectangle(b, 0, 0, Width, U(30));
        using (var b = new SolidBrush(Theme.Green)) g.FillRectangle(b, 0, U(30), Width, U(2));
        if (_logo != null) g.DrawImage(_logo, new Rectangle(pad, U(5), U(20), U(20)));
        using (var bold = new Font(Font, FontStyle.Bold))
            TextRenderer.DrawText(g, "ENCY", bold, new Rectangle(pad + U(26), 0, U(80), U(30)), Theme.Ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        string state = _sessionOk ? Loc.T("Portal angemeldet", "Portal signed in") : Loc.T("Portal: nicht angemeldet", "Portal: signed out");
        using (var small = new Font(Font.FontFamily, 8f))
            TextRenderer.DrawText(g, state, small, new Rectangle(Width / 2 - U(24), 0, Width / 2 + U(24) - pad - U(14), U(30)), Theme.Ink2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.Right);
        DrawDot(g, Width - pad + U(2), U(15), U(4), _sessionOk ? Theme.Green : Theme.Ink3);
        if (_unread > 0) DrawBadge(g, Width - pad - U(6) - U(100), U(15));

        int y = U(44);
        DrawMeter(g, pad, y, w, "CPU", _mon.EncyCpu, $"{_mon.EncyCpu:0} %"); y += U(38);
        DrawMeter(g, pad, y, w, "GPU", _mon.EncyGpu, _mon.EncyGpu >= 0 ? $"{_mon.EncyGpu:0} %" : Loc.T("nicht verfügbar", "n/a")); y += U(38);
        double ramPct = _mon.TotalRamGb > 0 ? _mon.EncyRamGb / _mon.TotalRamGb * 100 : 0;
        DrawMeter(g, pad, y, w, "RAM", ramPct, $"{_mon.EncyRamGb:0.0} / {_mon.TotalRamGb:0} GB"); y += U(40);

        // Kernauslastung (gesamtes System, je logischem Kern)
        TextRenderer.DrawText(g, Loc.T("Kerne", "Cores"), Font, new Rectangle(pad, y, w, U(16)), Theme.Ink2,
            TextFormatFlags.NoPadding | TextFormatFlags.Left);
        int n = _mon.CoreLoad.Length, top = y + U(20), hBar = Height - top - U(12);
        float gap = U(2), bw = Math.Max(2f, (w - gap * (n - 1)) / n);
        for (int i = 0; i < n; i++)
        {
            float x = pad + i * (bw + gap);
            using (var back = new SolidBrush(Theme.Surface2)) g.FillRectangle(back, x, top, bw, hBar);
            double v = _mon.CoreLoad[i];
            float fh = (float)(hBar * v / 100.0);
            using var fill = new SolidBrush(LoadColor(v));
            g.FillRectangle(fill, x, top + hBar - fh, bw, fh);
        }
    }

    private void DrawMeter(Graphics g, int x, int y, int w, string label, double pct, string value)
    {
        TextRenderer.DrawText(g, label, Font, new Rectangle(x, y, w / 2, U(18)), Theme.Ink2, TextFormatFlags.NoPadding | TextFormatFlags.Left);
        using (var bold = new Font(Font, FontStyle.Bold))
            TextRenderer.DrawText(g, value, bold, new Rectangle(x + w / 2, y, w / 2, U(18)), Theme.Ink, TextFormatFlags.NoPadding | TextFormatFlags.Right);
        var bar = new Rectangle(x, y + U(21), w, U(6));
        using (var p = Theme.Round(bar, U(3))) using (var b = new SolidBrush(Theme.Surface2)) g.FillPath(b, p);
        if (pct > 0)
        {
            int fw = Math.Max(U(6), (int)(w * Math.Clamp(pct, 0, 100) / 100.0));
            using var p = Theme.Round(new Rectangle(bar.X, bar.Y, fw, bar.Height), U(3));
            using var b = new SolidBrush(LoadColor(pct));
            g.FillPath(b, p);
        }
    }

    private void DrawDot(Graphics g, int cx, int cy, int r, Color c)
    {
        using var b = new SolidBrush(c);
        g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
    }

    private void DrawBadge(Graphics g, int cx, int cy)
    {
        int r = U(8);
        using (var b = new SolidBrush(Theme.Green)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        using var small = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
        TextRenderer.DrawText(g, _unread > 9 ? "9+" : _unread.ToString(), small, new Rectangle(cx - r, cy - r, 2 * r, 2 * r), Theme.GreenInk,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Dispose(); _menu.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Verwaltet das Widget in einem eigenen STA-Thread (ENCY-Prozess), Start und Ende ohne ENCY zu blockieren.</summary>
internal static class WidgetHost
{
    private static readonly object Lock = new();
    private static Thread? _thread;
    private static MonitorWidget? _form;

    public static void EnsureStarted()
    {
        lock (Lock)
        {
            if (_thread is { IsAlive: true }) return;
            _thread = new Thread(() =>
            {
                try
                {
                    var f = new MonitorWidget();
                    lock (Lock) _form = f;
                    Application.Run(f);
                }
                catch { /* Widget ist optional: kein Fehler nach ENCY */ }
                finally { lock (Lock) _form = null; }
            }) { IsBackground = true, Name = "EncyPcCheck.Widget" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    public static void Stop()
    {
        Thread? t; MonitorWidget? f;
        lock (Lock) { t = _thread; f = _form; }
        try { if (f is { IsDisposed: false, IsHandleCreated: true }) f.BeginInvoke(new Action(f.Close)); } catch { }
        try { t?.Join(3000); } catch { }
    }
}
