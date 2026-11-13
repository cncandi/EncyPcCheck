using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EncyPcCheck;

/// <summary>Chat-Nachricht des Kundenportals.</summary>
internal sealed record ChatMessage(string Id, string At, bool Mine, string Name, string Text, bool Deleted, List<ChatFile> Files);

internal sealed record ChatFile(string Url, string Name);

internal sealed class SupportException : Exception
{
    public int Status { get; }
    public SupportException(int status, string message) : base(message) { Status = status; }
}

/// <summary>
/// Zugang zum Kundenportal (CRM): Anmeldung, Ticket, Chat. Nutzt dieselben Schnittstellen wie das Portal.
/// Es wird nie das Passwort gespeichert, nur das refresh_token (Windows-DPAPI, aktueller Benutzer).
/// </summary>
internal sealed class SupportApi
{
    public const string BaseUrl = "https://crm.cnc-technik.cloud";
    private const string PortalDomain = "@kundenportal.cnc-technik.cloud";

    private static readonly HttpClient Http = CreateHttp();
    private static readonly string TokenFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EncyPcCheck", "support.dat");

    private string? _authUrl, _authKey, _access, _refresh;
    private DateTime _accessUntil = DateTime.MinValue;

    public bool HasSession => !string.IsNullOrEmpty(_refresh);
    public string UserName { get; private set; } = "";
    public string Company { get; private set; } = "";

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        h.DefaultRequestHeaders.Add("X-Client", "ENCY-Addon/1.0");
        return h;
    }

    // ---------------- Sitzung ----------------

    public void LoadSavedSession()
    {
        try
        {
            if (!File.Exists(TokenFile)) return;
            var raw = ProtectedData.Unprotect(File.ReadAllBytes(TokenFile), null, DataProtectionScope.CurrentUser);
            _refresh = Encoding.UTF8.GetString(raw);
        }
        catch { _refresh = null; }
    }

    private void SaveSession()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TokenFile)!);
            if (string.IsNullOrEmpty(_refresh)) { if (File.Exists(TokenFile)) File.Delete(TokenFile); return; }
            File.WriteAllBytes(TokenFile, ProtectedData.Protect(Encoding.UTF8.GetBytes(_refresh), null, DataProtectionScope.CurrentUser));
        }
        catch { /* Sitzung bleibt nur im Speicher */ }
    }

    private async Task EnsureConfigAsync(CancellationToken ct)
    {
        if (_authUrl != null) return;
        using var resp = await Http.GetAsync(BaseUrl + "/api/config", ct);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!resp.IsSuccessStatusCode) throw new SupportException((int)resp.StatusCode, Loc.T("Konfiguration nicht abrufbar.", "Configuration not available."));
        _authUrl = doc.RootElement.GetProperty("url").GetString()!.TrimEnd('/');
        _authKey = doc.RootElement.GetProperty("key").GetString()!;
    }

    /// <summary>Kundennummer (ohne @) oder E-Mail-Adresse (mit @).</summary>
    public async Task LoginAsync(string user, string password, CancellationToken ct = default)
    {
        await EnsureConfigAsync(ct);
        user = user.Trim();
        string email = user.Contains('@') ? user : user + PortalDomain;
        await TokenRequestAsync("password", new { email, password }, ct);
        SaveSession();
        await LoadMeAsync(ct);
    }

    private async Task TokenRequestAsync(string grant, object body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_authUrl}/auth/v1/token?grant_type={grant}");
        req.Headers.Add("apikey", _authKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var resp = await Http.SendAsync(req, ct);
        string text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new SupportException((int)resp.StatusCode, grant == "password"
                ? Loc.T("Anmeldung fehlgeschlagen. Bitte Zugangsdaten prüfen.", "Sign-in failed. Please check your credentials.")
                : Loc.T("Sitzung abgelaufen. Bitte erneut anmelden.", "Session expired. Please sign in again."));
        var root = JsonDocument.Parse(text).RootElement;
        _access = root.GetProperty("access_token").GetString();
        _refresh = root.GetProperty("refresh_token").GetString();
        int expires = root.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out int s) ? s : 3600;
        _accessUntil = DateTime.UtcNow.AddSeconds(expires - 60); // vor Ablauf verlaengern
        SaveSession();
    }

    private async Task EnsureAccessAsync(CancellationToken ct)
    {
        if (_access != null && DateTime.UtcNow < _accessUntil) return;
        if (string.IsNullOrEmpty(_refresh)) throw new SupportException(401, Loc.T("Nicht angemeldet.", "Not signed in."));
        await EnsureConfigAsync(ct);
        await TokenRequestAsync("refresh_token", new { refresh_token = _refresh }, ct);
    }

    /// <summary>Stellt eine gespeicherte Sitzung wieder her (true bei Erfolg).</summary>
    public async Task<bool> RestoreAsync(CancellationToken ct = default)
    {
        if (!HasSession) return false;
        try { await EnsureAccessAsync(ct); await LoadMeAsync(ct); return true; }
        catch (SupportException) { _refresh = null; _access = null; SaveSession(); return false; }
        catch (HttpRequestException) { return false; }
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (_access != null && _authUrl != null)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _authUrl + "/auth/v1/logout");
                req.Headers.Add("apikey", _authKey);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access);
                using var _ = await Http.SendAsync(req);
            }
        }
        catch { /* lokal trotzdem abmelden */ }
        _access = _refresh = null; UserName = Company = "";
        SaveSession();
    }

    private async Task LoadMeAsync(CancellationToken ct)
    {
        var me = await PortalAsync("me", new { }, ct);
        try
        {
            if (me.TryGetProperty("user", out var u))
                UserName = u.ValueKind == JsonValueKind.Object
                    ? (u.TryGetProperty("name", out var n) ? n.GetString() ?? "" : u.TryGetProperty("email", out var m) ? m.GetString() ?? "" : "")
                    : u.GetString() ?? "";
            if (me.TryGetProperty("company", out var c))
                Company = c.ValueKind == JsonValueKind.Object && c.TryGetProperty("name", out var cn) ? cn.GetString() ?? "" : c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
        }
        catch { /* Anzeige ist optional */ }
    }

    // ---------------- Portalaufrufe ----------------

    public async Task<JsonElement> PortalAsync(string endpoint, object body, CancellationToken ct = default)
    {
        for (int attempt = 0; ; attempt++)
        {
            await EnsureAccessAsync(ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/admin/portal/{endpoint}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access);
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req, ct);
            if (resp.StatusCode == (HttpStatusCode)429 && attempt < 3) { await Task.Delay(2000 * (attempt + 1), ct); continue; }
            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt < 1) { _accessUntil = DateTime.MinValue; continue; }
            return await ParseAsync(resp, ct);
        }
    }

    private static async Task<JsonElement> ParseAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        string text = await resp.Content.ReadAsStringAsync(ct);
        JsonElement root = default;
        try { root = JsonDocument.Parse(text).RootElement.Clone(); } catch { }
        if (resp.IsSuccessStatusCode) return root;
        string msg = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        int code = (int)resp.StatusCode;
        if (msg.Length == 0)
            msg = code switch
            {
                401 => Loc.T("Sitzung abgelaufen. Bitte erneut anmelden.", "Session expired. Please sign in again."),
                403 => Loc.T("Bereich nicht freigeschaltet oder kein Wartungsvertrag.", "Area not enabled or no maintenance contract."),
                429 => Loc.T("Zu viele Anfragen. Bitte kurz warten.", "Too many requests. Please wait a moment."),
                _ => Loc.T("Fehler ", "Error ") + code,
            };
        throw new SupportException(code, msg);
    }

    // ---------------- Ticket ----------------

    public async Task CreateTicketAsync(string title, string text, string priority, CancellationToken ct = default) =>
        await PortalAsync("ticket", new { title, text, priority }, ct);

    // ---------------- Chat ----------------

    public async Task<int> ChatUnreadAsync(CancellationToken ct = default)
    {
        var r = await PortalAsync("chat", new { action = "poll" }, ct);
        return r.ValueKind == JsonValueKind.Object && r.TryGetProperty("unread", out var u) && u.TryGetInt32(out int n) ? n : 0;
    }

    /// <summary>Oeffnet den Chat: ohne since die letzten 100 Nachrichten, sonst nur neue und geaenderte.</summary>
    public async Task<List<ChatMessage>> ChatOpenAsync(string? since, CancellationToken ct = default)
    {
        object body = since == null ? new { action = "poll", open = true } : new { action = "poll", open = true, since };
        return ParseMessages(await PortalAsync("chat", body, ct));
    }

    public async Task ChatSendAsync(string text, IEnumerable<ChatFile>? files = null, CancellationToken ct = default)
    {
        var list = (files ?? Enumerable.Empty<ChatFile>()).Select(f => new { url = f.Url, name = f.Name }).ToList();
        await PortalAsync("chat", new { action = "send", text, files = list }, ct);
    }

    /// <summary>Laedt eine Datei (max. 25 MB) fuer den Chat hoch; die Antwort wird unveraendert beim Senden mitgegeben.</summary>
    public async Task<ChatFile> ChatUploadAsync(string name, string mime, byte[] data, CancellationToken ct = default)
    {
        if (data.Length > 25 * 1024 * 1024) throw new SupportException(413, Loc.T("Datei größer als 25 MB. Bitte als ZIP packen.", "File larger than 25 MB. Please zip it."));
        for (int attempt = 0; ; attempt++)
        {
            await EnsureAccessAsync(ct);
            string url = $"{BaseUrl}/api/admin/portal/chat-upload?name={Uri.EscapeDataString(name)}&type={Uri.EscapeDataString(mime)}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _access);
            req.Content = new ByteArrayContent(data);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            using var resp = await Http.SendAsync(req, ct);
            if (resp.StatusCode == (HttpStatusCode)429 && attempt < 3) { await Task.Delay(2000 * (attempt + 1), ct); continue; }
            if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt < 1) { _accessUntil = DateTime.MinValue; continue; }
            var r = await ParseAsync(resp, ct);
            return new ChatFile(r.GetProperty("url").GetString() ?? "", r.TryGetProperty("name", out var n) ? n.GetString() ?? name : name);
        }
    }

    private static List<ChatMessage> ParseMessages(JsonElement root)
    {
        var res = new List<ChatMessage>();
        JsonElement arr = default;
        if (root.ValueKind == JsonValueKind.Array) arr = root;
        else if (root.ValueKind == JsonValueKind.Object)
            foreach (var key in new[] { "messages", "items", "data" })
                if (root.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.Array) { arr = a; break; }
        if (arr.ValueKind != JsonValueKind.Array) return res;
        foreach (var m in arr.EnumerateArray())
        {
            string S(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            bool B(string k) => m.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
            string id = m.TryGetProperty("id", out var idv) ? idv.ToString() : "";
            var files = new List<ChatFile>();
            if (m.TryGetProperty("files", out var fa) && fa.ValueKind == JsonValueKind.Array)
                foreach (var f in fa.EnumerateArray())
                    files.Add(new ChatFile(f.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "", f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : ""));
            res.Add(new ChatMessage(id, S("at"), B("mine"), S("name"), S("text"), B("deleted"), files));
        }
        return res;
    }
}
