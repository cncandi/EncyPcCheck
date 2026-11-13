using System.Diagnostics;

namespace EncyPcCheck;

/// <summary>Kurze Messungen: CPU (ein Kern / alle Kerne) und Schreibrate des Datentraegers.</summary>
internal static class Benchmarks
{
    private static volatile float _sink;

    /// <summary>Feste Rechenlast; Ergebnis in Millionen Schleifendurchlaeufen je Sekunde.</summary>
    private static double Kernel(double seconds, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long iterations = 0;
        double x = 1.0;
        ulong h = 14695981039346656037UL;
        while (sw.Elapsed.TotalSeconds < seconds && !ct.IsCancellationRequested)
        {
            for (int i = 0; i < 100_000; i++)
            {
                x = x * 1.0000001 + Math.Sqrt(x + i);
                h = (h ^ (ulong)i) * 1099511628211UL;
                if (x > 1e9) x = 1;
            }
            iterations += 100_000;
        }
        _sink = (float)(x + h);
        return iterations / sw.Elapsed.TotalSeconds / 1e6;
    }

    public static double CpuSingle(CancellationToken ct)
    {
        Kernel(0.3, ct); // Aufwaermen
        return Kernel(1.5, ct);
    }

    public static double CpuMulti(int threads, CancellationToken ct)
    {
        var results = new double[threads];
        var workers = new Thread[threads];
        for (int t = 0; t < threads; t++)
        {
            int idx = t;
            workers[t] = new Thread(() => results[idx] = Kernel(1.5, ct)) { IsBackground = true };
            workers[t].Start();
        }
        foreach (var w in workers) w.Join();
        return results.Sum();
    }

    /// <summary>Sequentielle Schreibrate in MB/s (256 MB, durchgeschrieben). Null bei Fehler.</summary>
    public static double? DiskWriteMBps(CancellationToken ct)
    {
        string dir = Path.Combine(Path.GetTempPath(), "EncyPcCheck");
        string file = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(dir);
            const int chunk = 4 * 1024 * 1024;
            const int chunks = 64; // 256 MB
            var buf = new byte[chunk];
            new Random(42).NextBytes(buf);

            using (var fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, chunk,
                       FileOptions.WriteThrough))
            {
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < chunks; i++)
                {
                    if (ct.IsCancellationRequested) return null;
                    fs.Write(buf, 0, chunk);
                }
                fs.Flush(true);
                sw.Stop();
                return chunks * (chunk / 1024.0 / 1024.0) / sw.Elapsed.TotalSeconds;
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
