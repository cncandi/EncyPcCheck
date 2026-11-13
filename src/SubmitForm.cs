using System.Drawing;

namespace EncyPcCheck;

/// <summary>Name, Firmen-E-Mail und Einwilligung vor dem Senden. TAB wechselt der Reihe nach von Feld zu Feld.</summary>
internal sealed class SubmitForm : Form
{
    private readonly TextBox _name = new() { MaxLength = 30 };
    private readonly TextBox _email = new() { MaxLength = 120 };
    private readonly Label _mailHint = new() { ForeColor = Color.Firebrick, Visible = false, TabStop = false };
    private readonly CheckBox _consent = new();
    private readonly Button _ok;
    private readonly Button _cancel;

    public string PlayerName => _name.Text.Trim();
    public string Email => _email.Text.Trim();

    public SubmitForm()
    {
        Text = Loc.T("Vergleich und Hall of Fame", "Comparison and Hall of Fame");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.White;
        TopMost = true;

        int w = this.S(540), pad = this.S(26), inner = w - 2 * pad;

        // Kopfband
        var head = new Panel { BackColor = UiHelper.Accent, Dock = DockStyle.Top, Height = this.S(88) };
        head.Controls.Add(new Label
        {
            Text = Loc.T("Vergleich mit anderen Rechnern", "Compare with other computers"),
            Font = new Font("Segoe UI Semibold", 13.5f), ForeColor = Color.White, BackColor = Color.Transparent,
            Location = new Point(pad, this.S(12)), Size = new Size(inner, this.S(30)), TabStop = false,
        });
        head.Controls.Add(new Label
        {
            Text = Loc.T("Erfahren Sie Ihre Position im Vergleich zu anderen Rechnern. Dazu genügen Name und Firmen-E-Mail-Adresse.",
                         "Find out how your computer ranks against others. Name and company e-mail address are all you need."),
            ForeColor = Color.FromArgb(228, 240, 250), BackColor = Color.Transparent,
            Location = new Point(pad, this.S(44)), Size = new Size(inner, this.S(38)), TabStop = false,
        });

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var fLabel = new Font("Segoe UI Semibold", 9.5f);
        int y = this.S(20);

        Label Lbl(string t) { var l = new Label { Text = t, Font = fLabel, ForeColor = Color.FromArgb(60, 60, 60), Location = new Point(pad, y), Size = new Size(inner, this.S(22)), TabStop = false }; y += this.S(24); return l; }

        body.Controls.Add(Lbl(Loc.T("Name oder Firma (2 bis 30 Zeichen)", "Name or company (2 to 30 characters)")));
        _name.Font = new Font("Segoe UI", 10.5f);
        _name.Location = new Point(pad, y); _name.Width = inner; body.Controls.Add(_name);
        y += _name.Height + this.S(16);

        body.Controls.Add(Lbl(Loc.T("Firmen-E-Mail-Adresse (wird nicht veröffentlicht)", "Company e-mail address (not published)")));
        _email.Font = new Font("Segoe UI", 10.5f);
        _email.Location = new Point(pad, y); _email.Width = inner; body.Controls.Add(_email);
        y += _email.Height + this.S(6);

        string hint = Loc.T("Private Maildienste (z. B. Gmail, Web.de, Yahoo, GMX, Outlook) sind nicht zulässig. Bitte eine Firmenadresse verwenden.",
                            "Private mail services (e.g. Gmail, Web.de, Yahoo, GMX, Outlook) are not accepted. Please use a company address.");
        int hHint = UiHelper.TextHeight(this, hint, Font, inner);
        _mailHint.Text = hint;
        _mailHint.Location = new Point(pad, y); _mailHint.Size = new Size(inner, hHint);
        body.Controls.Add(_mailHint);
        y += hHint + this.S(10);

        string consent = Loc.T(
            "Ich bin einverstanden, dass mein Name, der Score und die Hardwaredaten (Prozessor, Arbeitsspeicher, Grafik, " +
            "Datenträgertyp, Messwerte) sowie meine E-Mail-Adresse und eine anonymisierte Rechnerkennung an Datentechnik Reitz " +
            "übertragen werden. Öffentlich angezeigt werden nach Freigabe nur Name, Score und Hardwaredaten, nicht die " +
            "E-Mail-Adresse. Je E-Mail-Adresse ist ein Rechner möglich. Löschung auf Anfrage: reitz@cnc-technik.de.",
            "I agree that my name, score and hardware data (processor, memory, graphics, drive type, measured values) " +
            "as well as my e-mail address and an anonymised computer ID are sent to Datentechnik Reitz. Once approved, only " +
            "name, score and hardware data are shown publicly, not the e-mail address. One computer per e-mail address. " +
            "Deletion on request: reitz@cnc-technik.de.");
        int hCons = UiHelper.TextHeight(this, consent, Font, inner - this.S(26)) + this.S(4);
        _consent.Text = consent;
        _consent.AutoSize = false;
        _consent.CheckAlign = ContentAlignment.TopLeft; _consent.TextAlign = ContentAlignment.TopLeft;
        _consent.Location = new Point(pad, y); _consent.Size = new Size(inner, hCons);
        body.Controls.Add(_consent);
        y += hCons + this.S(24);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = this.S(66), BackColor = UiHelper.BarBack };
        _ok = UiHelper.MakeButton(this, Loc.T("Senden", "Send"), DialogResult.OK);
        _cancel = UiHelper.MakeButton(this, Loc.T("Abbrechen", "Cancel"), DialogResult.Cancel);
        _cancel.Location = new Point(w - pad - _cancel.Width, this.S(16));
        _ok.Location = new Point(_cancel.Left - this.S(10) - _ok.Width, this.S(16));
        _ok.Enabled = false;
        bar.Controls.Add(_ok); bar.Controls.Add(_cancel);

        Controls.Add(body);
        Controls.Add(bar);
        Controls.Add(head);
        ClientSize = new Size(w, head.Height + y + bar.Height);

        // Tab-Reihenfolge: Name -> E-Mail -> Einwilligung -> Senden -> Abbrechen
        _name.TabIndex = 0; _email.TabIndex = 1; _consent.TabIndex = 2; _ok.TabIndex = 3; _cancel.TabIndex = 4;
        foreach (Control c in new Control[] { _name, _email, _consent, _ok, _cancel }) c.TabStop = true;
        ActiveControl = _name;

        AcceptButton = _ok;
        CancelButton = _cancel;
        _name.TextChanged += (_, _) => Validate_();
        _email.TextChanged += (_, _) => Validate_();
        _consent.CheckedChanged += (_, _) => Validate_();
    }

    private void Validate_()
    {
        _mailHint.Visible = IsFormat(Email) && HallOfFameService.IsFreeMail(Email);
        string n = PlayerName;
        _ok.Enabled = _consent.Checked && n.Length is >= 2 and <= 30 && !n.Any(char.IsControl) && IsEmail(Email);
    }

    private static bool IsEmail(string e) =>
        IsFormat(e) && !HallOfFameService.IsFreeMail(e);

    private static bool IsFormat(string e) =>
        e.Length is >= 6 and <= 120 && System.Text.RegularExpressions.Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");
}
