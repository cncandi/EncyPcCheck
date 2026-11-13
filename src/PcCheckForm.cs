using System.Drawing;
using System.Reflection;

namespace EncyPcCheck;

/// <summary>Fenster mit Leistungsindex, Bewertung und Einzelwerten (Deutsch/Englisch umschaltbar).</summary>
internal sealed class PcCheckForm : Form
{
    private static readonly Color[] Colors =
    {
        Theme.RatingColors[0],   // nicht geeignet
        Theme.RatingColors[1],   // eingeschraenkt
        Theme.RatingColors[2],   // bedingt
        Theme.RatingColors[3],   // gut
        Theme.RatingColors[4],   // sehr gut
    };

    private const string SiteText = "www.cnc-technik.de";

    private readonly Label _lblIndex = new() { Font = new Font("Segoe UI", 40, FontStyle.Bold), AutoSize = true };
    private readonly Label _lblRating = new() { Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true };
    private readonly Label _lblHint = new() { AutoSize = false, Height = 40, Dock = DockStyle.Top };
    private readonly Panel _scale = new() { Height = 34, Dock = DockStyle.Top };
    private readonly Label _gpuWarn = new() { Dock = DockStyle.Top, Height = 24, Padding = new Padding(10, 3, 10, 0), ForeColor = Theme.Err, Font = new Font("Segoe UI", 9, FontStyle.Bold), Visible = false };
    private readonly TableLayoutPanel _cases = new() { Dock = DockStyle.Top, Height = 104, ColumnCount = 3, RowCount = 4, Padding = new Padding(10, 4, 10, 4) };
    private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, Dock = DockStyle.Fill };
    private readonly Label _lblStatus = new() { AutoSize = true, Dock = DockStyle.Bottom, Height = 20, Padding = new Padding(10, 0, 10, 0) };
    private readonly Label _note = new() { Dock = DockStyle.Bottom, Height = 66, Padding = new Padding(10, 2, 10, 2), ForeColor = Theme.Ink2 };
    private readonly Button _btnRun = new EncyButton { AutoSize = true };
    private readonly Button _btnCopy = new EncyButton { AutoSize = true, Enabled = false };
    private readonly Button _btnMail = new EncyButton { AutoSize = true, Enabled = false };
    private readonly Button _btnHof = new EncyButton { AutoSize = true };
    private readonly Button _btnSupport = new EncyButton { AutoSize = true, Enabled = false };
    private readonly Button _btnSubmit = new EncyButton { AutoSize = true, Enabled = false };
    private readonly Button _btnClose = new EncyButton { AutoSize = true };
    private readonly LinkLabel _promo = new() { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(10, 4, 10, 4) };
    private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };

    private readonly ToolTip _tips = new();
    private readonly FlowLayoutPanel _buttons;
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
        Theme.Init(this);
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
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Theme.Green });
        Theme.StyleCombo(_lang);
        Theme.StyleList(_list, this.S(28));
        _promo.LinkColor = Theme.Green; _promo.ActiveLinkColor = Theme.Ink; _promo.VisitedLinkColor = Theme.Green; _promo.ForeColor = Theme.Ink2;

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

        _buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(10, 5, 10, 5) };
        _buttons.Controls.Add(_btnClose);
        _buttons.Controls.Add(_btnCopy);
        _buttons.Controls.Add(_btnMail);
        _buttons.Controls.Add(_btnSupport);
        _buttons.Controls.Add(_btnSubmit);
        _buttons.Controls.Add(_btnHof);
        _buttons.Controls.Add(_btnRun);

        Controls.Add(listHost);
        Controls.Add(_lblStatus);
        Controls.Add(_note);
        Controls.Add(_buttons);
        Controls.Add(_promo);
        Controls.Add(_cases);
        Controls.Add(_gpuWarn);
        Controls.Add(_lblHint);
        Controls.Add(_scale);
        Controls.Add(header);

        _lang.TabIndex = 0; _btnRun.TabIndex = 1; _btnHof.TabIndex = 2; _btnSubmit.TabIndex = 3;
        _btnMail.TabIndex = 4; _btnCopy.TabIndex = 5; _btnSupport.TabIndex = 6; _btnClose.TabIndex = 7; _promo.TabIndex = 8;
        foreach (var b in new[] { _btnRun, _btnHof, _btnSubmit, _btnSupport, _btnMail, _btnCopy, _btnClose })
        { b.MinimumSize = new Size(0, 30); b.Margin = new Padding(6, 0, 0, 0); b.Padding = new Padding(8, 0, 8, 0); }
        _btnRun.Click += async (_, _) => await RunAsync();
        _btnCopy.Click += (_, _) => CopyText();
        _btnClose.Click += (_, _) => Close();
        _btnMail.Click += (_, _) => SendMail();
        _btnHof.Click += (_, _) => ShowHallOfFame();
        _btnSupport.Click += (_, _) => ShowSupport();
        _btnSubmit.Click += async (_, _) => await SubmitAsync();
        _promo.LinkClicked += (_, _) => OpenSite();
        _lang.SelectedIndexChanged += (_, _) => { Loc.German = _lang.SelectedIndex == 0; ApplyLanguage(); };
        Shown += async (_, _) => { FitButtons(); await RunAsync(); };
        Resize += (_, _) => FitButtons();
        FormClosing += (_, _) => _cts?.Cancel();

        ApplyLanguage();
    }

    /// <summary>Alle Schaltflächen sichtbar halten: reicht die Breite nicht, wird die Leiste zweizeilig und höher.</summary>
    private void FitButtons()
    {
        if (!IsHandleCreated) return;
        int need = _buttons.Padding.Horizontal;
        foreach (Control c in _buttons.Controls) need += c.GetPreferredSize(Size.Empty).Width + c.Margin.Horizontal;
        bool two = need > _buttons.ClientSize.Width;
        int h = this.S(two ? 90 : 46);
        if (_buttons.WrapContents != two) _buttons.WrapContents = two;
        if (_buttons.Height != h) _buttons.Height = h;
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
        _btnCopy.Text = Loc.T("Kopieren", "Copy");
        _btnMail.Text = Loc.T("Per E-Mail", "By e-mail");
        _btnClose.Text = Loc.T("Schließen", "Close");
        _btnSupport.Text = "Support";
        _btnHof.Text = "Hall of Fame";
        _btnHof.Enabled = HallOfFameService.Configured;
        _btnSubmit.Text = Loc.T("Rechner eintragen", "Enter computer");
        _tips.SetToolTip(_btnRun, Loc.T("Messung erneut durchführen", "Run the measurement again"));
        _tips.SetToolTip(_btnHof, HallOfFameService.Configured ? Loc.T("Hall of Fame ansehen (nur für Teilnehmer)", "View the Hall of Fame (participants only)") : Loc.T("Hall of Fame ist nicht eingerichtet", "Hall of Fame is not set up"));
        _tips.SetToolTip(_btnSubmit, Loc.T("Diesen Rechner in die Hall of Fame eintragen", "Enter this computer in the Hall of Fame"));
        _tips.SetToolTip(_btnSupport, Loc.T("Ticket oder Chat mit dem Support von Datentechnik Reitz", "Ticket or chat with Datentechnik Reitz support"));
        _tips.SetToolTip(_btnMail, Loc.T("Ergebnis per E-Mail senden", "Send the result by e-mail"));
        _tips.SetToolTip(_btnCopy, Loc.T("Ergebnis als Text in die Zwischenablage kopieren", "Copy the result as text to the clipboard"));
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
        FitButtons();
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
            using var p = new Pen(Theme.Ink, 3);
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
        _btnSupport.Enabled = false;
        _btnSubmit.Enabled = false;
        _result = null;
        _list.Items.Clear();
        _cases.Controls.Clear();
        _gpuWarn.Visible = false;
        _lblIndex.Text = "--";
        _lblIndex.ForeColor = Theme.Ink3;
        _lblRating.ForeColor = Theme.Ink3;
        _lblRating.Text = Loc.T("Messung läuft", "Measuring");
        _lblHint.Text = "";
        _scale.Invalidate();

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        void Status(string de, string en) { try { if (!IsDisposed) BeginInvoke(() => _lblStatus.Text = Loc.T(de, en)); } catch { } }

        bool offer = false;
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
            offer = true;
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
        if (offer && !IsDisposed && HallOfFameService.Configured) await OfferComparisonAsync();
    }

    private async Task OfferComparisonAsync()
    {
        bool changed = false;
        string? machine = HallOfFameService.MachineHash();
        if (machine != null && _data != null)
        {
            string sig = SubmissionStore.Signature(_data);
            if (SubmissionStore.IsUnchanged(machine, sig)) { ShowAlreadyEntered(); return; }
            changed = SubmissionStore.HasRecord(machine);
            if (!changed)
            {
                // Rechner wurde vor diesem Merker eingetragen: der Server kennt ihn dann als Teilnehmer.
                try
                {
                    if (await HallOfFameService.LoadAsync(CancellationToken.None) != null)
                    {
                        SubmissionStore.Save(machine, sig);
                        if (!IsDisposed) ShowAlreadyEntered();
                        return;
                    }
                }
                catch { /* Server nicht erreichbar: Frage wie bisher */ }
            }
        }
        if (IsDisposed) return;
        bool yes = InfoDialog.Ask(this,
            Loc.T("Vergleich", "Comparison"),
            changed ? Loc.T("Hardware hat sich geändert", "Hardware has changed")
                    : Loc.T("Wie schneidet Ihr Rechner im Vergleich ab?", "How does your computer compare?"),
            changed ? Loc.T("Die Hardware dieses Rechners hat sich seit Ihrem Eintrag geändert. Möchten Sie die neuen Werte in der Hall of Fame eintragen?",
                            "The hardware of this computer has changed since your entry. Would you like to enter the new values in the Hall of Fame?")
                    : Loc.T("Möchten Sie wissen, wie Ihr Rechner im Vergleich zu anderen bewertet wird? Dazu geben Sie Ihre Firmen-E-Mail-Adresse ein und erhalten Ihre Position in der Hall of Fame.",
                            "Would you like to know how your computer compares to others? Enter your company e-mail address to receive your position in the Hall of Fame."),
            Loc.T("Ja, vergleichen", "Yes, compare"), Loc.T("Nein, danke", "No, thanks"));
        if (yes) await SubmitAsync();
    }

    private void ShowAlreadyEntered()
    {
        _btnSubmit.Enabled = false;
        _lblStatus.Text = Loc.T("Dieser Rechner ist bereits in der Hall of Fame eingetragen (Hardware unverändert).",
                                "This computer is already entered in the Hall of Fame (hardware unchanged).");
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
        _btnSupport.Enabled = true;
        _btnSubmit.Enabled = HallOfFameService.Configured && !IsAlreadyEntered();
        if (IsAlreadyEntered()) ShowAlreadyEntered();
    }

    /// <summary>Rechner ist mit genau dieser Hardware bereits eingetragen: kein Abgleich, kein erneutes Senden.</summary>
    private bool IsAlreadyEntered()
    {
        string? machine = HallOfFameService.MachineHash();
        return machine != null && _data != null && SubmissionStore.IsUnchanged(machine, SubmissionStore.Signature(_data));
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

    private void ShowSupport()
    {
        if (_result == null) return;
        try
        {
            using var f = new SupportForm(ReportText.Build(_data, _result),
                "ENCY PC-Check: " + Loc.T("Leistungsindex ", "Performance index ") + _result.Index,
                () => SupportForm.CaptureForm(this));
            f.ShowDialog(this);
        }
        catch (Exception ex) { _lblStatus.Text = "Support: " + ex.Message; }
    }

    private void ShowHallOfFame()
    {
        try { using var f = new HallOfFameForm(); f.ShowDialog(this); }
        catch (Exception ex) { _lblStatus.Text = "Hall of Fame: " + ex.Message; }
    }

    private async Task SubmitAsync()
    {
        if (_result == null || _data == null) return;
        if (IsAlreadyEntered()) { ShowAlreadyEntered(); return; }
        string? machine = HallOfFameService.MachineHash();
        if (machine == null)
        {
            _lblStatus.Text = Loc.T("Der Rechner lässt sich nicht eindeutig erkennen. Eintrag nicht möglich.", "This computer cannot be identified uniquely. Entry not possible.");
            return;
        }
        if (SubmissionStore.EnteredToday(machine))
        {
            string m = Loc.T("Für diesen Rechner wurde heute bereits ein Eintrag gesendet. Ein neuer Eintrag ist morgen wieder möglich.",
                             "An entry for this computer has already been sent today. A new entry is possible again tomorrow.");
            _lblStatus.Text = m;
            InfoDialog.Show(this, "Hall of Fame", Loc.T("Nicht möglich", "Not possible"), m);
            return;
        }
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
                Email = dlg.Email.ToLowerInvariant(),
                MachineHash = machine,
            };
            // Position vor dem Eintrag ermitteln: Die Gesamtzahl enthaelt dann den eigenen Eintrag noch nicht
            // (RankAsync zaehlt ihn dazu), egal ob der Server neue Eintraege sofort freigibt.
            bool replacing = SubmissionStore.HasRecord(machine);
            var rank = await HallOfFameService.RankAsync(_result.Index, CancellationToken.None, replacing);
            var saved = await HallOfFameService.SubmitAsync(s, CancellationToken.None);
            if (saved != SubmitResult.DuplicateEmail) SubmissionStore.Save(machine, SubmissionStore.Signature(_data));
            string pos = Loc.T($"Ihre Position: Platz {rank.Rank} von {rank.Total} (Score {_result.Index}).",
                               $"Your position: rank {rank.Rank} of {rank.Total} (score {_result.Index}).");
            string msg = saved == SubmitResult.Saved
                ? pos + "\n\n" + (replacing ? Loc.T("Ihr bisheriger Eintrag wurde durch die neuen Werte ersetzt.", "Your previous entry has been replaced by the new values.")
                                   : Loc.T("Ihr Eintrag wurde gespeichert und ist in der Hall of Fame sichtbar.", "Your entry has been saved and is visible in the Hall of Fame."))
                : saved == SubmitResult.DuplicateEmail
                ? Loc.T("Nicht gespeichert: Mit dieser E-Mail-Adresse ist bereits ein Rechner eingetragen. Je E-Mail-Adresse ist nur ein Rechner möglich.",
                        "Not saved: A computer is already entered with this e-mail address. Only one computer per e-mail address is possible.")
                : Loc.T("Nicht gespeichert: Für diesen Rechner wurde heute bereits ein Eintrag gespeichert. Ein neuer Eintrag ist morgen wieder möglich.",
                        "Not saved: An entry for this computer has already been saved today. A new entry is possible again tomorrow.");
            if (!IsDisposed)
            {
                // Position nur bei gespeichertem Eintrag: bei Ablehnung wurde der neue Score nicht eingetragen.
                if (saved == SubmitResult.Saved) _lblStatus.Text = pos;
                string head = saved == SubmitResult.Saved
                    ? Loc.T($"Platz {rank.Rank} von {rank.Total}", $"Rank {rank.Rank} of {rank.Total}")
                    : Loc.T("Nicht gespeichert", "Not saved");
                InfoDialog.Show(this, "Hall of Fame", head, msg);
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed) { _lblStatus.Text = Loc.T("Senden fehlgeschlagen: ", "Sending failed: ") + ex.Message; _btnSubmit.Enabled = true; }
        }
    }
}
