using System.Drawing;

namespace EncyPcCheck;

/// <summary>Namenseingabe und Einwilligung vor dem Senden.</summary>
internal sealed class SubmitForm : Form
{
    private readonly TextBox _name = new() { MaxLength = 30, Dock = DockStyle.Top };
    private readonly TextBox _email = new() { MaxLength = 120, Dock = DockStyle.Top };
    private readonly Label _mailHint = new()
    {
        Dock = DockStyle.Top, Height = 34, ForeColor = Color.Firebrick, Visible = false,
        Text = Loc.T("Private Maildienste (z. B. Gmail, Web.de, Yahoo, GMX, Outlook) sind nicht zulässig. Bitte eine Firmenadresse verwenden.",
                     "Private mail services (e.g. Gmail, Web.de, Yahoo, GMX, Outlook) are not accepted. Please use a company address."),
    };
    private readonly CheckBox _consent = new()
    {
        Dock = DockStyle.Top,
        Height = 110,
        Text = Loc.T(
            "Ich bin einverstanden, dass mein Name, der Score und die Hardwaredaten (Prozessor, Arbeitsspeicher, Grafik, " +
            "Datenträgertyp, Messwerte) sowie meine E-Mail-Adresse und eine anonymisierte Rechnerkennung an Datentechnik Reitz " +
            "übertragen werden. Öffentlich angezeigt werden nach Freigabe nur Name, Score und Hardwaredaten, nicht die " +
            "E-Mail-Adresse. Je E-Mail-Adresse ist ein Rechner möglich. Löschung auf Anfrage: reitz@cnc-technik.de.",
            "I agree that my name, score and hardware data (processor, memory, graphics, drive type, measured values) " +
            "as well as my e-mail address and an anonymised computer ID are sent to Datentechnik Reitz. Once approved, only " +
            "name, score and hardware data are shown publicly, not the e-mail address. One computer per e-mail address. " +
            "Deletion on request: reitz@cnc-technik.de."),
        CheckAlign = ContentAlignment.TopLeft,
        TextAlign = ContentAlignment.TopLeft,
    };
    private readonly Button _ok = new() { Text = Loc.T("Senden", "Send"), DialogResult = DialogResult.OK, Enabled = false, AutoSize = true };
    private readonly Button _cancel = new() { Text = Loc.T("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };

    public string PlayerName => _name.Text.Trim();
    public string Email => _email.Text.Trim();

    public SubmitForm()
    {
        Text = Loc.T("Vergleich und Hall of Fame", "Comparison and Hall of Fame");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(480, 330);
        Font = new Font("Segoe UI", 9);
        TopMost = true;

        var lbl = new Label { Text = Loc.T("Name oder Firma (2 bis 30 Zeichen):", "Name or company (2 to 30 characters):"), Dock = DockStyle.Top, Height = 22, Padding = new Padding(0, 4, 0, 0) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_ok);
        Padding = new Padding(12);
        Controls.Add(buttons);
        Controls.Add(_consent);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        Controls.Add(_mailHint);
        Controls.Add(_email);
        Controls.Add(new Label { Text = Loc.T("Firmen-E-Mail-Adresse (wird nicht veröffentlicht):", "Company e-mail address (not published):"), Dock = DockStyle.Top, Height = 22, Padding = new Padding(0, 6, 0, 0) });
        Controls.Add(_name);
        Controls.Add(lbl);
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
