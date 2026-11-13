namespace EncyPcCheck;

internal enum Rating { SehrGut, Gut, Bedingt, Eingeschraenkt, NichtGeeignet }

internal enum Kind { Cpu, Ram, Gpu, Disk }

internal sealed record CategoryResult(Kind Kind, string Name, string Value, int? Score, string Note);

internal sealed record UseCaseResult(string Name, int Index, Rating Rating);

internal sealed class CheckResult
{
    public List<UseCaseResult> UseCases { get; } = new();
    public bool GpuCritical { get; set; }
    public string GpuWarning { get; set; } = "";
    public List<CategoryResult> Categories { get; } = new();
    public int Index { get; set; }
    public Rating Rating { get; set; }
    public List<string> KnockOut { get; } = new();
    public string RatingText => Scoring.RatingText(Rating);
    public string RatingCode => Scoring.RatingCode(Rating);
    public string RatingHint { get; set; } = "";
}

/// <summary>
/// Bewertung. Quelle der Eckwerte: ENCY-Systemanforderungen (docs.encycam.com/ENCY/1/en/284.html):
/// Minimum (kleine/mittlere Projekte) Windows 10 64 Bit, 8 GB RAM, OpenGL-Grafik mit 1 GB;
/// empfohlen (grosse Projekte) Windows 11, Intel i7/i9 Rocket Lake oder AMD Ryzen 7/9 Zen 3 oder neuer, 32 GB RAM.
/// Alle Stufen dazwischen, die Gewichtung und die Benchmark-Referenzwerte sind Richtwerte des Add-ons.
/// </summary>
internal static class Scoring
{
    // ---- Referenzrechner = 100 Punkte (Vorgabe Datentechnik Reitz): Ryzen 9 9950X3D (16 Kerne), RTX 5090 32 GB,
    // ---- 96 GB DDR5, NVMe PCIe 4.0, Windows 11 Pro. Die CPU-Referenzwerte sind GESCHAETZT (nicht auf diesem Rechner gemessen):
    // ---- gemessener i9-14900HX (242 / 5322) hochgerechnet mit oeffentlichen Benchmark-Verhaeltnissen
    // ---- (Einzelkern ca. 1,15, alle Kerne ca. 1,43). Sobald ein Rechner dieser Klasse gemessen ist, hier ersetzen.
    public const double RefSingleMops = 278.0;
    public const double RefMultiMops = 7600.0;

    /// <summary>Verhaeltnis zum Referenzrechner (0..1) -> Wert 0..100. Empfohlene ENCY-Klasse (ca. 0,55) = 76.</summary>
    private static double CpuScore(double ratio) =>
        Map(Math.Min(1.0, ratio), new[] { (0.0, 0.0), (0.1, 18.0), (0.3, 48.0), (0.55, 76.0), (1.0, 100.0) });

    private const int WCpu = 35, WRam = 30, WGpu = 20, WDisk = 15;

    public static string RatingCode(Rating r) => r switch
    {
        Rating.SehrGut => "very_good",
        Rating.Gut => "good",
        Rating.Bedingt => "fair",
        Rating.Eingeschraenkt => "limited",
        _ => "unsuitable",
    };

    public static string RatingTextFromCode(string code) => code switch
    {
        "very_good" => RatingText(Rating.SehrGut),
        "good" => RatingText(Rating.Gut),
        "fair" => RatingText(Rating.Bedingt),
        "limited" => RatingText(Rating.Eingeschraenkt),
        "unsuitable" => RatingText(Rating.NichtGeeignet),
        _ => code,
    };

    public static string RatingText(Rating r) => r switch
    {
        Rating.SehrGut => Loc.T("Sehr gut geeignet", "Very well suited"),
        Rating.Gut => Loc.T("Gut geeignet", "Well suited"),
        Rating.Bedingt => Loc.T("Bedingt geeignet", "Moderately suited"),
        Rating.Eingeschraenkt => Loc.T("Eingeschränkt geeignet", "Limited suitability"),
        _ => Loc.T("Nicht geeignet", "Not suitable"),
    };

    private static string RatingHint(Rating r, bool knockOut) => r switch
    {
        Rating.SehrGut => Loc.T("Entspricht den empfohlenen Anforderungen für große Projekte.",
                                "Meets the recommended requirements for large projects."),
        Rating.Gut => Loc.T("Geeignet für mittlere bis große Projekte.", "Suitable for medium to large projects."),
        Rating.Bedingt => Loc.T("Geeignet für kleine und mittlere Projekte; bei großen Projekten sind Wartezeiten möglich.",
                                "Suitable for small and medium projects; waiting times are possible with large projects."),
        Rating.Eingeschraenkt => Loc.T("Mindestanforderungen knapp erreicht; nur für kleine Projekte.",
                                       "Minimum requirements barely met; small projects only."),
        _ => knockOut ? Loc.T("Mindestanforderung von ENCY nicht erfüllt.", "ENCY minimum requirement not met.")
                      : Loc.T("Deutlich unter den empfohlenen Anforderungen.", "Well below the recommended requirements."),
    };

    private static double Map(double v, (double x, double y)[] pts)
    {
        if (v <= pts[0].x) return pts[0].y;
        for (int i = 1; i < pts.Length; i++)
        {
            if (v <= pts[i].x)
            {
                var (x0, y0) = pts[i - 1];
                var (x1, y1) = pts[i];
                return y0 + (y1 - y0) * (v - x0) / (x1 - x0);
            }
        }
        return pts[^1].y;
    }

    public static CategoryResult Cpu(SystemData s, double single, double multi)
    {
        double a = CpuScore(single / RefSingleMops);
        double b = CpuScore(multi / RefMultiMops);
        int score = (int)Math.Round(0.5 * a + 0.5 * b);
        string name = s.CpuName + (s.CpuMhz > 0 ? $" ({s.CpuMhz / 1000.0:F1} GHz, {s.LogicalCores} Threads)" : $" ({s.LogicalCores} Threads)");
        return new CategoryResult(Kind.Cpu, Loc.T("Prozessor", "Processor"), name, score,
            Loc.T($"Messwerte: 1 Kern {single:F0}, alle Kerne {multi:F0} Punkte",
                  $"Measured: 1 core {single:F0}, all cores {multi:F0} points"));
    }

    public static CategoryResult Ram(SystemData s)
    {
        string name = Loc.T("Arbeitsspeicher", "Memory");
        if (s.RamGb <= 0) return new CategoryResult(Kind.Ram, name, Loc.T("nicht ermittelbar", "not detected"), null, "");
        int score = (int)Math.Round(Map(s.RamGb, new[] { (4.0, 0.0), (7.5, 30.0), (15.0, 55.0), (31.0, 78.0), (62.0, 92.0), (94.0, 100.0) }));
        return new CategoryResult(Kind.Ram, name, $"{s.RamGb:F1} GB",
            score, Loc.T("Minimum 8 GB, empfohlen 32 GB", "Minimum 8 GB, recommended 32 GB"));
    }

    public static CategoryResult Gpu(SystemData s)
    {
        string name = Loc.T("Grafik", "Graphics");
        if (s.GpuKind == GpuKind.Unknown)
            return new CategoryResult(Kind.Gpu, name, Loc.T("nicht ermittelbar", "not detected"), null, "");
        int score = (int)Math.Round(Map(s.VramGb, new[] { (0.5, 12.0), (1.0, 30.0), (2.0, 45.0), (4.0, 58.0), (8.0, 72.0), (16.0, 88.0), (30.0, 100.0) }));
        string note;
        switch (s.GpuKind)
        {
            case GpuKind.GeForce:
                note = Loc.T("NVIDIA GeForce mit OpenGL-Unterstützung (Treiber vorausgesetzt)", "NVIDIA GeForce with OpenGL support (driver required)");
                if (s.VramGb < 1.0) note += Loc.T("; Grafikspeicher unter 1 GB", "; video memory below 1 GB");
                break;
            case GpuKind.Quadro:
                score = Math.Min(score, 30);
                note = Loc.T("KRITISCH: NVIDIA Quadro; normale NVIDIA GeForce erforderlich", "CRITICAL: NVIDIA Quadro; a standard NVIDIA GeForce is required");
                break;
            default:
                score = Math.Min(score, 30);
                note = Loc.T("KRITISCH: keine NVIDIA-Grafik; NVIDIA GeForce mit OpenGL erforderlich", "CRITICAL: no NVIDIA graphics; NVIDIA GeForce with OpenGL is required");
                break;
        }
        return new CategoryResult(Kind.Gpu, name, $"{s.GpuName}, {s.VramGb:F1} GB", score, note);
    }

    public static CategoryResult Disk(SystemData s, double? writeMBps)
    {
        string drive = string.IsNullOrEmpty(s.DriveName) ? Loc.T("Datenträger", "Drive") : Loc.T("Datenträger ", "Drive ") + s.DriveName;
        string kind = s.DriveIsHdd switch { true => "HDD", false => "SSD", _ => Loc.T("Typ unbekannt", "type unknown") };
        if (writeMBps == null)
            return new CategoryResult(Kind.Disk, drive, kind + Loc.T(", Messung fehlgeschlagen", ", measurement failed"), null, "");
        double sc = Map(writeMBps.Value, new[] { (50.0, 10.0), (120.0, 22.0), (300.0, 40.0), (500.0, 52.0), (1500.0, 75.0), (3000.0, 100.0) });
        if (s.DriveIsHdd == true) sc = Math.Min(sc, 40);
        return new CategoryResult(Kind.Disk, drive, Loc.T($"{kind}, Schreibrate {writeMBps.Value:F0} MB/s", $"{kind}, write speed {writeMBps.Value:F0} MB/s"),
            (int)Math.Round(sc), Loc.T("Messung: 256 MB sequentiell geschrieben (Temp-Ordner)", "Test: 256 MB written sequentially (temp folder)"));
    }

    private static Rating RatingFor(int index, bool knockOut) =>
        knockOut ? Rating.NichtGeeignet
        : index >= 85 ? Rating.SehrGut
        : index >= 70 ? Rating.Gut
        : index >= 50 ? Rating.Bedingt
        : index >= 30 ? Rating.Eingeschraenkt
        : Rating.NichtGeeignet;

    // Anwendungsfaelle: Gewichte (Summe 100) fuer 1 Kern, alle Kerne, RAM, Grafik, Datentraeger. Richtwerte.
    private static readonly (string De, string En, int Cs, int Cm, int Ram, int Gpu, int Disk, bool Easy)[] UseCaseDefs =
    {
        ("Große Bauteile mit vielen Details", "Large parts with many details",   10, 15, 35, 25, 15, false),
        ("Schnelle Berechnung",               "Fast toolpath calculation",        25, 40, 20,  5, 10, false),
        ("Detaillierte Simulation",           "Detailed simulation",              20, 10, 30, 30, 10, false),
        ("Einfache Teile",                    "Simple parts",                     30, 10, 25, 15, 20, true),
    };

    public static CheckResult Evaluate(SystemData s, List<CategoryResult> cats, double single, double multi)
    {
        var r = new CheckResult();
        r.Categories.AddRange(cats);

        // Ausschlusskriterien: Mindestanforderungen laut ENCY
        if (!s.Is64BitOs) r.KnockOut.Add(Loc.T("Kein 64-Bit-Betriebssystem (Minimum: Windows 10 64 Bit)", "Not a 64-bit operating system (minimum: Windows 10 64-bit)"));
        if (s.OsBuild > 0 && s.OsBuild < 10240) r.KnockOut.Add(Loc.T("Betriebssystem älter als Windows 10", "Operating system older than Windows 10"));
        if (s.RamGb > 0 && s.RamGb < 7.5) r.KnockOut.Add(Loc.T($"Arbeitsspeicher {s.RamGb:F1} GB (Minimum: 8 GB)", $"Memory {s.RamGb:F1} GB (minimum: 8 GB)"));
        bool ko = r.KnockOut.Count > 0;

        // NVIDIA-Pruefung: nur eine normale NVIDIA GeForce ist unkritisch
        r.GpuCritical = s.GpuKind is GpuKind.Quadro or GpuKind.Other;
        if (s.GpuKind == GpuKind.Quadro)
            r.GpuWarning = Loc.T("Kritisch: NVIDIA Quadro erkannt. Erforderlich ist eine normale NVIDIA GeForce mit OpenGL-Unterstützung.",
                                 "Critical: NVIDIA Quadro detected. A standard NVIDIA GeForce with OpenGL support is required.");
        else if (s.GpuKind == GpuKind.Other)
            r.GpuWarning = Loc.T("Kritisch: keine NVIDIA-Grafik erkannt. Erforderlich ist eine normale NVIDIA GeForce mit OpenGL-Unterstützung.",
                                 "Critical: no NVIDIA graphics detected. A standard NVIDIA GeForce with OpenGL support is required.");

        // Teilwerte 0..100
        double cs = CpuScore(single / RefSingleMops);
        double cm = CpuScore(multi / RefMultiMops);
        int? ramS = cats.FirstOrDefault(c => c.Kind == Kind.Ram)?.Score;
        int? gpuS = cats.FirstOrDefault(c => c.Kind == Kind.Gpu)?.Score;
        int? diskS = cats.FirstOrDefault(c => c.Kind == Kind.Disk)?.Score;

        // Gesamtindex (Hall of Fame): Prozessor 35, RAM 30, Grafik 20, Datentraeger 15
        double sum = 0, wsum = 0;
        int minScore = 100;
        foreach (var c in cats)
        {
            if (c.Score is not int sc) continue;
            int w = c.Kind switch { Kind.Cpu => WCpu, Kind.Ram => WRam, Kind.Gpu => WGpu, _ => WDisk };
            sum += sc * w; wsum += w; minScore = Math.Min(minScore, sc);
        }
        double idx = wsum > 0 ? sum / wsum : 0;
        if (minScore < 40) idx = Math.Min(idx, 69);
        if (r.GpuCritical) idx = Math.Min(idx, 49);
        if (ko) idx = Math.Min(idx, 29);
        r.Index = (int)Math.Round(idx);
        r.Rating = RatingFor(r.Index, ko);
        r.RatingHint = RatingHint(r.Rating, ko);

        // Bewertung je Anwendungsfall
        foreach (var d in UseCaseDefs)
        {
            double usum = 0, uw = 0, umin = 100;
            void Add(double? score, int w)
            {
                if (score is not double sc || w <= 0) return;
                if (d.Easy) sc = 100 * Math.Sqrt(sc / 100); // geringere Anforderungen: schwache Werte zaehlen weniger
                usum += sc * w; uw += w;
                if (w >= 25) umin = Math.Min(umin, sc);
            }
            Add(cs, d.Cs); Add(cm, d.Cm); Add(ramS, d.Ram); Add(gpuS, d.Gpu); Add(diskS, d.Disk);
            double u = uw > 0 ? usum / uw : 0;
            if (umin < 40) u = Math.Min(u, 69);     // stark gewichtete Schwachstelle begrenzt auf "bedingt"
            if (r.GpuCritical) u = Math.Min(u, 49); // keine NVIDIA GeForce: hoechstens "eingeschraenkt"
            if (ko) u = Math.Min(u, 29);
            int ui = (int)Math.Round(u);
            r.UseCases.Add(new UseCaseResult(Loc.T(d.De, d.En), ui, RatingFor(ui, ko)));
        }
        return r;
    }
}
