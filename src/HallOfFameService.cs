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
}

/// <summary>Zugriff auf die Hall of Fame (Supabase REST, nur Anon-Schluessel).</summary>
internal static class HallOfFameService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static bool Configured => !string.IsNullOrWhiteSpace(AppInfo.SupabaseAnonKey);

    private static HttpRequestMessage Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, AppInfo.SupabaseUrl + "/rest/v1/" + path);
        req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppInfo.SupabaseAnonKey);
        return req;
    }

    public static async Task<List<HofEntry>> LoadAsync(CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Hall of Fame ist nicht eingerichtet.");
        const string q = "dtr_pc_scores?select=player_name,score,rating,cpu_name,cpu_threads,ram_gb,gpu_name,vram_gb,disk_kind,created_at" +
                         "&approved=eq.true&order=score.desc,created_at.asc&limit=100";
        using var req = Request(HttpMethod.Get, q);
        using var resp = await Http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"Abruf fehlgeschlagen (HTTP {(int)resp.StatusCode}).");
        return JsonSerializer.Deserialize<List<HofEntry>>(text) ?? new List<HofEntry>();
    }

    public static async Task SubmitAsync(HofSubmission s, CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Hall of Fame ist nicht eingerichtet.");
        using var req = Request(HttpMethod.Post, "dtr_pc_scores");
        req.Headers.Add("Prefer", "return=minimal");
        req.Content = new StringContent(JsonSerializer.Serialize(s), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"Senden fehlgeschlagen (HTTP {(int)resp.StatusCode}).");
    }
}
