using System.Drawing;

namespace EncyPcCheck;

/// <summary>Namenseingabe und Einwilligung vor dem Senden.</summary>
internal sealed class SubmitForm : Form
{
    private readonly TextBox _name = new() { MaxLength = 30, Dock = DockStyle.Top };
    private readonly CheckBox _consent = new()
    {
        Dock = DockStyle.Top,
        Height = 90,
        Text = Loc.T(
            "Ich bin einverstanden, dass mein Name, der Score und die Hardwaredaten (Prozessor, Arbeitsspeicher, Grafik, " +
            "Datenträgertyp, Messwerte) an Datentechnik Reitz übertragen und nach Freigabe öffentlich in der Hall of Fame " +
            "angezeigt werden. Eine Löschung kann per E-Mail an reitz@cnc-technik.de verlangt werden.",
            "I agree that my name, score and hardware data (processor, memory, graphics, drive type, measured values) " +
            "are sent to Datentechnik Reitz and, once approved, shown publicly in the Hall of Fame. " +
            "Deletion can be requested by e-mail to reitz@cnc-technik.de."),
        CheckAlign = ContentAlignment.TopLeft,
        TextAlign = ContentAlignment.TopLeft,
    };
    private readonly Button _ok = new() { Text = Loc.T("Senden", "Send"), DialogResult = DialogResult.OK, Enabled = false, AutoSize = true };
    private readonly Button _cancel = new() { Text = Loc.T("Abbrechen", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };

    public string PlayerName => _name.Text.Trim();

    public SubmitForm()
    {
        Text = Loc.T("In die Hall of Fame eintragen", "Enter the Hall of Fame");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ClientSize = new Size(480, 210);
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
        Controls.Add(_name);
        Controls.Add(lbl);
        AcceptButton = _ok;
        CancelButton = _cancel;
        _name.TextChanged += (_, _) => Validate_();
        _consent.CheckedChanged += (_, _) => Validate_();
    }

    private void Validate_()
    {
        string n = PlayerName;
        _ok.Enabled = _consent.Checked && n.Length is >= 2 and <= 30 && !n.Any(char.IsControl);
    }
}
