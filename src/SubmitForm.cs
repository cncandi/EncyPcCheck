using System.Drawing;

namespace EncyPcCheck;

/// <summary>Name, Firmen-E-Mail und Einwilligung vor dem Senden. TAB wechselt der Reihe nach von Feld zu Feld.</summary>
internal sealed class SubmitForm : Form
{
    private static readonly Color Accent = Color.FromArgb(40, 120, 190);

    private readonly TextBox _name = new() { MaxLength = 30, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 10), Font = new Font("Segoe UI", 10) };
    private readonly TextBox _email = new() { MaxLength = 120, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 4), Font = new Font("Segoe UI", 10) };
    private readonly Label _mailHint = new()
    {
        Dock = DockStyle.Fill, AutoSize = true, MaximumSize = new Size(448, 0), ForeColor = Color.Firebrick, Visible = false,
        Margin = new Padding(0, 0, 0, 6),
        Text = Loc.T("Private Maildienste (z. B. Gmail, Web.de, Yahoo, GMX, Outlook) sind nicht zulässig. Bitte eine Firmenadresse verwenden.",
                     "Private mail services (e.g. Gmail, Web.de, Yahoo, GMX, Outlook) are not accepted. Please use a company address."),
    };
    private readonly CheckBox _consent = new()
    {
        AutoSize = true, MaximumSize = new Size(448, 0), Margin = new Padding(0, 8, 0, 4),
        Text = Loc.T(
            "Ich bin einverstanden, dass mein Name, der Score und die Hardwaredaten (Prozessor, Arbeitsspeicher, Grafik, " +
            "Datenträgertyp, Messwerte) sowie meine E-Mail-Adresse und eine anonymisierte Rechnerkennung an Datentechnik Reitz " +
            "übertragen werden. Öffentlich angezeigt werden nach Freigabe nur Name, Score und Hardwaredaten, nicht die " +
            "E-Mail-Adresse. Je E-Mail-Adresse ist ein Rechner möglich. Löschung auf Anfrage: reitz@cnc-technik.de.",
            "I agree that my name, score and hardware data (processor, memory, graphics, drive type, measured values) " +
            "as well as my e-mail address and an anonymised computer ID are sent to Datentechnik Reitz. Once approved, only " +
            "name, score and hardware data are shown publicly, not the e-mail address. One computer per e-mail address. " +
            "Deletion on request: reitz@cnc-technik.de."),
    };
    private readonly Button _ok = new() { Text = Loc.T("Senden", "Send"), DialogResult = DialogResult.OK, Enabled = false, AutoSize = true, MinimumSize = new Size(100, 32), Margin = new Padding(8, 0, 0, 0) };
    private readonly Button _cancel = new() { Text = Loc.T("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(100, 32), Margin = new Padding(8, 0, 0, 0) };

    public string PlayerName => _name.Text.Trim();
    public string Email => _email.Text.Trim();

    public SubmitForm()
    {
        Text = Loc.T("Vergleich und Hall of Fame", "Comparison and Hall of Fame");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9);
        BackColor = Color.White;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // Kopfband
        var head = new Panel { BackColor = Accent, Dock = DockStyle.Top, Height = 76, Padding = new Padding(24, 12, 24, 8) };
        head.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(225, 238, 250),
            Text = Loc.T("Erfahren Sie Ihre Position im Vergleich zu anderen Rechnern. Dazu genügen Name und Firmen-E-Mail-Adresse.",
                         "Find out how your computer ranks against others. Name and company e-mail address are all you need."),
        });
        head.Controls.Add(new Label
        {
            Dock = DockStyle.Top, Height = 28, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 13),
            Text = Loc.T("Vergleich mit anderen Rechnern", "Compare with other computers"),
        });

        // Formular
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(24, 16, 24, 8),
            Width = 496,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 448));
        grid.Controls.Add(MakeLabel(Loc.T("Name oder Firma (2 bis 30 Zeichen)", "Name or company (2 to 30 characters)")));
        grid.Controls.Add(_name);
        grid.Controls.Add(MakeLabel(Loc.T("Firmen-E-Mail-Adresse (wird nicht veröffentlicht)", "Company e-mail address (not published)")));
        grid.Controls.Add(_email);
        grid.Controls.Add(_mailHint);
        grid.Controls.Add(_consent);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(24, 12, 24, 16), BackColor = Color.FromArgb(246, 247, 249),
        };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_ok);

        Controls.Add(grid);
        Controls.Add(buttons);
        Controls.Add(head);
        ClientSize = new Size(496, ClientSize.Height);

        // Tab-Reihenfolge: Name -> E-Mail -> Einwilligung -> Senden -> Abbrechen
        _name.TabIndex = 0; _email.TabIndex = 1; _consent.TabIndex = 2; _ok.TabIndex = 3; _cancel.TabIndex = 4;
        foreach (Control c in new Control[] { _name, _email, _consent, _ok, _cancel }) c.TabStop = true;
        grid.TabIndex = 0; buttons.TabIndex = 1; head.TabStop = false;
        ActiveControl = _name;

        AcceptButton = _ok;
        CancelButton = _cancel;
        _name.TextChanged += (_, _) => Validate_();
        _email.TextChanged += (_, _) => Validate_();
        _consent.CheckedChanged += (_, _) => Validate_();
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 0), ForeColor = Color.FromArgb(60, 60, 60),
        Font = new Font("Segoe UI Semibold", 9), TabStop = false,
    };

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
