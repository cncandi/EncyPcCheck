using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace EncyPcCheck;

/// <summary>
/// Misst die Auslastung des laufenden ENCY-Prozesses (diese Erweiterung laeuft im ENCY-Prozess): CPU, GPU, Arbeitsspeicher,
/// dazu die Auslastung je Prozessorkern. GPU und Kerne ueber die Windows-Leistungsindikatoren (PDH), ohne Fremdbibliothek.
/// Nicht verfuegbare Werte bleiben negativ (GPU) bzw. 0.
/// </summary>
internal sealed class ResourceMonitor : IDisposable
{
    private const uint PdhMoreData = 0x800007D2;
    private const uint FmtDouble = 0x00000200;

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string? src, IntPtr user, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr user, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint size, ref uint count, IntPtr buffer);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);

    private readonly Process _proc = Process.GetCurrentProcess();
    private TimeSpan _lastCpu;
    private DateTime _lastTime;
    private IntPtr _coreQuery, _coreCounter;
    private IntPtr _gpuQuery, _gpuCounter, _gpuNewQuery, _gpuNewCounter;
    private int _tick;
    private bool _pdhFailed;

    public int Cores { get; } = Environment.ProcessorCount;
    public double EncyCpu { get; private set; }
    public double EncyGpu { get; private set; } = -1;
    public double EncyRamGb { get; private set; }
    public double TotalRamGb { get; } = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0;
    public double[] CoreLoad { get; private set; }

    public ResourceMonitor()
    {
        CoreLoad = new double[Cores];
        _lastCpu = _proc.TotalProcessorTime;
        _lastTime = DateTime.UtcNow;
        try
        {
            if (PdhOpenQuery(null, IntPtr.Zero, out _coreQuery) == 0 &&
                PdhAddEnglishCounter(_coreQuery, @"\Processor(*)\% Processor Time", IntPtr.Zero, out _coreCounter) == 0)
                PdhCollectQueryData(_coreQuery);
            else _coreQuery = IntPtr.Zero;
        }
        catch { _pdhFailed = true; }
        StartGpuQuery(out _gpuQuery, out _gpuCounter);
    }

    /// <summary>Alle Werte neu messen (einmal pro Sekunde aufrufen).</summary>
    public void Sample()
    {
        try
        {
            _proc.Refresh();
            var now = DateTime.UtcNow;
            var cpu = _proc.TotalProcessorTime;
            double wall = (now - _lastTime).TotalSeconds;
            if (wall > 0.05)
            {
                EncyCpu = Math.Clamp((cpu - _lastCpu).TotalSeconds / wall / Cores * 100.0, 0, 100);
                _lastCpu = cpu; _lastTime = now;
            }
            EncyRamGb = _proc.WorkingSet64 / 1024.0 / 1024.0 / 1024.0;
        }
        catch { /* letzter Wert bleibt */ }
        if (_pdhFailed) return;
        try { SampleCores(); SampleGpu(); }
        catch { _pdhFailed = true; EncyGpu = -1; }
    }

    private void SampleCores()
    {
        if (_coreQuery == IntPtr.Zero || PdhCollectQueryData(_coreQuery) != 0) return;
        foreach (var (name, value) in ReadArray(_coreCounter))
            if (int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int i) && i >= 0 && i < CoreLoad.Length)
                CoreLoad[i] = Math.Clamp(value, 0, 100);
    }

    private void StartGpuQuery(out IntPtr query, out IntPtr counter)
    {
        query = counter = IntPtr.Zero;
        try
        {
            string path = $@"\GPU Engine(pid_{Environment.ProcessId}_*)\Utilization Percentage";
            if (PdhOpenQuery(null, IntPtr.Zero, out query) == 0 && PdhAddEnglishCounter(query, path, IntPtr.Zero, out counter) == 0)
            {
                PdhCollectQueryData(query);
                return;
            }
            if (query != IntPtr.Zero) PdhCloseQuery(query);
            query = counter = IntPtr.Zero;
        }
        catch { query = counter = IntPtr.Zero; }
    }

    private void SampleGpu()
    {
        _tick++;
        // Engine-Instanzen kommen und gehen: alle 5 s eine neue Abfrage vorbereiten und nach einer Sekunde tauschen.
        if (_gpuNewQuery != IntPtr.Zero)
        {
            PdhCollectQueryData(_gpuNewQuery);
            if (_gpuQuery != IntPtr.Zero) PdhCloseQuery(_gpuQuery);
            _gpuQuery = _gpuNewQuery; _gpuCounter = _gpuNewCounter;
            _gpuNewQuery = _gpuNewCounter = IntPtr.Zero;
        }
        else if (_tick % 5 == 0) StartGpuQuery(out _gpuNewQuery, out _gpuNewCounter);

        if (_gpuQuery == IntPtr.Zero) { EncyGpu = -1; return; }
        if (PdhCollectQueryData(_gpuQuery) != 0) return;
        // Wie der Task-Manager: je Engine-Typ summieren, dann den groessten Typ nehmen.
        var byType = new Dictionary<string, double>();
        bool any = false;
        foreach (var (name, value) in ReadArray(_gpuCounter))
        {
            any = true;
            int k = name.LastIndexOf("engtype_", StringComparison.Ordinal);
            string type = k >= 0 ? name[(k + 8)..] : "?";
            byType[type] = byType.GetValueOrDefault(type) + value;
        }
        EncyGpu = any && byType.Count > 0 ? Math.Clamp(byType.Values.Max(), 0, 100) : 0;
    }

    private static List<(string Name, double Value)> ReadArray(IntPtr counter)
    {
        var list = new List<(string, double)>();
        uint size = 0, count = 0;
        if (PdhGetFormattedCounterArrayW(counter, FmtDouble, ref size, ref count, IntPtr.Zero) != PdhMoreData || size == 0) return list;
        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(counter, FmtDouble, ref size, ref count, buf) != 0) return list;
            const int itemSize = 24; // PDH_FMT_COUNTERVALUE_ITEM_W (x64): Name-Zeiger, Status, Wert
            for (int i = 0; i < count; i++)
            {
                IntPtr item = buf + i * itemSize;
                if (Marshal.ReadInt32(item, 8) != 0) continue;
                string? name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(item, 0));
                double v = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, 16));
                if (name != null && !double.IsNaN(v)) list.Add((name, v));
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
        return list;
    }

    public void Dispose()
    {
        foreach (var q in new[] { _coreQuery, _gpuQuery, _gpuNewQuery })
            if (q != IntPtr.Zero) try { PdhCloseQuery(q); } catch { }
        _coreQuery = _gpuQuery = _gpuNewQuery = IntPtr.Zero;
        _proc.Dispose();
    }
}
