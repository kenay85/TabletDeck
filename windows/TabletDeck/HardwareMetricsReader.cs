// File: /TabletDeck/TabletDeck/HardwareMetricsReader.cs
using LibreHardwareMonitor.Hardware;

namespace TabletDeck;

/// <summary>
/// CPU/GPU: temperatury, obciążenie, VRAM.
/// CPU temp: heurystyka po nazwach + fallback do płyty, ale z blokadą iGPU/Graphics.
/// </summary>
internal sealed class HardwareMetricsReader : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _updateVisitor = new();

    public HardwareMetricsReader()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true
        };
        _computer.Open();
    }

    public HardwareSnapshot Read()
    {
        try
        {
            _computer.Accept(_updateVisitor);

            var cpuTemp = ReadCpuTempC();

            var gpu = PickPreferredGpu();
            var gpuPct = gpu is null ? null : ReadGpuCoreLoadPct(gpu);
            var gpuTemp = gpu is null ? null : ReadGpuTempC(gpu);
            var (vramUsedMb, vramTotalMb) = gpu is null ? (null, null) : ReadGpuVramMb(gpu);

            return new HardwareSnapshot(
                CpuTempC: cpuTemp,
                GpuName: gpu?.Name,
                GpuPct: gpuPct,
                GpuTempC: gpuTemp,
                GpuMemUsedMb: vramUsedMb,
                GpuMemTotalMb: vramTotalMb
            );
        }
        catch
        {
            return default;
        }
    }

    private void DumpCpuTempCandidates()
    {
        try
        {
            var temps = new List<(string hwName, HardwareType hwType, string sensorName, double v)>();
            foreach (var hw in _computer.Hardware)
                CollectTempsRecursive(hw, temps);

            Log.Info("[HW] ---- CPU TEMP CANDIDATES (all) ----");

            foreach (var t in temps.Where(x => !IsGpuHardwareType(x.hwType)))
            {
                var ok = IsPlausibleTemp(t.v) ? "OK" : "BAD";
                Log.Info($"[HW] {ok} [{t.hwType}] {t.hwName} / {t.sensorName} = {t.v:0.###}");
            }

            Log.Info("[HW] ---- END CPU TEMP CANDIDATES ----");
        }
        catch (Exception ex)
        {
            Log.Info($"[HW] DumpCpuTempCandidates failed: {ex.Message}");
        }
    }


    private double? ReadCpuTempC()
    {
        // 1) Zbierz wszystkie temperatury, ale nie z GPU hardware.
        var temps = new List<(string hwName, HardwareType hwType, string sensorName, double tempC)>();
        foreach (var hw in _computer.Hardware)
            CollectTempsRecursive(hw, temps);

        temps = temps
            .Where(t => IsPlausibleTemp(t.tempC))
            .Where(t => !IsGpuHardwareType(t.hwType))
            .ToList();

        if (temps.Count == 0)
            return null;

        // 2) Preferuj najpierw CPU hardware, potem płyta.
        var cpuCandidates = temps.Where(t => t.hwType == HardwareType.Cpu).ToList();
        var boardCandidates = temps.Where(t => t.hwType is HardwareType.Motherboard or HardwareType.SuperIO or HardwareType.EmbeddedController).ToList();

        var fromCpu = PickCpuLikeTemp(cpuCandidates);
        if (fromCpu is not null) return fromCpu;

        var fromBoard = PickCpuLikeTemp(boardCandidates);
        return fromBoard;
    }

    private static double? PickCpuLikeTemp(List<(string hwName, HardwareType hwType, string sensorName, double tempC)> candidates)
    {
        if (candidates.Count == 0) return null;

        static int Score(string hwName, string sensorName)
        {
            var h = (hwName ?? "").ToLowerInvariant();
            var n = (sensorName ?? "").ToLowerInvariant();

            // Twarde odrzucenia: iGPU/graphics + chipset/system
            if (ContainsAny(n, "gpu", "graphics", "igpu", "uhd", "iris", "radeon graphics", "gt ")) return int.MinValue / 4;
            if (ContainsAny(n, "pch", "chipset", "system", "ambient", "vrm", "mos", "memory", "ram")) return int.MinValue / 4;

            var s = 0;

            // ✅ Najlepsze: dokładnie to co zwykle pokazuje HWMonitor jako "CPU Package"/"Tctl/Tdie"
            if (n.Contains("core (tctl/tdie)")) s += 300;        // TOP 1 na Ryzenach
            if (ContainsAny(n, "cpu package", "package")) s += 250;

            // ✅ Bardzo dobre: ogólne Tctl/Tdie (ale nie CCD)
            if (ContainsAny(n, "tctl/tdie", "tctl", "tdie"))
            {
                s += 220;
                if (n.Contains("ccd")) s -= 120;                 // CCD traktuj jako fallback
            }

            // ✅ CCD (Tdie) — działa, ale mniej “globalne”
            if (n.Contains("ccd") && ContainsAny(n, "tdie", "tctl/tdie")) s += 80;

            // Dodatkowe:
            if (n.Contains("peci")) s += 120;
            if (n.Contains("cpu")) s += 60;
            if (ContainsAny(n, "core", "die")) s += 20;
            if (n.Contains("soc")) s += 30;

            if (h.Contains("cpu")) s += 10;

            return s;
        }

        // PASS 1: tylko “pewne” CPU (score >= 60)
        var pass1 = candidates
            .Select(c => (c, score: Score(c.hwName, c.sensorName)))
            .Where(x => x.score >= 60)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.c.tempC)
            .ToList();

        if (pass1.Count > 0)
        {
            var best = pass1[0];
            Log.Info($"[HW] CPU temp source: [{best.c.hwType}] {best.c.hwName} / {best.c.sensorName} = {best.c.tempC:0.0}C (score={best.score})");
            return best.c.tempC;
        }

        // PASS 2: dopuść “średnie” CPU (score >= 30), ale nadal bez iGPU/PCH/etc.
        var pass2 = candidates
            .Select(c => (c, score: Score(c.hwName, c.sensorName)))
            .Where(x => x.score >= 30)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.c.tempC)
            .ToList();

        if (pass2.Count > 0)
        {
            var best = pass2[0];
            Log.Info($"[HW] CPU temp source: [{best.c.hwType}] {best.c.hwName} / {best.c.sensorName} = {best.c.tempC:0.0}C (score={best.score})");
            return best.c.tempC;
        }

        // PASS 3: jeśli nadal nic, NIE zgaduj na ślepo -> null
        return null;
    }

    private static bool ContainsAny(string s, params string[] parts)
    {
        foreach (var p in parts)
            if (s.Contains(p)) return true;
        return false;
    }

    private static bool IsGpuHardwareType(HardwareType t) =>
        t is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static void CollectTempsRecursive(IHardware hw, List<(string hwName, HardwareType hwType, string sensorName, double tempC)> outList)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != SensorType.Temperature) continue;
            if (s.Value is null) continue;
            outList.Add((hw.Name ?? hw.HardwareType.ToString(), hw.HardwareType, s.Name ?? "", s.Value.Value));
        }

        foreach (var sub in hw.SubHardware)
            CollectTempsRecursive(sub, outList);
    }

    private static bool IsPlausibleTemp(double t)
    {
        if (t <= 5.0) return false;
        if (t >= 250.0) return false;
        if (t > 125.0) return false;
        return true;
    }

    private IHardware? PickPreferredGpu()
    {
        IHardware? nvidia = null;
        IHardware? amd = null;
        IHardware? intel = null;

        foreach (var hw in _computer.Hardware)
        {
            if (hw.HardwareType == HardwareType.GpuNvidia) nvidia ??= hw;
            else if (hw.HardwareType == HardwareType.GpuAmd) amd ??= hw;
            else if (hw.HardwareType == HardwareType.GpuIntel) intel ??= hw;
        }

        return nvidia ?? amd ?? intel;
    }

    private static double? ReadGpuCoreLoadPct(IHardware gpu)
    {
        double? bestPreferred = null;
        double? bestAny = null;

        void Scan(IHardware h)
        {
            foreach (var s in h.Sensors)
            {
                if (s.SensorType != SensorType.Load || s.Value is null) continue;

                var v = (double)s.Value.Value;
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                v = Math.Clamp(v, 0, 100);

                bestAny = Math.Max(bestAny ?? double.MinValue, v);

                var name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("gpu core") || name == "core" || name.Contains("core"))
                    bestPreferred = Math.Max(bestPreferred ?? double.MinValue, v);
            }

            foreach (var sub in h.SubHardware) Scan(sub);
        }

        Scan(gpu);
        return bestPreferred ?? bestAny;
    }

    private static double? ReadGpuTempC(IHardware gpu)
    {
        var temps = new List<(string sensorName, double tempC)>();

        void Scan(IHardware h)
        {
            foreach (var s in h.Sensors)
            {
                if (s.SensorType != SensorType.Temperature || s.Value is null) continue;
                temps.Add((s.Name ?? "", s.Value.Value));
            }
            foreach (var sub in h.SubHardware) Scan(sub);
        }

        Scan(gpu);

        var good = temps
            .Where(x => IsPlausibleTemp(x.tempC))
            .ToList();

        if (good.Count == 0) return null;

        static int Score(string sensorName)
        {
            var n = sensorName.ToLowerInvariant();
            var s = 0;
            if (n.Contains("hot spot") || n.Contains("hotspot")) s += 30;
            if (n.Contains("gpu")) s += 20;
            if (n.Contains("core")) s += 10;
            return s;
        }

        return good
            .Select(x => (x.tempC, score: Score(x.sensorName)))
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.tempC)
            .First()
            .tempC;
    }

    private static (int? usedMb, int? totalMb) ReadGpuVramMb(IHardware gpu)
    {
        float? used = null;
        float? total = null;
        float? free = null;

        void Scan(IHardware h)
        {
            foreach (var s in h.Sensors)
            {
                if (s.Value is null) continue;

                if (s.SensorType is SensorType.Data or SensorType.SmallData)
                {
                    var name = (s.Name ?? "").ToLowerInvariant();
                    if (name.Contains("memory") && name.Contains("used")) used ??= s.Value.Value;
                    if (name.Contains("memory") && name.Contains("total")) total ??= s.Value.Value;
                    if (name.Contains("memory") && name.Contains("free")) free ??= s.Value.Value;
                }
            }
            foreach (var sub in h.SubHardware) Scan(sub);
        }

        Scan(gpu);

        static int? ToMb(float? v)
        {
            if (v is null) return null;
            var x = v.Value;
            if (float.IsNaN(x) || float.IsInfinity(x) || x <= 0) return null;
            var mb = x < 128 ? x * 1024f : x;
            return mb <= 0 ? null : (int)Math.Round(mb);
        }

        var usedMb = ToMb(used);
        var totalMb = ToMb(total);
        if (totalMb is null && used is not null && free is not null)
            totalMb = ToMb(used.Value + free.Value);

        return (usedMb, totalMb);
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();

            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }


        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}

internal readonly record struct HardwareSnapshot(
    double? CpuTempC,
    string? GpuName,
    double? GpuPct,
    double? GpuTempC,
    int? GpuMemUsedMb,
    int? GpuMemTotalMb
);
