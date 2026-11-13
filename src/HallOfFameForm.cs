using System.Drawing;

namespace EncyPcCheck;

/// <summary>Anzeige der freigegebenen Eintraege.</summary>
internal sealed class HallOfFameForm : Form
{
    private readonly ListView _list = new() { View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(8, 2, 8, 0) };
    private readonly CancellationTokenSource _cts = new();

    public HallOfFameForm()
    {
        Text = Loc.T("Hall of Fame - ENCY PC-Check", "Hall of Fame - ENCY PC-Check");
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(900, 480);
        Font = new Font("Segoe UI", 9);
        TopMost = true;
        _list.Columns.Add(Loc.T("Platz", "Rank"), 50, HorizontalAlignment.Right);
        _list.Columns.Add("Name", 150);
        _list.Columns.Add("Score", 55, HorizontalAlignment.Right);
        _list.Columns.Add(Loc.T("Bewertung", "Rating"), 130);
        _list.Columns.Add(Loc.T("Prozessor", "Processor"), 210);
        _list.Columns.Add("RAM", 60, HorizontalAlignment.Right);
        _list.Columns.Add(Loc.T("Grafik", "Graphics"), 170);
        _list.Columns.Add(Loc.T("Datum", "Date"), 80);
        Controls.Add(_list);
        Controls.Add(_status);
        Shown += async (_, _) => await LoadAsync();
        FormClosing += (_, _) => _cts.Cancel();
    }

    private async Task LoadAsync()
    {
        _status.Text = Loc.T("Daten werden geladen ...", "Loading data ...");
        try
        {
            var rows = await HallOfFameService.LoadAsync(_cts.Token);
            if (IsDisposed) return;
            int rank = 1;
            foreach (var e in rows)
            {
                var it = new ListViewItem((rank++).ToString());
                it.SubItems.Add(e.PlayerName);
                it.SubItems.Add(e.Score.ToString());
                it.SubItems.Add(Scoring.RatingTextFromCode(e.Rating));
                it.SubItems.Add(e.CpuName ?? "");
                it.SubItems.Add(e.RamGb is double r ? $"{r:F0} GB" : "");
                it.SubItems.Add(e.GpuName ?? "");
                it.SubItems.Add(e.CreatedAt.ToLocalTime().ToString(Loc.German ? "dd.MM.yy" : "yyyy-MM-dd"));
                _list.Items.Add(it);
            }
            _status.Text = rows.Count == 0 ? Loc.T("Noch keine freigegebenen Einträge.", "No approved entries yet.") : Loc.T($"{rows.Count} Einträge.", $"{rows.Count} entries.");
        }
        catch (Exception ex)
        {
            if (!IsDisposed) _status.Text = Loc.T("Fehler: ", "Error: ") + ex.Message;
        }
    }
}
