using System.Drawing;

namespace EncyPcCheck;

/// <summary>Einheitlich gestaltete Frage-/Hinweisfenster (ersetzt MessageBox).</summary>
internal sealed class InfoDialog : Form
{
    private InfoDialog(string title, string headline, string message, string yes, string? no)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.White;
        TopMost = true;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var body = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(24, 20, 24, 12) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 432));
        body.Controls.Add(new Label { Text = headline, AutoSize = true, MaximumSize = new Size(432, 0), Font = new Font("Segoe UI Semibold", 12), ForeColor = Color.FromArgb(40, 120, 190), Margin = new Padding(0, 0, 0, 8) });
        body.Controls.Add(new Label { Text = message, AutoSize = true, MaximumSize = new Size(432, 0), ForeColor = Color.FromArgb(50, 50, 50) });

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(24, 12, 24, 16), BackColor = Color.FromArgb(246, 247, 249) };
        var bYes = new Button { Text = yes, DialogResult = DialogResult.Yes, AutoSize = true, MinimumSize = new Size(110, 32), Margin = new Padding(8, 0, 0, 0), TabIndex = 0 };
        buttons.Controls.Add(bYes);
        AcceptButton = bYes;
        if (no != null)
        {
            var bNo = new Button { Text = no, DialogResult = DialogResult.No, AutoSize = true, MinimumSize = new Size(110, 32), Margin = new Padding(8, 0, 0, 0), TabIndex = 1 };
            buttons.Controls.Add(bNo);
            CancelButton = bNo;
        }
        else CancelButton = bYes;
        Controls.Add(body);
        Controls.Add(buttons);
        ClientSize = new Size(480, ClientSize.Height);
        ActiveControl = bYes;
    }

    public static bool Ask(IWin32Window owner, string title, string headline, string message, string yes, string no)
    {
        using var d = new InfoDialog(title, headline, message, yes, no);
        return d.ShowDialog(owner) == DialogResult.Yes;
    }

    public static void Show(IWin32Window owner, string title, string headline, string message)
    {
        using var d = new InfoDialog(title, headline, message, "OK", null);
        d.ShowDialog(owner);
    }
}
