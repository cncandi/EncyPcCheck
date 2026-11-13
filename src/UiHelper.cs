using System.Drawing;

namespace EncyPcCheck;

/// <summary>Gemeinsame Hilfen fuer die festen, DPI-skalierten Dialoge.</summary>
internal static class UiHelper
{
    public static readonly Color Accent = Color.FromArgb(40, 120, 190);
    public static readonly Color BarBack = Color.FromArgb(246, 247, 249);

    public static int S(this Control c, int v) => (int)Math.Round(v * c.DeviceDpi / 96.0);

    /// <summary>Hoehe eines umbrechenden Textes bei gegebener Breite (mit Reserve gegen Abschneiden).</summary>
    public static int TextHeight(Control c, string text, Font font, int width)
    {
        var sz = TextRenderer.MeasureText(text, font, new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
        return sz.Height + c.S(6);
    }

    public static Button MakeButton(Control owner, string text, DialogResult result) => new()
    {
        Text = text, DialogResult = result, UseVisualStyleBackColor = true,
        Size = new Size(owner.S(112), owner.S(34)),
    };
}
