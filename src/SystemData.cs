using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EncyPcCheck;

internal enum GpuKind { Unknown, GeForce, Quadro, Other }

/// <summary>Ermittelte Systemdaten (ohne Benchmark).</summary>
internal sealed class SystemData
{
    public string OsName { get; set; } = "unbekannt";
    public int OsBuild { get; set; }
    public bool Is64BitOs { get; set; }
    public string CpuName { get; set; } = "unbekannt";
    public int CpuMhz { get; set; }
    public int LogicalCores { get; set; }
    public double RamGb { get; set; }
    public string GpuName { get; set; } = "nicht ermittelbar";
    public double VramGb { get; set; }
    public GpuKind GpuKind { get; set; } = GpuKind.Unknown;
    public string DriveName { get; set; } = "";
    public bool? DriveIsHdd { get; set; }

    public bool IsWindows11 => OsBuild >= 22000;

    public static SystemData Collect()
    {
        var d = new SystemData
        {
            Is64BitOs = Environment.Is64BitOperatingSystem,
            LogicalCores = Environment.ProcessorCount,
        };
        Try(() => ReadOs(d));
        Try(() => ReadCpu(d));
        Try(() => ReadRam(d));
        Try(() => ReadGpu(d));
        Try(() => ReadDrive(d));
        return d;
    }

    private static void Try(Action a)
    {
        try { a(); } catch { /* Einzelwert nicht ermittelbar: Standardwert bleibt */ }
    }

    private static void ReadOs(SystemData d)
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (k == null) return;
        _ = int.TryParse(k.GetValue("CurrentBuildNumber")?.ToString(), out int build);
        d.OsBuild = build;
        string product = k.GetValue("ProductName")?.ToString() ?? "Windows";
        // ProductName nennt auch unter Windows 11 noch "Windows 10": Build-Nummer entscheidet.
        if (build >= 22000) product = product.Replace("Windows 10", "Windows 11");
        d.OsName = product + " (Build " + build + ")";
    }

    private static void ReadCpu(SystemData d)
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        if (k == null) return;
        d.CpuName = (k.GetValue("ProcessorNameString")?.ToString() ?? "unbekannt").Trim();
        if (k.GetValue("~MHz") is int mhz) d.CpuMhz = mhz;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    private static void ReadRam(SystemData d)
    {
        var m = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref m))
            d.RamGb = m.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
    }

    private static void ReadGpu(SystemData d)
    {
        // Grafikadapter-Klasse; Unterschluessel 0000, 0001, ...
        using var cls = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
        if (cls == null) return;
        // Rangfolge: NVIDIA GeForce (nicht Quadro) vor Quadro vor Sonstigen; innerhalb gleicher Art der groesste Grafikspeicher.
        GpuKind bestKind = GpuKind.Unknown;
        double bestVram = -1;
        foreach (string sub in cls.GetSubKeyNames())
        {
            if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;
            using var k = cls.OpenSubKey(sub);
            string? name = k?.GetValue("DriverDesc")?.ToString();
            if (k == null || string.IsNullOrWhiteSpace(name)) continue;
            if (name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;

            double vram = 0;
            object? q = k.GetValue("HardwareInformation.qwMemorySize");
            if (q is long l) vram = l;
            else if (q is byte[] qb && qb.Length >= 8) vram = BitConverter.ToInt64(qb, 0);
            else
            {
                object? m = k.GetValue("HardwareInformation.MemorySize");
                if (m is int i) vram = (uint)i;
                else if (m is byte[] mb && mb.Length >= 4) vram = BitConverter.ToUInt32(mb, 0);
            }
            double gb = vram / 1024.0 / 1024.0 / 1024.0;

            bool nvidia = name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                          name.Contains("Quadro", StringComparison.OrdinalIgnoreCase);
            bool quadro = name.Contains("Quadro", StringComparison.OrdinalIgnoreCase);
            GpuKind kind = !nvidia ? GpuKind.Other : quadro ? GpuKind.Quadro : GpuKind.GeForce;

            int rank(GpuKind x) => x switch { GpuKind.GeForce => 3, GpuKind.Quadro => 2, GpuKind.Other => 1, _ => 0 };
            if (rank(kind) > rank(bestKind) || (kind == bestKind && gb > bestVram))
            {
                bestKind = kind;
                bestVram = gb;
                d.GpuName = name.Trim();
                d.VramGb = gb;
                d.GpuKind = kind;
            }
        }
    }

    // --- Laufwerkstyp (SSD/HDD) ueber IOCTL_STORAGE_QUERY_PROPERTY / Seek-Penalty ---

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(IntPtr hDevice, uint dwIoControlCode,
        ref StoragePropertyQuery lpInBuffer, int nInBufferSize,
        out DeviceSeekPenaltyDescriptor lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public int PropertyId;     // 7 = StorageDeviceSeekPenaltyProperty
        public int QueryType;      // 0 = PropertyStandardQuery
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceSeekPenaltyDescriptor
    {
        public int Version;
        public int Size;
        [MarshalAs(UnmanagedType.U1)] public bool IncursSeekPenalty;
    }

    private static void ReadDrive(SystemData d)
    {
        string root = Path.GetPathRoot(Path.GetTempPath()) ?? "";
        if (root.Length < 2 || root[1] != ':') return;
        d.DriveName = root.Substring(0, 2);

        const uint FILE_SHARE_READ_WRITE = 3;
        const uint OPEN_EXISTING = 3;
        IntPtr h = CreateFile(@"\\.\" + d.DriveName, 0, FILE_SHARE_READ_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == IntPtr.Zero || h == new IntPtr(-1)) return;
        try
        {
            var q = new StoragePropertyQuery { PropertyId = 7, QueryType = 0 };
            if (DeviceIoControl(h, 0x2D1400, ref q, Marshal.SizeOf<StoragePropertyQuery>(),
                    out var desc, Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(), out _, IntPtr.Zero))
                d.DriveIsHdd = desc.IncursSeekPenalty;
        }
        finally { CloseHandle(h); }
    }
}
