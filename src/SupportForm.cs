using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace EncyPcCheck;

/// <summary>Anmeldung am Kundenportal, Ticket mit PC-Bericht und Chat mit dem Support (Datentechnik Reitz).</summary>
internal sealed class SupportForm : Form
{
    private const int PollIdleMs = 30_000, PollChatMs = 5_000;

    private readonly SupportApi _api = new();
    private readonly string _report;
    private readonly string _ticketTitle;
    private readonly Func<byte[]?> _screenshot;

    // Anmeldung
    private readonly Panel _loginPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(24) };
    private readonly TextBox _user = new() { Width = 320 };
    private readonly TextBox _pass = new() { Width = 320, UseSystemPasswordChar = true };
    private readonly Button _btnLogin = new EncyButton { AutoSize = true };
    private readonly Label _loginInfo = new() { AutoSize = false, Width = 520, Height = 54 };
    private readonly Label _loginErr = new() { AutoSize = false, Width = 520, Height = 40, ForeColor = Theme.Err };
    private readonly Label _lblUser = new() { AutoSize = true };
    private readonly Label _lblPass = new() { AutoSize = true };

    // Hauptbereich
    private readonly Panel _mainPanel = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly SimpleTabs _tabs = new() { Dock = DockStyle.Fill };
    private readonly Panel _tabTicket = new();
    private readonly Panel _tabChat = new();
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(8, 3, 8, 0) };
    private readonly Button _btnLogout = new EncyButton { AutoSize = true, Dock = DockStyle.Right };

    // Ticket
    private readonly TextBox _title = new() { Dock = DockStyle.Top, MaxLength = 200 };
    private readonly TextBox _desc = new() { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
    private readonly CheckBox _withReport = new() { Dock = DockStyle.Bottom, Checked = true, Height = 26 };
    private readonly Button _btnTicket = new EncyButton { AutoSize = true };
    private readonly Label _lblTitle = new() { Dock = DockStyle.Top, Height = 20 };
    private readonly Label _lblDesc = new() { Dock = DockStyle.Top, Height = 20 };

    // Chat
    private readonly RichTextBox _log = new() { Dock = DockStyle.Fill, ReadOnly = true, DetectUrls = true, BorderStyle = BorderStyle.None };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = false };
    private readonly Button _btnSend = new EncyButton { AutoSize = true };
    private readonly Button _btnShot = new EncyButton { AutoSize = true };

    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly List<ChatMessage> _messages = new();
    private string? _since;
    private bool _busy, _chatLoaded;
    private int _unread;

    public SupportForm(string reportText, string ticketTitle, Func<byte[]?> screenshot)
    {
        _report = reportText; _ticketTitle = ticketTitle; _screenshot = screenshot;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 600);
        MinimumSize = new Size(640, 520);
        Font = new Font("Segoe UI", 9.5f);
        Theme.Init(this);
        TopMost = true;
        _log.BackColor = Theme.Surface2; _log.ForeColor = Theme.Ink; Theme.DarkScroll(_log);
        Theme.StyleText(_user); Theme.StyleText(_pass); Theme.StyleText(_title); Theme.StyleText(_desc); Theme.StyleText(_input);
        ((EncyButton)_btnLogin).Primary = true; ((EncyButton)_btnTicket).Primary = true; ((EncyButton)_btnSend).Primary = true;
        _status.ForeColor = Theme.Ink2; _withReport.ForeColor = Theme.Ink;
        foreach (var panel in new Control[] { _loginPanel, _mainPanel }) panel.BackColor = Theme.Bg;

        // Anmeldung
        _lblUser.Location = new Point(24, 90); _user.Location = new Point(24, 112);
        _lblPass.Location = new Point(24, 148); _pass.Location = new Point(24, 170);
        _btnLogin.Location = new Point(24, 212); _btnLogin.MinimumSize = new Size(110, 32);
        _loginInfo.Location = new Point(24, 16);
        _loginErr.Location = new Point(24, 256);
        _loginPanel.Controls.AddRange(new Control[] { _loginInfo, _lblUser, _user, _lblPass, _pass, _btnLogin, _loginErr });
        AcceptButton = _btnLogin;

        // Ticket
        var tp = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
        bottom.Controls.Add(_btnTicket);
        tp.Controls.Add(_desc);
        tp.Controls.Add(_lblDesc);
        tp.Controls.Add(_title);
        tp.Controls.Add(_lblTitle);
        tp.Controls.Add(_withReport);
        tp.Controls.Add(bottom);
        _tabTicket.Controls.Add(tp);

        // Chat
        var cp = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        var inputRow = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(0, 8, 0, 0) };
        var btnCol = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 150, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _btnSend.MinimumSize = new Size(140, 26); _btnShot.MinimumSize = new Size(140, 26);
        btnCol.Controls.Add(_btnSend); btnCol.Controls.Add(_btnShot);
        inputRow.Controls.Add(_input);
        inputRow.Controls.Add(btnCol);
        cp.Controls.Add(_log);
        cp.Controls.Add(inputRow);
        _tabChat.Controls.Add(cp);

        _tabs.AddPage(_tabTicket, "Ticket");
        _tabs.AddPage(_tabChat, "Chat");
        var foot = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        foot.Controls.Add(_status);
        foot.Controls.Add(_btnLogout);
        _mainPanel.Controls.Add(_tabs);
        _mainPanel.Controls.Add(foot);

        Controls.Add(_mainPanel);
        Controls.Add(_loginPanel);

        _btnLogin.Click += async (_, _) => await LoginAsync();
        _btnLogout.Click += async (_, _) => await LogoutAsync();
        _btnTicket.Click += async (_, _) => await SendTicketAsync();
        _btnSend.Click += async (_, _) => await SendChatAsync(null);
        _btnShot.Click += async (_, _) => await SendScreenshotAsync();
        _input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; await SendChatAsync(null); }
        };
        _log.LinkClicked += (_, e) => { try { if (e.LinkText != null) ReportText.OpenUrl(e.LinkText); } catch { } };
        _tabs.SelectedIndexChanged += async (_, _) => { ApplyPollInterval(); if (_tabs.SelectedTab == _tabChat) await PollAsync(); };
        _timer.Tick += async (_, _) => await PollAsync();
        Shown += async (_, _) => await TryRestoreAsync();
        FormClosing += (_, _) => _timer.Stop();

        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Loc.T("Support - Datentechnik Reitz", "Support - Datentechnik Reitz");
        _loginInfo.Text = Loc.T(
            "Melden Sie sich mit den Zugangsdaten Ihres Kundenportals an (Kundennummer oder E-Mail-Adresse). Das Passwort wird nicht gespeichert.",
            "Sign in with your customer portal credentials (customer number or e-mail address). The password is not stored.");
        _lblUser.Text = Loc.T("Kundennummer oder E-Mail", "Customer number or e-mail");
        _lblPass.Text = Loc.T("Passwort", "Password");
        _btnLogin.Text = Loc.T("Anmelden", "Sign in");
        _btnLogout.Text = Loc.T("Abmelden", "Sign out");
        _tabs.SetText(_tabTicket, Loc.T("Ticket", "Ticket"));
        UpdateChatTitle();
        _lblTitle.Text = Loc.T("Betreff", "Subject");
        _lblDesc.Text = Loc.T("Beschreibung", "Description");
        _withReport.Text = Loc.T("PC-Check-Bericht anhängen", "Attach PC-Check report");
        _btnTicket.Text = Loc.T("Ticket senden", "Send ticket");
        _btnSend.Text = Loc.T("Senden", "Send");
        _btnShot.Text = Loc.T("Screenshot senden", "Send screenshot");
        if (_title.Text.Length == 0) _title.Text = _ticketTitle;
    }

    private void UpdateChatTitle() =>
        _tabs.SetText(_tabChat, "Chat" + (_unread > 0 ? $" ({_unread})" : ""));

    // ---------------- Sitzung ----------------

    private async Task TryRestoreAsync()
    {
        _api.LoadSavedSession();
        if (!_api.HasSession) { _user.Focus(); return; }
        SetBusy(true, Loc.T("Anmeldung wird erneuert ...", "Restoring session ..."));
        bool ok = await _api.RestoreAsync();
        SetBusy(false, "");
        if (ok) ShowMain(); else _user.Focus();
    }

    private async Task LoginAsync()
    {
        if (_user.Text.Trim().Length == 0 || _pass.Text.Length == 0) return;
        _loginErr.Text = "";
        SetBusy(true, "");
        try
        {
            await _api.LoginAsync(_user.Text, _pass.Text);
            _pass.Clear();
            ShowMain();
        }
        catch (SupportException e) { _loginErr.Text = e.Message; }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        { _loginErr.Text = Loc.T("Server nicht erreichbar.", "Server not reachable."); }
        finally { SetBusy(false, ""); }
    }

    private async Task LogoutAsync()
    {
        _timer.Stop();
        await _api.LogoutAsync();
        _messages.Clear(); _log.Clear(); _since = null; _chatLoaded = false; _unread = 0; UpdateChatTitle();
        _mainPanel.Visible = false; _loginPanel.Visible = true;
        AcceptButton = _btnLogin;
        _user.Focus();
    }

    private void ShowMain()
    {
        _loginPanel.Visible = false; _mainPanel.Visible = true;
        AcceptButton = null;
        string who = _api.UserName;
        if (_api.Company.Length > 0) who += (who.Length > 0 ? " - " : "") + _api.Company;
        _status.Text = Loc.T("Angemeldet: ", "Signed in: ") + who;
        ApplyPollInterval();
        _timer.Start();
    }

    private void SetBusy(bool busy, string text)
    {
        _busy = busy;
        UseWaitCursor = busy;
        if (text.Length > 0) _status.Text = text;
        _btnLogin.Enabled = !busy;
    }

    // ---------------- Ticket ----------------

    private async Task SendTicketAsync()
    {
        string title = _title.Text.Trim(), text = _desc.Text.Trim();
        if (title.Length == 0 || text.Length == 0)
        {
            MessageBox.Show(this, Loc.T("Bitte Betreff und Beschreibung ausfüllen.", "Please fill in subject and description."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_withReport.Checked) text += "\r\n\r\n" + _report;
        _btnTicket.Enabled = false;
        try
        {
            await _api.CreateTicketAsync(title, text, "normal");
            _desc.Clear();
            MessageBox.Show(this, Loc.T("Das Ticket wurde angelegt.", "The ticket has been created."), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception e) { ShowError(e); }
        finally { _btnTicket.Enabled = true; }
    }

    // ---------------- Chat ----------------

    private void ApplyPollInterval() => _timer.Interval = _tabs.SelectedTab == _tabChat ? PollChatMs : PollIdleMs;

    /// <summary>Abfrage im Takt; bei minimiertem Fenster ausgesetzt, damit der Kunde nicht dauerhaft "online" bleibt.</summary>
    private async Task PollAsync()
    {
        if (_busy || !_mainPanel.Visible || WindowState == FormWindowState.Minimized) return;
        _busy = true;
        try
        {
            if (_tabs.SelectedTab == _tabChat)
            {
                var list = await _api.ChatOpenAsync(_chatLoaded ? _since : null);
                MergeMessages(list, !_chatLoaded);
                _chatLoaded = true;
                _unread = 0;
            }
            else
            {
                _unread = await _api.ChatUnreadAsync();
            }
            UpdateChatTitle();
        }
        catch (SupportException e) when (e.Status == 401) { await LogoutAsync(); _loginErr.Text = e.Message; }
        catch { /* naechster Takt */ }
        finally { _busy = false; }
    }

    private void MergeMessages(List<ChatMessage> incoming, bool replace)
    {
        if (replace) _messages.Clear();
        if (incoming.Count == 0 && !replace) return;
        foreach (var m in incoming)
        {
            int i = _messages.FindIndex(x => x.Id == m.Id);
            if (i >= 0) _messages[i] = m; else _messages.Add(m);
        }
        _messages.Sort((a, b) => string.CompareOrdinal(a.At, b.At));
        if (_messages.Count > 0) _since = _messages[^1].At;
        RenderLog();
    }

    private void RenderLog()
    {
        _log.SuspendLayout();
        _log.Clear();
        foreach (var m in _messages)
        {
            string time = DateTime.TryParse(m.At, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t) ? t.ToLocalTime().ToString("g") : m.At;
            string who = m.Mine ? Loc.T("Sie", "You") : (m.Name.Length > 0 ? m.Name : "Support");
            _log.SelectionColor = m.Mine ? UiHelper.Accent : Theme.Ink2;
            _log.SelectionFont = new Font(Font, FontStyle.Bold);
            _log.AppendText($"{who}  {time}\r\n");
            _log.SelectionColor = Theme.Ink;
            _log.SelectionFont = Font;
            var sb = new StringBuilder();
            sb.Append(m.Deleted ? Loc.T("(Nachricht gelöscht)", "(message deleted)") : m.Text);
            if (!m.Deleted) foreach (var f in m.Files) sb.Append("\r\n").Append(f.Name).Append(": ").Append(f.Url);
            _log.AppendText(sb + "\r\n\r\n");
        }
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
        _log.ResumeLayout();
    }

    private async Task SendChatAsync(IEnumerable<ChatFile>? files)
    {
        string text = _input.Text.Trim();
        if (text.Length == 0 && files == null) return;
        _btnSend.Enabled = false;
        try
        {
            await _api.ChatSendAsync(text, files);
            _input.Clear();
            await PollAsync();
        }
        catch (Exception e) { ShowError(e); }
        finally { _btnSend.Enabled = true; }
    }

    private async Task SendScreenshotAsync()
    {
        _btnShot.Enabled = false;
        try
        {
            byte[]? png = _screenshot();
            if (png == null) { MessageBox.Show(this, Loc.T("Kein Screenshot verfügbar.", "No screenshot available."), Text); return; }
            var file = await _api.ChatUploadAsync($"PC-Check_{DateTime.Now:yyyyMMdd_HHmmss}.png", "image/png", png);
            string typed = _input.Text.Trim();
            _input.Text = typed.Length > 0 ? typed : "Screenshot PC-Check";
            await SendChatAsync(new[] { file });
        }
        catch (Exception e) { ShowError(e); }
        finally { _btnShot.Enabled = true; }
    }

    private void ShowError(Exception e)
    {
        string msg = e is SupportException ? e.Message
            : e is HttpRequestException or TaskCanceledException ? Loc.T("Server nicht erreichbar.", "Server not reachable.")
            : e.Message;
        MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>PNG des Fensters als Rohdaten.</summary>
    public static byte[]? CaptureForm(Form f)
    {
        try
        {
            using var bmp = new Bitmap(f.Width, f.Height);
            f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch { return null; }
    }
}
