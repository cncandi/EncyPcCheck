using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EncyPcCheck;

internal sealed class HofEntry
{
    [JsonPropertyName("player_name")] public string PlayerName { get; set; } = "";
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("rating")] public string Rating { get; set; } = "";
    [JsonPropertyName("cpu_name")] public string? CpuName { get; set; }
    [JsonPropertyName("cpu_threads")] public int? CpuThreads { get; set; }
    [JsonPropertyName("ram_gb")] public double? RamGb { get; set; }
    [JsonPropertyName("gpu_name")] public string? GpuName { get; set; }
    [JsonPropertyName("vram_gb")] public double? VramGb { get; set; }
    [JsonPropertyName("disk_kind")] public string? DiskKind { get; set; }
    [JsonPropertyName("disk_write_mbps")] public double? DiskWriteMBps { get; set; }
    [JsonPropertyName("os_name")] public string? OsName { get; set; }
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
}

internal sealed class HofSubmission
{
    [JsonPropertyName("player_name")] public string PlayerName { get; set; } = "";
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("rating")] public string Rating { get; set; } = "";
    [JsonPropertyName("cpu_name")] public string CpuName { get; set; } = "";
    [JsonPropertyName("cpu_threads")] public int CpuThreads { get; set; }
    [JsonPropertyName("ram_gb")] public double RamGb { get; set; }
    [JsonPropertyName("gpu_name")] public string GpuName { get; set; } = "";
    [JsonPropertyName("vram_gb")] public double VramGb { get; set; }
    [JsonPropertyName("disk_kind")] public string DiskKind { get; set; } = "";
    [JsonPropertyName("disk_write_mbps")] public double? DiskWriteMBps { get; set; }
    [JsonPropertyName("cpu_single")] public double CpuSingle { get; set; }
    [JsonPropertyName("cpu_multi")] public double CpuMulti { get; set; }
    [JsonPropertyName("os_name")] public string OsName { get; set; } = "";
    [JsonPropertyName("app_version")] public string AppVersion { get; set; } = AppInfo.Version;
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("machine_hash")] public string MachineHash { get; set; } = "";
}

internal enum SubmitResult { Saved, DuplicateEmail, DuplicateToday }

internal sealed class HofRank
{
    [JsonPropertyName("rank")] public int Rank { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}

/// <summary>Zugriff auf die Hall of Fame (Supabase REST, nur Anon-Schluessel).</summary>
internal static class HallOfFameService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static bool Configured => !string.IsNullOrWhiteSpace(AppInfo.SupabaseAnonKey);

    /// <summary>Anonymisierte Rechnerkennung (SHA-256 der Windows-MachineGuid mit festem Praefix). Null, wenn nicht lesbar.</summary>
    public static string? MachineHash()
    {
        try
        {
            using var key = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            string? guid = key?.GetValue("MachineGuid") as string;
            if (string.IsNullOrWhiteSpace(guid)) return null;
            byte[] h = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("EncyPcCheck|" + guid.Trim().ToLowerInvariant()));
            return Convert.ToHexString(h).ToLowerInvariant();
        }
        catch { return null; }
    }

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, AppInfo.SupabaseUrl + "/rest/v1/" + path);
        req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppInfo.SupabaseAnonKey);
        return req;
    }

    private sealed class HofList
    {
        [JsonPropertyName("allowed")] public bool Allowed { get; set; }
        [JsonPropertyName("entries")] public List<HofEntry> Entries { get; set; } = new();
    }

    /// <summary>Einsicht nur fuer Teilnehmer: der Server liefert die Liste nur, wenn dieser Rechner freigegeben in der Hall of Fame steht.</summary>
    public static async Task<List<HofEntry>?> LoadAsync(CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Hall of Fame ist nicht eingerichtet.");
        string? machine = MachineHash();
        if (machine == null) return null;
        using var req = Request(HttpMethod.Post, "rpc/dtr_pc_list");
        req.Content = new StringContent(JsonSerializer.Serialize(new { p_machine_hash = machine }), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception(Loc.T($"Abruf fehlgeschlagen (HTTP {(int)resp.StatusCode}).", $"Request failed (HTTP {(int)resp.StatusCode})."));
        var r = JsonSerializer.Deserialize<HofList>(text);
        return r is { Allowed: true } ? r.Entries : null;
    }

    private static readonly System.Text.RegularExpressions.Regex FreeMail = new(
        @"^(gmail\.[a-z.]+|googlemail\.[a-z.]+|web\.de|gmx\.[a-z.]+|yahoo\.[a-z.]+|ymail\.com|outlook\.[a-z.]+|hotmail\.[a-z.]+|live\.[a-z.]+|msn\.com|icloud\.com|me\.com|mac\.com|aol\.[a-z.]+|t-online\.de|freenet\.de|arcor\.de|online\.de|mail\.com|proton\.me|protonmail\.[a-z.]+|pm\.me|posteo\.[a-z.]+|mailbox\.org|bluewin\.ch|yandex\.[a-z.]+|mail\.ru|qq\.com|163\.com)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>True bei privaten Maildiensten (Gmail, Web.de, Yahoo, GMX, Outlook ...): dafuer ist kein Eintrag moeglich.</summary>
    public static bool IsFreeMail(string email)
    {
        int at = email.LastIndexOf('@');
        return at < 0 || FreeMail.IsMatch(email[(at + 1)..].Trim());
    }

    /// <summary>Speichert den Eintrag ueber dtr_pc_submit: Je E-Mail-Adresse ein Rechner, je Rechner und Tag ein Eintrag. Ein frueherer Eintrag desselben Rechners (anderer Tag) wird ersetzt.</summary>
    public static async Task<SubmitResult> SubmitAsync(HofSubmission s, CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Hall of Fame ist nicht eingerichtet.");
        using var req = Request(HttpMethod.Post, "rpc/dtr_pc_submit");
        req.Content = new StringContent(JsonSerializer.Serialize(new { p_row = s }), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            string body = await resp.Content.ReadAsStringAsync(ct);
            return body.Contains("email_uq", StringComparison.OrdinalIgnoreCase) ? SubmitResult.DuplicateEmail : SubmitResult.DuplicateToday;
        }
        if (!resp.IsSuccessStatusCode) throw new Exception(Loc.T($"Senden fehlgeschlagen (HTTP {(int)resp.StatusCode}).", $"Sending failed (HTTP {(int)resp.StatusCode})."));
        return SubmitResult.Saved;
    }

    /// <summary>Position des Scores unter den freigegebenen Eintraegen (Rang und Gesamtzahl inklusive des eigenen Rechners).</summary>
    public static async Task<HofRank> RankAsync(int score, CancellationToken ct, bool replacing = false)
    {
        if (!Configured) throw new InvalidOperationException("Hall of Fame ist nicht eingerichtet.");
        using var req = Request(HttpMethod.Post, "rpc/dtr_pc_rank");
        req.Content = new StringContent(JsonSerializer.Serialize(new { p_score = score }), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception(Loc.T($"Positionsabfrage fehlgeschlagen (HTTP {(int)resp.StatusCode}).", $"Rank lookup failed (HTTP {(int)resp.StatusCode})."));
        var r = JsonSerializer.Deserialize<HofRank>(text) ?? new HofRank();
        return new HofRank { Rank = r.Rank, Total = replacing ? r.Total : r.Total + 1 };
    }
}
