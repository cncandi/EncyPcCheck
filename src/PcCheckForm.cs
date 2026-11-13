using System.Drawing;
using System.Reflection;

namespace EncyPcCheck;

/// <summary>Fenster mit Leistungsindex, Bewertung und Einzelwerten (Deutsch/Englisch umschaltbar).</summary>
internal sealed class PcCheckForm : Form
{
    private static readonly Color[] Colors =
    {
        Color.FromArgb(200, 50, 50),   // nicht geeignet
        Color.FromArgb(230, 130, 40),  // eingeschraenkt
        Color.FromArgb(225, 190, 40),  // bedingt
        Color.FromArgb(140, 190, 60),  // gut
        Color.FromArgb(40, 160, 70),   // sehr gut
    };

    private const string SiteText = "www.cnc-technik.de";

    private readonly Label _lblIndex = new() { Font = new Font("Segoe UI", 40, FontStyle.Bold), AutoSize = true };
    private readonly Label _lblRating = new() { Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true };
    private readonly Label _lblHint = new() { AutoSize = false, Height = 40, Dock = DockStyle.Top };
    private readonly Panel _scale = new() { Height = 34, Dock = DockStyle.Top };
    private readonly Label _gpuWarn = new() { Dock = DockStyle.Top, Height = 24, Padding = new Padding(10, 3, 10, 0), ForeColor = Color.FromArgb(190, 30, 30), Font = new Font("Segoe UI", 9, FontStyle.Bold), Visible = false };
    private readonly TableLayoutPanel _cases = new() { Dock = DockStyle.Top, Height = 104, ColumnCount = 3, RowCount = 4, Padding = new Padding(10, 4, 10, 4) };
    private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill };
    private readonly Label _lblStatus = new() { AutoSize = true, Dock = DockStyle.Bottom, Height = 20, Padding = new Padding(10, 0, 10, 0) };
    private readonly Label _note = new() { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(10, 2, 10, 2), ForeColor = SystemColors.GrayText };
    private readonly Button _btnRun = new() { AutoSize = true };
    private readonly Button _btnCopy = new() { AutoSize = true, Enabled = false };
    private readonly Button _btnMail = new() { AutoSize = true, Enabled = false };
    private readonly Button _btnHof = new() { AutoSize = true };
    private readonly Button _btnSubmit = new() { AutoSize = true, Enabled = false };
    private readonly Button _btnClose = new() { AutoSize = true };
    private readonly LinkLabel _promo = new() { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(10, 4, 10, 4) };
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };

    private CancellationTokenSource? _cts;
    private CheckResult? _result;
    private SystemData? _data;
    private double _single, _multi;
    private double? _disk;
    private bool _running;
    private bool _measured;

    public PcCheckForm()
    {
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(880, 720);
        MinimumSize = new Size(760, 680);
        Font = new Font("Segoe UI", 9);
        TopMost = true; // ohne Owner-Fenster sonst hinter ENCY
        LoadIcon();

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(10, 6, 10, 0), WrapContents = false };
        var scoreBox = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        scoreBox.Controls.Add(_lblRating);
        top.Controls.Add(_lblIndex);
        top.Controls.Add(scoreBox);
        _lblIndex.Margin = new Padding(0, 0, 20, 0);
        _lblRating.Margin = new Padding(0, 18, 0, 0);

        _lang.Items.AddRange(new object[] { "Deutsch", "English" });
        _lang.SelectedIndex = Loc.German ? 0 : 1;
        var langPanel = new Panel { Dock = DockStyle.Right, Width = 120, Padding = new Padding(0, 10, 10, 0) };
        _lang.Dock = DockStyle.Top;
        langPanel.Controls.Add(_lang);
        var header = new Panel { Dock = DockStyle.Top, Height = 80 };
        header.Controls.Add(top);
        header.Controls.Add(langPanel);

        _lblHint.Padding = new Padding(10, 4, 10, 0);
        _scale.Padding = new Padding(10, 4, 10, 4);
        _scale.Paint += PaintScale;
        _scale.Resize += (_, _) => _scale.Invalidate();

        _cases.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300));
        _cases.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        _cases.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) _cases.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        _list.Columns.Add("", 140);
        _list.Columns.Add("", 300);
        _list.Columns.Add("", 55, HorizontalAlignment.Right);
        _list.Columns.Add("", 240);
        var listHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6) };
        listHost.Controls.Add(_list);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 5, 10, 5) };
        buttons.Controls.Add(_btnClose);
        buttons.Controls.Add(_btnCopy);
        buttons.Controls.Add(_btnMail);
        buttons.Controls.Add(_btnSubmit);
        buttons.Controls.Add(_btnHof);
        buttons.Controls.Add(_btnRun);

        Controls.Add(listHost);
        Controls.Add(_lblStatus);
        Controls.Add(_note);
        Controls.Add(buttons);
        Controls.Add(_promo);
        Controls.Add(_cases);
        Controls.Add(_gpuWarn);
        Controls.Add(_lblHint);
        Controls.Add(_scale);
        Controls.Add(header);

        _btnRun.Click += async (_, _) => await RunAsync();
        _btnCopy.Click += (_, _) => CopyText();
        _btnClose.Click += (_, _) => Close();
        _btnMail.Click += (_, _) => SendMail();
        _btnHof.Click += (_, _) => ShowHallOfFame();
        _btnSubmit.Click += async (_, _) => await SubmitAsync();
        _promo.LinkClicked += (_, _) => OpenSite();
        _lang.SelectedIndexChanged += (_, _) => { Loc.German = _lang.SelectedIndex == 0; ApplyLanguage(); };
        Shown += async (_, _) => await RunAsync();
        FormClosing += (_, _) => _cts?.Cancel();

        ApplyLanguage();
    }

    private void LoadIcon()
    {
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("EncyPcCheck_button.png");
            if (s == null) return;
            using var bmp = new Bitmap(s);
            Icon = Icon.FromHandle(bmp.GetHicon());
        }
        catch { /* Standardsymbol bleibt */ }
    }

    /// <summary>Setzt alle Texte in der gewaehlten Sprache; Ergebnis wird ohne neue Messung neu aufgebaut.</summary>
    private void ApplyLanguage()
    {
        Text = "ENCY PC-Check";
        _btnRun.Text = Loc.T("Erneut prüfen", "Run again");
        _btnCopy.Text = Loc.T("Als Text kopieren", "Copy as text");
        _btnMail.Text = Loc.T("Per E-Mail senden", "Send by e-mail");
        _btnClose.Text = Loc.T("Schließen", "Close");
        _btnHof.Text = HallOfFameService.Configured ? Loc.T("Hall of Fame ansehen", "View Hall of Fame")
                                                    : Loc.T("Hall of Fame (nicht eingerichtet)", "Hall of Fame (not set up)");
        _btnHof.Enabled = HallOfFameService.Configured;
        _btnSubmit.Text = Loc.T("In Hall of Fame eintragen", "Enter Hall of Fame");
        _list.Columns[0].Text = Loc.T("Komponente", "Component");
        _list.Columns[1].Text = Loc.T("Erkannt", "Detected");
        _list.Columns[2].Text = Loc.T("Wert", "Score");
        _list.Columns[3].Text = Loc.T("Hinweis", "Note");
        _note.Text = Loc.T(
            "Eckwerte nach den ENCY-Systemanforderungen (Minimum / empfohlen). Gewichtung und Benchmark-Referenzwerte sind Richtwerte; " +
            "die Messung ist eine Momentaufnahme und ersetzt keinen Test mit dem eigenen Projekt. " +
            "100 entspricht einem Referenzrechner (Ryzen 9 9950X3D, RTX 5090 32 GB, 96 GB RAM, NVMe). Mainboard, Netzteil, Kühlung und Gehäuse werden nicht geprüft.",
            "Reference points follow the ENCY system requirements (minimum / recommended). Weighting and benchmark reference values are guidelines; " +
            "the measurement is a snapshot and does not replace a test with your own project. " +
            "100 corresponds to a reference PC (Ryzen 9 9950X3D, RTX 5090 32 GB, 96 GB RAM, NVMe). Mainboard, power supply, cooling and case are not checked.");
        _promo.Text = Loc.T(
            "Datentechnik Reitz - CAD/CAM-Lösungen seit 1994: SprutCAM ENCY, Robotik und CNC.   " + SiteText + "\r\n" +
            "Weitere ENCY-Add-ons von uns: Drawing (technische Zeichnungen aus ENCY) und Assembly (Baugruppen ausrichten) - mehr auf cnc-technik.de.",
            "Datentechnik Reitz - CAD/CAM solutions since 1994: SprutCAM ENCY, robotics and CNC.   " + SiteText + "\r\n" +
            "More ENCY add-ons from us: Drawing (technical drawings from ENCY) and Assembly (align parts in assemblies) - more at cnc-technik.de.");
        _promo.LinkArea = new LinkArea(_promo.Text.IndexOf(SiteText, StringComparison.Ordinal), SiteText.Length);
        if (_measured) Rebuild();
        else if (_running) { _lblRating.Text = Loc.T("Messung läuft", "Measuring"); }
    }

    private void PaintScale(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        int x0 = _scale.Padding.Left, w = _scale.Width - _scale.Padding.Horizontal;
        int barH = 14, y = 10;
        int[] edges = { 0, 30, 50, 70, 85, 100 };
        for (int i = 0; i < 5; i++)
        {
            int xa = x0 + w * edges[i] / 100, xb = x0 + w * edges[i + 1] / 100;
            using var b = new SolidBrush(Colors[i]);
            g.FillRectangle(b, xa, y, xb - xa - 1, barH);
        }
        if (_result != null)
        {
            int mx = x0 + w * _result.Index / 100;
            using var p = new Pen(Color.Black, 3);
            g.DrawLine(p, mx, y - 6, mx, y + barH + 6);
        }
    }

    private async Task RunAsync()
    {
        if (_running) return;
        _running = true;
        _measured = false;
        _btnRun.Enabled = false;
        _btnCopy.Enabled = false;
        _btnMail.Enabled = false;
        _btnSubmit.Enabled = false;
        _result = null;
        _list.Items.Clear();
        _cases.Controls.Clear();
        _gpuWarn.Visible = false;
        _lblIndex.Text = "--";
        _lblIndex.ForeColor = SystemColors.GrayText;
        _lblRating.ForeColor = SystemColors.GrayText;
        _lblRating.Text = Loc.T("Messung läuft", "Measuring");
        _lblHint.Text = "";
        _scale.Invalidate();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        void Status(string de, string en) { try { if (!IsDisposed) BeginInvoke(() => _lblStatus.Text = Loc.T(de, en)); } catch { } }

        try
        {
            var outcome = await Task.Run(() =>
            {
                Status("Systemdaten werden gelesen ...", "Reading system data ...");
                var data = SystemData.Collect();
                Status("Prozessor: Messung mit einem Kern ...", "Processor: single-core test ...");
                double single = Benchmarks.CpuSingle(ct);
                Status("Prozessor: Messung mit allen Kernen ...", "Processor: all-core test ...");
                double multi = Benchmarks.CpuMulti(Math.Max(1, data.LogicalCores), ct);
                Status("Datenträger: Schreibtest ...", "Drive: write test ...");
                double? disk = Benchmarks.DiskWriteMBps(ct);
                return (data, single, multi, disk);
            }, ct);

            if (ct.IsCancellationRequested || IsDisposed) return;
            _data = outcome.data; _single = outcome.single; _multi = outcome.multi; _disk = outcome.disk;
            _measured = true;
            Rebuild();
            _lblStatus.Text = Loc.T("Messung abgeschlossen.", "Measurement complete.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _lblRating.Text = Loc.T("Fehler", "Error");
                _lblHint.Text = ex.Message;
                _lblStatus.Text = "";
            }
        }
        finally
        {
            _running = false;
            if (!IsDisposed) _btnRun.Enabled = true;
        }
    }

    /// <summary>Bewertung aus den gespeicherten Messwerten in der aktuellen Sprache aufbauen.</summary>
    private void Rebuild()
    {
        if (_data == null) return;
        var cats = new List<CategoryResult>
        {
            Scoring.Cpu(_data, _single, _multi),
            Scoring.Ram(_data),
            Scoring.Gpu(_data),
            Scoring.Disk(_data, _disk),
        };
        _result = Scoring.Evaluate(_data, cats, _single, _multi);
        ShowResult();
    }

    private void ShowResult()
    {
        if (_result == null || _data == null) return;
        var col = Colors[4 - (int)_result.Rating];
        _lblIndex.Text = _result.Index.ToString();
        _lblIndex.ForeColor = col;
        _lblRating.Text = Loc.T("Gesamt: ", "Overall: ") + _result.RatingText;
        _lblRating.ForeColor = col;
        string hint = _result.RatingHint;
        if (_result.KnockOut.Count > 0) hint += " " + string.Join("; ", _result.KnockOut) + ".";
        _lblHint.Text = hint;

        _gpuWarn.Text = _result.GpuWarning;
        _gpuWarn.Visible = _result.GpuWarning.Length > 0;
        _cases.Controls.Clear();
        for (int i = 0; i < _result.UseCases.Count; i++)
        {
            var u = _result.UseCases[i];
            var c = Colors[4 - (int)u.Rating];
            _cases.Controls.Add(new Label { Text = Loc.T("Geeignet für: ", "Suitable for: ") + u.Name, AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 10) }, 0, i);
            _cases.Controls.Add(new Label { Text = Scoring.RatingText(u.Rating), AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = c, Font = new Font("Segoe UI", 10, FontStyle.Bold) }, 1, i);
            _cases.Controls.Add(new Label { Text = u.Index.ToString(), AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = c }, 2, i);
        }
        _list.Items.Clear();
        AddRow(Loc.T("Betriebssystem", "Operating system"), _data.OsName, _data.IsWindows11 ? "ok" : "",
            _data.IsWindows11 ? "" : Loc.T("Windows 11 empfohlen", "Windows 11 recommended"));
        foreach (var c in _result.Categories)
            AddRow(c.Name, c.Value, c.Score?.ToString() ?? "-", c.Note);
        _scale.Invalidate();
        _btnCopy.Enabled = true;
        _btnMail.Enabled = true;
        _btnSubmit.Enabled = HallOfFameService.Configured;
    }

    private void AddRow(string a, string b, string c, string d)
    {
        var it = new ListViewItem(a);
        it.SubItems.Add(b);
        it.SubItems.Add(c);
        it.SubItems.Add(d);
        _list.Items.Add(it);
    }

    private void CopyText()
    {
        if (_result == null) return;
        try { Clipboard.SetText(ReportText.Build(_data, _result)); _lblStatus.Text = Loc.T("In die Zwischenablage kopiert.", "Copied to clipboard."); }
        catch (Exception ex) { _lblStatus.Text = Loc.T("Kopieren fehlgeschlagen: ", "Copy failed: ") + ex.Message; }
    }

    private void SendMail()
    {
        if (_result == null) return;
        try
        {
            ReportText.OpenMail("ENCY PC-Check: " + Loc.T("Leistungsindex ", "Performance index ") + _result.Index,
                ReportText.Build(_data, _result));
            _lblStatus.Text = Loc.T("Mailprogramm wurde geöffnet.", "Mail program opened.");
        }
        catch (Exception ex) { _lblStatus.Text = Loc.T("Mailprogramm nicht gestartet: ", "Mail program not started: ") + ex.Message; }
    }

    private void OpenSite()
    {
        try { ReportText.OpenUrl(AppInfo.WebsiteUrl); }
        catch (Exception ex) { _lblStatus.Text = Loc.T("Webseite nicht geöffnet: ", "Website not opened: ") + ex.Message; }
    }

    private void ShowHallOfFame()
    {
        try { using var f = new HallOfFameForm(); f.ShowDialog(this); }
        catch (Exception ex) { _lblStatus.Text = "Hall of Fame: " + ex.Message; }
    }

    private async Task SubmitAsync()
    {
        if (_result == null || _data == null) return;
        using var dlg = new SubmitForm();
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _btnSubmit.Enabled = false;
        _lblStatus.Text = Loc.T("Eintrag wird gesendet ...", "Sending entry ...");
        try
        {
            var s = new HofSubmission
            {
                PlayerName = dlg.PlayerName,
                Score = _result.Index,
                Rating = _result.RatingCode,
                CpuName = _data.CpuName,
                CpuThreads = _data.LogicalCores,
                RamGb = Math.Round(_data.RamGb, 1),
                GpuName = _data.GpuName,
                VramGb = Math.Round(_data.VramGb, 1),
                DiskKind = _data.DriveIsHdd switch { true => "HDD", false => "SSD", _ => "unknown" },
                DiskWriteMBps = _disk is double d ? Math.Round(d, 1) : null,
                CpuSingle = Math.Round(_single, 1),
                CpuMulti = Math.Round(_multi, 1),
                OsName = _data.OsName,
            };
            await HallOfFameService.SubmitAsync(s, CancellationToken.None);
            if (!IsDisposed) _lblStatus.Text = Loc.T("Eintrag gesendet. Er erscheint nach Freigabe durch Datentechnik Reitz.",
                                                     "Entry sent. It will appear after approval by Datentechnik Reitz.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed) { _lblStatus.Text = Loc.T("Senden fehlgeschlagen: ", "Sending failed: ") + ex.Message; _btnSubmit.Enabled = true; }
        }
    }
}
