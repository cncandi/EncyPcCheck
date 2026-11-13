using System.Drawing;

namespace EncyPcCheck;

/// <summary>Einheitlich gestaltete Frage-/Hinweisfenster (ersetzt MessageBox). Feste Breite, berechnete Hoehe.</summary>
internal sealed class InfoDialog : Form
{
    private InfoDialog(string title, string headline, string message, string yes, string? no)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Segoe UI", 9.5f);
        Theme.Init(this);
        TopMost = true;

        int w = this.S(520), pad = this.S(26), inner = w - 2 * pad;
        var fHead = new Font("Segoe UI Semibold", 12.5f);
        int hHead = UiHelper.TextHeight(this, headline, fHead, inner);
        int hMsg = UiHelper.TextHeight(this, message, Font, inner);

        var bar = new Panel { Dock = DockStyle.Bottom, Height = this.S(66), BackColor = UiHelper.BarBack };
        var bYes = UiHelper.MakeButton(this, yes, DialogResult.Yes, primary: true);
        bYes.Location = new Point(w - pad - bYes.Width, this.S(16));
        bYes.TabIndex = 0;
        bar.Controls.Add(bYes);
        AcceptButton = bYes;
        if (no != null)
        {
            var bNo = UiHelper.MakeButton(this, no, DialogResult.No);
            bNo.Location = new Point(bYes.Left - this.S(10) - bNo.Width, this.S(16));
            bNo.TabIndex = 1;
            bar.Controls.Add(bNo);
            CancelButton = bNo;
        }
        else CancelButton = bYes;

        var lblHead = new Label { Text = headline, Font = fHead, ForeColor = UiHelper.Accent, Location = new Point(pad, this.S(24)), Size = new Size(inner, hHead), TabStop = false };
        var lblMsg = new Label { Text = message, Location = new Point(pad, lblHead.Bottom + this.S(10)), Size = new Size(inner, hMsg), ForeColor = Theme.Ink, TabStop = false };

        Controls.Add(lblHead);
        Controls.Add(lblMsg);
        Controls.Add(bar);
        ClientSize = new Size(w, lblMsg.Bottom + this.S(26) + bar.Height);
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
