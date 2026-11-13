using System.Globalization;

namespace EncyPcCheck;

/// <summary>Sprache der Oberflaeche: Voreinstellung nach Systemsprache (Deutsch, sonst Englisch).</summary>
internal static class Loc
{
    public static bool German { get; set; } =
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase);

    public static string T(string de, string en) => German ? de : en;
}
