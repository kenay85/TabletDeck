// File: /TabletDeck/TabletDeck/MetricsSampler.cs
using System.Diagnostics;
using System.IO;

namespace TabletDeck;

public sealed class MetricsSampler
{
    private readonly PerformanceCounter _cpu = new("Processor", "% Processor Time", "_Total");
    private readonly HardwareMetricsReader _hw = new();

    public MetricsSnapshot Sample()
    {
        // CPU load (pewne)
        var cpu = Math.Clamp(_cpu.NextValue(), 0, 100);

        // RAM (pewne)
        var (ramUsedMb, ramTotalMb) = MemoryUtil.GetSystemRamMb();

        // Dysk (kompatybilność wstecz + w razie gdyby tablet/stare UI patrzyło w diskFreeGb)
        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var di = new DriveInfo(systemDrive);
        var diskFreeGb = Math.Round(di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);

        // Lista dysków
        var disks = DiskUtil.GetFixedDisks();

        // Hardware (może być null - NIE jest krytyczne)
        HardwareSnapshot hw;
        try { hw = _hw.Read(); }
        catch { hw = default; }

        return new MetricsSnapshot(
            CpuPct: Math.Round(cpu, 1),
            CpuTempC: hw.CpuTempC,
            GpuName: hw.GpuName,
            GpuPct: hw.GpuPct,
            GpuTempC: hw.GpuTempC,
            GpuMemUsedMb: hw.GpuMemUsedMb,
            GpuMemTotalMb: hw.GpuMemTotalMb,
            RamUsedMb: ramUsedMb,
            RamTotalMb: ramTotalMb,
            DiskFreeGb: diskFreeGb,
            Disks: disks
        );
    }
}

public sealed record MetricsSnapshot(
    double CpuPct,
    double? CpuTempC,
    string? GpuName,
    double? GpuPct,
    double? GpuTempC,
    int? GpuMemUsedMb,
    int? GpuMemTotalMb,
    int RamUsedMb,
    int RamTotalMb,
    double DiskFreeGb,
    IReadOnlyList<DiskSnapshot> Disks
);

public sealed record DiskSnapshot(string Name, double TotalGb, double FreeGb);

internal static class DiskUtil
{
    public static IReadOnlyList<DiskSnapshot> GetFixedDisks()
    {
        var list = new List<DiskSnapshot>();
        foreach (var d in DriveInfo.GetDrives())
        {
            if (d.DriveType != DriveType.Fixed) continue;
            if (!d.IsReady) continue;

            var totalGb = d.TotalSize / 1024.0 / 1024.0 / 1024.0;
            var freeGb = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            var name = (d.Name ?? "").TrimEnd('\\');

            list.Add(new DiskSnapshot(name, Math.Round(totalGb, 1), Math.Round(freeGb, 1)));
        }
        return list;
    }
}

internal static class MemoryUtil
{
    public static (int usedMb, int totalMb) GetSystemRamMb()
    {
        var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
        var total = (long)ci.TotalPhysicalMemory;
        var avail = (long)ci.AvailablePhysicalMemory;
        var used = total - avail;

        return (
            usedMb: (int)Math.Round(used / 1024.0 / 1024.0),
            totalMb: (int)Math.Round(total / 1024.0 / 1024.0)
        );
    }
}
