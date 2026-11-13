using System.Diagnostics;
using System.Text;

namespace EncyPcCheck;

internal static class ReportText
{
    public static string Build(SystemData? data, CheckResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ENCY PC-Check " + AppInfo.Version);
        sb.AppendLine(Loc.T("Leistungsindex", "Performance index") + $": {r.Index} / 100 - {r.RatingText}");
        sb.AppendLine(r.RatingHint);
        foreach (string k in r.KnockOut) sb.AppendLine(Loc.T("Ausschluss: ", "Exclusion: ") + k);
        if (r.GpuWarning.Length > 0) sb.AppendLine(r.GpuWarning);
        sb.AppendLine();
        sb.AppendLine(Loc.T("Eignung:", "Suitability:"));
        foreach (var u in r.UseCases) sb.AppendLine($"  {u.Name}: {Scoring.RatingText(u.Rating)} ({u.Index})");
        sb.AppendLine();
        if (data != null) sb.AppendLine(Loc.T("Betriebssystem", "Operating system") + $": {data.OsName}");
        foreach (var c in r.Categories)
            sb.AppendLine($"{c.Name}: {c.Value} - {Loc.T("Wert", "Score")} {(c.Score?.ToString() ?? "-")}" + (c.Note.Length > 0 ? $" ({c.Note})" : ""));
        return sb.ToString();
    }

    /// <summary>Oeffnet das Standard-Mailprogramm mit vorbereitetem Text (ohne Empfaenger).</summary>
    public static void OpenMail(string subject, string body)
    {
        string url = "mailto:?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(body);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
}
