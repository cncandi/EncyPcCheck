using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncyPcCheck;

/// <summary>
/// Merkt sich lokal (pro Windows-Benutzer), dass dieser Rechner bereits in die Hall of Fame eingetragen wurde,
/// und mit welcher Hardware. So wird die Vergleichsfrage nicht bei jedem Start gestellt.
/// </summary>
internal static class SubmissionStore
{
    private sealed class Rec
    {
        public string Machine { get; set; } = "";
        public string Signature { get; set; } = "";
        public DateTime At { get; set; }
    }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EncyPcCheck", "submitted.json");

    /// <summary>Kennung der Hardware: Prozessor, Threads, RAM, Grafik, VRAM und Laufwerksart. Nur eine Aenderung daran gilt als neue Konfiguration.</summary>
    public static string Signature(SystemData d)
    {
        string kind = d.DriveIsHdd switch { true => "HDD", false => "SSD", _ => "?" };
        string raw = string.Join("|", d.CpuName, d.LogicalCores, Math.Round(d.RamGb), d.GpuName, Math.Round(d.VramGb), kind);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    private static Rec? Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Rec>(File.ReadAllText(FilePath)) : null; }
        catch { return null; }
    }

    /// <summary>Fuer diesen Rechner (Kennung) liegt ein Eintrag vor, egal mit welcher Hardware.</summary>
    public static bool HasRecord(string machine) => Load() is { } r && r.Machine == machine;

    /// <summary>Fuer diesen Rechner wurde heute (UTC) bereits ein Eintrag gesendet.</summary>
    public static bool EnteredToday(string machine) =>
        Load() is { } r && r.Machine == machine && r.At.ToUniversalTime().Date == DateTime.UtcNow.Date;

    /// <summary>Eingetragen und die Hardware ist unveraendert.</summary>
    public static bool IsUnchanged(string machine, string signature) =>
        Load() is { } r && r.Machine == machine && r.Signature == signature;

    public static void Save(string machine, string signature)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Rec { Machine = machine, Signature = signature, At = DateTime.UtcNow }));
        }
        catch { /* ohne Merker wird die Frage erneut gestellt */ }
    }
}
