using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace EncyPcCheck;

/// <summary>
/// Gemeinsame Gestaltung im Stil von ENCY 3 (dunkles Schema, gruener Akzent). Palette nach den ENCY-Gestaltungsregeln.
/// Nur Windows-Forms-Bordmittel, keine Fremdbibliothek.
/// </summary>
internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(0x1E, 0x20, 0x28);
    public static readonly Color Surface = Color.FromArgb(0x30, 0x34, 0x39);
    public static readonly Color Surface2 = Color.FromArgb(0x32, 0x34, 0x3C);
    public static readonly Color Hover = Color.FromArgb(0x3B, 0x3F, 0x45);
    public static readonly Color RowAlt = Color.FromArgb(0x24, 0x26, 0x2F);
    public static readonly Color RowSel = Color.FromArgb(0x49, 0x4D, 0x52);
    public static readonly Color Line = Color.FromArgb(0x41, 0x47, 0x4D);
    public static readonly Color Ink = Color.FromArgb(0xE9, 0xEB, 0xED);
    public static readonly Color Ink2 = Color.FromArgb(0x9A, 0xA1, 0xA7);
    public static readonly Color Ink3 = Color.FromArgb(0x6B, 0x72, 0x78);
    public static readonly Color Green = Color.FromArgb(0x22, 0xC4, 0x7E);
    public static readonly Color GreenSoft = Color.FromArgb(0x1F, 0x3A, 0x31);
    public static readonly Color GreenInk = Color.FromArgb(0x0B, 0x1F, 0x17);
    public static readonly Color Err = Color.FromArgb(0xE8, 0x62, 0x58);

    /// <summary>Bewertungsfarben, Index 0 = nicht geeignet ... 4 = sehr gut geeignet.</summary>
    public static readonly Color[] RatingColors =
    {
        Color.FromArgb(0xE0, 0x4A, 0x4A),
        Color.FromArgb(0xE6, 0x82, 0x2A),
        Color.FromArgb(0xE1, 0xBE, 0x28),
        Color.FromArgb(0x8C, 0xBE, 0x3C),
        Color.FromArgb(0x22, 0xC4, 0x7E),
    };

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr hwnd, string? subApp, string? subIdList);

    /// <summary>Dunkle Farben und dunkle Titelleiste fuer ein Fenster.</summary>
    public static void Init(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = Ink;
        f.HandleCreated += (_, _) =>
        {
            try { int v = 1; DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int)); } catch { /* aeltere Windows-Version: helle Titelleiste */ }
        };
    }

    /// <summary>Dunkle Bildlaufleisten fuer native Steuerelemente (Windows 10/11).</summary>
    public static void DarkScroll(Control c)
    {
        void Apply() { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } }
        if (c.IsHandleCreated) Apply(); else c.HandleCreated += (_, _) => Apply();
    }

    public static void StyleText(TextBoxBase t)
    {
        t.BackColor = Surface2;
        t.ForeColor = Ink;
        t.BorderStyle = BorderStyle.FixedSingle;
        DarkScroll(t);
    }

    public static void StyleCombo(ComboBox c)
    {
        c.FlatStyle = FlatStyle.Flat;
        c.BackColor = Surface2;
        c.ForeColor = Ink;
    }

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = Math.Max(2, radius * 2);
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Liste im ENCY-Stil: dunkle Kopfzeile, abwechselnde Zeilen, keine Gitterlinien. Letzte Spalte fuellt die Breite.</summary>
    public static void StyleList(ListView lv, int rowHeight)
    {
        lv.OwnerDraw = true;
        lv.GridLines = false;
        lv.BorderStyle = BorderStyle.None;
        lv.BackColor = Bg;
        lv.ForeColor = Ink;
        lv.FullRowSelect = true;
        lv.HideSelection = false;
        lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lv.SmallImageList = new ImageList { ImageSize = new Size(1, rowHeight) };
        DarkScroll(lv);

        var headFont = new Font("Segoe UI Semibold", 9f);
        lv.DrawColumnHeader += (_, e) =>
        {
            using (var b = new SolidBrush(Surface)) e.Graphics.FillRectangle(b, e.Bounds);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis |
                        (e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", headFont, Rectangle.Inflate(e.Bounds, -8, 0), Ink2, flags);
        };
        lv.DrawItem += (_, e) => { e.DrawDefault = false; };
        lv.DrawSubItem += (_, e) =>
        {
            Color bg = e.Item!.Selected ? RowSel : e.ItemIndex % 2 == 1 ? RowAlt : Bg;
            using (var b = new SolidBrush(bg)) e.Graphics.FillRectangle(b, e.Bounds);
            var align = e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", lv.Font, Rectangle.Inflate(e.Bounds, -8, 0), Ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | align);
        };
        void FitLast()
        {
            if (lv.Columns.Count == 0) return;
            int others = 0;
            for (int i = 0; i < lv.Columns.Count - 1; i++) others += lv.Columns[i].Width;
            int w = Math.Max(120, lv.ClientSize.Width - others);
            if (lv.Columns[^1].Width != w) lv.Columns[^1].Width = w;
        }
        lv.Resize += (_, _) => FitLast();
        lv.HandleCreated += (_, _) => FitLast();
    }
}

/// <summary>Schaltflaeche im ENCY-Stil (abgerundet, flach). Primary = gruen hinterlegte Hauptaktion.</summary>
internal sealed class EncyButton : Button
{
    private bool _hover, _down;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }

    public EncyButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        Color fill, border, text;
        if (!Enabled) { fill = Color.FromArgb(0x25, 0x27, 0x2F); border = Theme.Line; text = Theme.Ink3; }
        else if (Primary)
        {
            fill = _down ? Color.FromArgb(0x2C, 0x5A, 0x48) : _hover ? Color.FromArgb(0x27, 0x4A, 0x3E) : Theme.GreenSoft;
            border = Theme.Green; text = Theme.Ink;
        }
        else
        {
            fill = _down ? Theme.RowSel : _hover ? Theme.Hover : Theme.Surface2;
            border = Focused && ShowFocusCues ? Theme.Green : Theme.Line; text = Theme.Ink;
        }
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Round(r, (int)Math.Round(6 * DeviceDpi / 96.0)))
        using (var fb = new SolidBrush(fill))
        using (var pen = new Pen(border))
        {
            g.FillPath(fb, path);
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>Einfache Registerleiste im ENCY-Stil (Text mit gruener Linie unter der aktiven Seite).</summary>
internal sealed class SimpleTabs : Panel
{
    private sealed class Tab : Control
    {
        public bool Selected;
        public Tab()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            TextRenderer.DrawText(e.Graphics, Text, Selected ? new Font(Font, FontStyle.Bold) : Font, ClientRectangle,
                Selected ? Theme.Ink : Theme.Ink2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (Selected)
                using (var b = new SolidBrush(Theme.Green)) e.Graphics.FillRectangle(b, 0, Height - 3, Width, 3);
        }
    }

    private readonly Panel _bar = new() { Dock = DockStyle.Top, BackColor = Theme.Bg };
    private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly List<(Panel Page, Tab Tab)> _pages = new();
    private int _selected = -1;

    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex => _selected;
    public Panel? SelectedTab => _selected >= 0 && _selected < _pages.Count ? _pages[_selected].Page : null;

    public SimpleTabs()
    {
        BackColor = Theme.Bg;
        Controls.Add(_body);
        Controls.Add(_bar);
        _bar.Paint += (_, e) =>
        {
            using var p = new Pen(Theme.Line);
            e.Graphics.DrawLine(p, 0, _bar.Height - 1, _bar.Width, _bar.Height - 1);
        };
    }

    public void AddPage(Panel page, string text)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        _body.Controls.Add(page);
        var tab = new Tab { Text = text, Height = _bar.Height };
        _bar.Height = Math.Max(_bar.Height, (int)Math.Round(34 * DeviceDpi / 96.0));
        int idx = _pages.Count;
        tab.Click += (_, _) => Select(idx);
        _pages.Add((page, tab));
        _bar.Controls.Add(tab);
        LayoutTabs();
        if (_selected < 0) Select(0);
    }

    public void SetText(Panel page, string text)
    {
        foreach (var (p, t) in _pages) if (p == page) { t.Text = text; t.Invalidate(); }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _pages.Count || index == _selected) return;
        _selected = index;
        for (int i = 0; i < _pages.Count; i++)
        {
            _pages[i].Page.Visible = i == index;
            _pages[i].Tab.Selected = i == index;
            _pages[i].Tab.Invalidate();
        }
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); LayoutTabs(); }

    private void LayoutTabs()
    {
        int x = 0, w = (int)Math.Round(120 * DeviceDpi / 96.0);
        foreach (var (_, t) in _pages) { t.SetBounds(x, 0, w, _bar.Height); x += w; }
    }
}
