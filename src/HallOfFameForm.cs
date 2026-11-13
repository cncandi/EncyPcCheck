using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace EncyPcCheck;

/// <summary>
/// Anzeige der Eintraege im Stil von ENCY 3 (dunkles Schema, gruener Akzent).
/// Palette nach den ENCY-Gestaltungsregeln (design-rules.md, Abschnitt 3). Nur Windows-Forms-Bordmittel, keine Fremdbibliothek.
/// </summary>
internal sealed class HallOfFameForm : Form
{
    // ENCY-Palette (dunkles Schema)
    private static readonly Color Bg = Color.FromArgb(0x1E, 0x20, 0x28);
    private static readonly Color Surface = Color.FromArgb(0x30, 0x34, 0x39);
    private static readonly Color Surface2 = Color.FromArgb(0x32, 0x34, 0x3C);
    private static readonly Color RowAlt = Color.FromArgb(0x24, 0x26, 0x2F);
    private static readonly Color RowSel = Color.FromArgb(0x49, 0x4D, 0x52);
    private static readonly Color Line = Color.FromArgb(0x41, 0x47, 0x4D);
    private static readonly Color Ink = Color.FromArgb(0xE9, 0xEB, 0xED);
    private static readonly Color Ink2 = Color.FromArgb(0x9A, 0xA1, 0xA7);
    private static readonly Color Green = Color.FromArgb(0x22, 0xC4, 0x7E);
    private static readonly Color GreenInk = Color.FromArgb(0x0B, 0x1F, 0x17);

    private const string ColRank = "rank", ColScore = "score", ColRating = "rating";

    private readonly DataGridView _grid = new();
    private readonly Label _msg = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Ink2, Visible = false };
    private readonly Label _count = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = Ink2 };
    private readonly CancellationTokenSource _cts = new();
    private readonly Font _fBold;
    private List<HofEntry> _rows = new();

    public HallOfFameForm()
    {
        Text = Loc.T("Hall of Fame - ENCY PC-Check", "Hall of Fame - ENCY PC-Check");
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(this.S(1240), this.S(540));
        MinimumSize = new Size(this.S(900), this.S(380));
        Font = new Font("Segoe UI", 9.5f);
        _fBold = new Font("Segoe UI Semibold", 9.5f);
        Theme.Init(this);
        TopMost = true;
        KeyPreview = true;

        // Kopfbereich mit Titel und Akzentlinie
        var head = new Panel { Dock = DockStyle.Top, Height = this.S(74), BackColor = Bg };
        head.Controls.Add(new Label
        {
            Text = "Hall of Fame", Font = new Font("Segoe UI Semibold", 17f), ForeColor = Ink, BackColor = Color.Transparent,
            Location = new Point(this.S(24), this.S(10)), Size = new Size(this.S(420), this.S(36)), TabStop = false,
        });
        head.Controls.Add(new Label
        {
            Text = Loc.T("Rechner im Vergleich - ENCY PC-Check", "Computers compared - ENCY PC-Check"),
            ForeColor = Ink2, BackColor = Color.Transparent,
            Location = new Point(this.S(26), this.S(46)), Size = new Size(this.S(520), this.S(22)), TabStop = false,
        });
        _count.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _count.Location = new Point(head.Width - this.S(260), this.S(40));
        _count.Size = new Size(this.S(236), this.S(24));
        head.Controls.Add(_count);
        head.Resize += (_, _) => _count.Left = head.Width - this.S(260);
        head.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = this.S(2), BackColor = Green });

        BuildGrid();
        _msg.Font = new Font("Segoe UI", 11f);
        _msg.Padding = new Padding(this.S(40));

        Controls.Add(_grid);
        Controls.Add(_msg);
        Controls.Add(head);

        _grid.CellPainting += Grid_CellPainting;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        Shown += async (_, _) => await LoadAsync();
        FormClosing += (_, _) => _cts.Cancel();
    }

    private void BuildGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Bg;
        _grid.BorderStyle = BorderStyle.None;
        _grid.GridColor = Line;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        _grid.EnableHeadersVisualStyles = false;
        _grid.RowHeadersVisible = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _grid.ColumnHeadersHeight = this.S(38);
        _grid.RowTemplate.Height = this.S(42);
        _grid.ScrollBars = ScrollBars.Both;
        foreach (Control child in _grid.Controls) Theme.DarkScroll(child);
        // ruhiges Zeichnen
        typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(_grid, true);

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface, ForeColor = Ink2, Font = _fBold,
            SelectionBackColor = Surface, SelectionForeColor = Ink2,
            Padding = new Padding(this.S(10), 0, this.S(6), 0), Alignment = DataGridViewContentAlignment.MiddleLeft,
        };
        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Bg, ForeColor = Ink, SelectionBackColor = RowSel, SelectionForeColor = Ink,
            Padding = new Padding(this.S(10), 0, this.S(6), 0), Alignment = DataGridViewContentAlignment.MiddleLeft,
            WrapMode = DataGridViewTriState.False,
        };
        _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = RowAlt };

        void Add(string name, string header, int width, bool fill = false, int minWidth = 60, bool right = false)
        {
            var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, SortMode = DataGridViewColumnSortMode.NotSortable, MinimumWidth = this.S(minWidth) };
            if (fill) { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; c.FillWeight = width; }
            else { c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None; c.Width = this.S(width); }
            if (right) c.DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
            _grid.Columns.Add(c);
        }
        Add(ColRank, Loc.T("Platz", "Rank"), 56);
        Add("name", "Name", 170);
        Add(ColScore, "Score", 150);
        Add(ColRating, Loc.T("Bewertung", "Rating"), 160);
        Add("cpu", Loc.T("Prozessor", "Processor"), 200, fill: true, minWidth: 110);
        Add("ram", "RAM", 70, right: true);
        Add("gpu", Loc.T("Grafik", "Graphics"), 170, fill: true, minWidth: 100);
        Add("disk", Loc.T("Datenträger", "Drive"), 140);
        Add("os", Loc.T("Betriebssystem", "Operating system"), 150, fill: true, minWidth: 110);
        Add("date", Loc.T("Datum", "Date"), 84);
        _grid.Columns["ram"]!.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    private static Color RatingColor(string code) => code switch
    {
        "very_good" => Color.FromArgb(0x22, 0xC4, 0x7E),
        "good" => Color.FromArgb(0x8C, 0xBE, 0x3C),
        "fair" => Color.FromArgb(0xE1, 0xBE, 0x28),
        "limited" => Color.FromArgb(0xE6, 0x82, 0x2A),
        _ => Color.FromArgb(0xC8, 0x32, 0x32),
    };

    private static string DiskText(HofEntry e)
    {
        string kind = e.DiskKind is "SSD" or "HDD" ? e.DiskKind : "";
        if (e.DiskWriteMBps is double m) return kind.Length > 0 ? $"{kind}  {m:F0} MB/s" : $"{m:F0} MB/s";
        return kind.Length > 0 ? kind : "-";
    }

    private static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Platz (Kreis fuer die ersten drei), Score (Zahl mit Balken) und Bewertung (Punkt mit Text) werden selbst gezeichnet.</summary>
    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.RowIndex >= _rows.Count) return;
        string col = _grid.Columns[e.ColumnIndex].Name;
        if (col != ColRank && col != ColScore && col != ColRating) return;

        var entry = _rows[e.RowIndex];
        var g = e.Graphics!;
        e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
        var r = e.CellBounds;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const TextFormatFlags vc = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.Left;

        if (col == ColRank)
        {
            int rank = e.RowIndex + 1, d = this.S(28);
            var circle = new Rectangle(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
            if (rank <= 3)
            {
                Color fill = rank == 1 ? Green : rank == 2 ? Color.FromArgb(0xB4, 0xBA, 0xC0) : Color.FromArgb(0xB0, 0x78, 0x46);
                using var br = new SolidBrush(fill);
                g.FillEllipse(br, circle);
                TextRenderer.DrawText(g, rank.ToString(), _fBold, circle, GreenInk, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
                TextRenderer.DrawText(g, rank.ToString(), Font, circle, Ink2, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        else if (col == ColScore)
        {
            var num = new Rectangle(r.X + this.S(10), r.Y, this.S(36), r.Height);
            TextRenderer.DrawText(g, entry.Score.ToString(), _fBold, num, Ink, vc);
            int bx = r.X + this.S(52), bw = r.Width - this.S(52) - this.S(16), bh = this.S(8);
            if (bw > this.S(20))
            {
                var track = new Rectangle(bx, r.Y + (r.Height - bh) / 2, bw, bh);
                using (var tp = Round(track, bh / 2)) using (var tb = new SolidBrush(Surface2)) g.FillPath(tb, tp);
                int fw = Math.Max(bh, (int)Math.Round(bw * Math.Clamp(entry.Score, 0, 100) / 100.0));
                var fillR = new Rectangle(bx, track.Y, fw, bh);
                using var fp = Round(fillR, bh / 2);
                using var fb = new SolidBrush(RatingColor(entry.Rating));
                g.FillPath(fb, fp);
            }
        }
        else
        {
            int dot = this.S(10);
            using (var br = new SolidBrush(RatingColor(entry.Rating)))
                g.FillEllipse(br, r.X + this.S(10), r.Y + (r.Height - dot) / 2, dot, dot);
            var tr = new Rectangle(r.X + this.S(28), r.Y, r.Width - this.S(34), r.Height);
            TextRenderer.DrawText(g, Scoring.RatingTextFromCode(entry.Rating), Font, tr, Ink, vc);
        }
        e.Handled = true;
    }

    private void ShowMessage(string text)
    {
        _grid.Visible = false;
        _msg.Text = text;
        _msg.Visible = true;
        _count.Text = "";
    }

    private async Task LoadAsync()
    {
        _count.Text = Loc.T("Daten werden geladen ...", "Loading data ...");
        try
        {
            var rows = await HallOfFameService.LoadAsync(_cts.Token);
            if (IsDisposed) return;
            if (rows == null)
            {
                string? m = HallOfFameService.MachineHash();
                ShowMessage(m != null && SubmissionStore.HasRecord(m)
                    ? Loc.T("Für diesen Rechner liegt ein Eintrag vor, die Liste ist aber noch nicht freigeschaltet. Bitte später erneut öffnen.",
                            "An entry exists for this computer, but the list is not enabled yet. Please open it again later.")
                    : Loc.T("Die Hall of Fame ist nur für Teilnehmer einsehbar. Bitte tragen Sie Ihren Rechner ein; danach haben Sie Einsicht.",
                            "The Hall of Fame can only be viewed by participants. Please enter your computer; afterwards you will have access."));
                return;
            }
            _rows = rows;
            _grid.Rows.Clear();
            foreach (var e in rows)
            {
                _grid.Rows.Add(
                    "", e.PlayerName, "", "",
                    e.CpuName ?? "",
                    e.RamGb is double r ? $"{r:F0} GB" : "",
                    e.GpuName ?? "",
                    DiskText(e),
                    e.OsName is { Length: > 0 } os ? os : "-",
                    e.CreatedAt.ToLocalTime().ToString(Loc.German ? "dd.MM.yy" : "yyyy-MM-dd"));
            }
            _grid.ClearSelection();
            if (rows.Count == 0) { ShowMessage(Loc.T("Noch keine Einträge.", "No entries yet.")); return; }
            _count.Text = Loc.T($"{rows.Count} Einträge", $"{rows.Count} entries");
        }
        catch (Exception ex)
        {
            if (!IsDisposed) ShowMessage(Loc.T("Fehler: ", "Error: ") + ex.Message);
        }
    }
}
