using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;

namespace CyborgVitals.Collector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length < 2 || args.Length > 5 || args[0] != "--output" || args.Skip(2).Any(a => a != "--hardware-sensors" && a != "--motherboard-fans" && a != "--show-tray"))
        {
            MessageBox.Show("Please use Start-Monitor.ps1 from the downloaded folder.", "Cyborg Vitals");
            return;
        }
        string output;
        try { output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output); }
        catch (Exception e) { MessageBox.Show(e.Message, "Cyborg Vitals"); return; }
        // UAC must elevate this same account: another administrator's package path is different.
        string packageRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"))
            + Path.DirectorySeparatorChar;
        if (!output.StartsWith(packageRoot, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(new DirectoryInfo(output).Name, "LocalState", StringComparison.OrdinalIgnoreCase)
            || !new DirectoryInfo(output).Parent!.Name.StartsWith("CyborgVitals_", StringComparison.Ordinal))
        {
            MessageBox.Show("The output must be this account's CyborgVitals package LocalState folder. " +
                "Sign in with the same administrator account used to install the widget.", "Cyborg Vitals");
            return;
        }
        using var mutex = new Mutex(true, @"Local\CyborgVitalsCollector", out bool created);
        if (!created) { MessageBox.Show("The sensor reader is already running. Use Stop reader in Setup or Stop-Monitor.ps1 before restarting it.", "Cyborg Vitals"); return; }
        try { Application.Run(new CollectorContext(output, args.Contains("--hardware-sensors"), args.Contains("--hardware-sensors") && args.Contains("--motherboard-fans"), args.Contains("--show-tray"))); }
        catch (Exception e) { MessageBox.Show(e.ToString(), "Cyborg Vitals — sensor reader error"); }
    }
}

internal sealed class CollectorContext : ApplicationContext
{
    private readonly NotifyIcon? tray;
    private readonly Control dispatcher = new();
    private readonly long startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private readonly string output;
    private bool exiting;

    private readonly bool hardwareSensors, motherboardFans, showTray;
    private readonly string sensorDriverStatus;
    public CollectorContext(string output, bool hardwareSensors, bool motherboardFans, bool showTray)
    {
        this.output = output; this.hardwareSensors = hardwareSensors; this.motherboardFans = motherboardFans; this.showTray = showTray;
        sensorDriverStatus = hardwareSensors ? SensorDriverStatus() : "Hardware reader disabled.";
        _ = dispatcher.Handle; // A non-visible control for shutdown dispatch; no taskbar/tray window.
        if (showTray)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Cyborg Vitals · sensor reader").Enabled = false;
            menu.Items.Add("Open readings and diagnostics", null, (_, _) =>
                Process.Start(new ProcessStartInfo("explorer.exe", output) { UseShellExecute = true }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit sensor reader", null, (_, _) => ExitThread());
            tray = new NotifyIcon { Icon = System.Drawing.SystemIcons.Information,
                Text = "Cyborg Vitals · sensor reader", ContextMenuStrip = menu, Visible = true };
        }
        worker = Task.Run(ReadLoop);
    }

    protected override void ExitThreadCore()
    {
        if (exiting) return;
        exiting = true;
        stop.Cancel();
        if (tray != null) { tray.Visible = false; tray.Dispose(); }
        // Wait for any active hardware call to finish before the process exits.
        // Shutdown never disposes hardware concurrently with a sensor read.
        worker.GetAwaiter().GetResult();
        dispatcher.Dispose();
        stop.Dispose();
        base.ExitThreadCore();
    }

    private sealed record FanState(string id, string name, string hardwareName, double? rpm);
    private sealed record SensorState(long Timestamp, double? CpuTemp, double? GpuTemp, double? GpuLoad,
        double? GpuMemory, string? CpuLabel, string? GpuLabel, string CpuName, string GpuName, FanState[] Fans,
        string FanSource, string FanStatus, string? MsiFanRaw, string? Error);
    private SensorState? sensorState;
    private volatile string hardwareStatus = "Waiting for a visible hardware reading in the widget.";
    private string SensorDriverStatus()
    {
        try { return LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled ? "PawnIO installed (does not guarantee driver access)." : "PawnIO not installed; some CPU temperatures and motherboard sensors may be unavailable."; }
        catch (Exception e) { return "PawnIO status unavailable: " + e.Message; }
    }

    private MonitorDemand? ReadDemand()
    {
        try
        {
            string path = Path.Combine(output, "reader-demand.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) return null;
            var demand = JsonSerializer.Deserialize<MonitorDemand>(File.ReadAllText(path), new JsonSerializerOptions { MaxDepth = 4 });
            return demand != null && demand.IsFresh(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) ? demand : null;
        }
        catch { return null; } // Missing, partial or invalid requests never activate hardware probing.
    }

    private bool StopRequested()
    {
        try
        {
            string path = Path.Combine(output, "reader-stop.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) return false;
            using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { MaxDepth = 4 });
            return json.RootElement.TryGetProperty("processId", out var pid) && pid.TryGetInt32(out int target)
                && json.RootElement.TryGetProperty("timestampUnixMs", out var time) && time.TryGetInt64(out long timestamp)
                && Metrics.StopRequestMatches(target, Environment.ProcessId, timestamp, startedAt, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        catch { return false; }
    }

    private async Task ReadLoop()
    {
        // Driver initialization or one slow hardware probe must not stop the CPU/RAM heartbeat.
        var sensors = hardwareSensors ? Task.Run(SensorLoop) : Task.CompletedTask;
        var nativeCpu = new NativeCpu();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (StopRequested())
                {
                    stop.Cancel();
                    dispatcher.BeginInvoke(new Action(ExitThread));
                    break;
                }
                var state = Volatile.Read(ref sensorState);
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                bool fresh = state != null && now - state.Timestamp <= 10000;
                var memory = NativeMemory.Read();
                var snapshot = new {
                    schemaVersion = 1, timestampUnixMs = now,
                    readerProcessId = Environment.ProcessId, trayIconEnabled = showTray,
                    hardwareSensorsEnabled = hardwareSensors, motherboardFanProbingEnabled = motherboardFans,
                    hardwareMonitoringStatus = hardwareStatus,
                    sensorDriverStatus,
                    hardwareTimestampUnixMs = state?.Timestamp,
                    cpuUsagePercent = nativeCpu.Read(), ramUsedGiB = memory.used, ramTotalGiB = memory.total,
                    ramUsagePercent = memory.total is > 0 ? 100 * memory.used / memory.total : null,
                    cpuTemperatureC = fresh ? state!.CpuTemp : null,
                    cpuTemperatureLabel = state?.CpuLabel,
                    gpuUsagePercent = fresh ? state!.GpuLoad : null,
                    gpuTemperatureC = fresh ? state!.GpuTemp : null,
                    gpuTemperatureLabel = state?.GpuLabel,
                    gpuMemoryUsedMiB = fresh ? state!.GpuMemory : null,
                    fans = fresh ? state!.Fans : Array.Empty<FanState>(),
                    fanSource = fresh ? state!.FanSource : "Unavailable",
                    fanStatus = state?.FanStatus ?? "Not requested yet.",
                    msiFanRaw = fresh ? state!.MsiFanRaw : null,
                    cpuName = state?.CpuName ?? "CPU", gpuName = state?.GpuName ?? "GPU",
                    sensorError = !hardwareSensors ? "Hardware probing is disabled. CPU/RAM use Windows APIs." : fresh ? state!.Error : state == null ? hardwareStatus : "Hardware sensor update delayed; old temperatures and fan RPM were cleared."
                };
                try { AtomicWrite("telemetry.json", JsonSerializer.Serialize(snapshot)); }
                catch (Exception e) { LogError("Writing telemetry: " + e); }
                try { await Task.Delay(1000, stop.Token); } catch (OperationCanceledException) { break; }
            }
        }
        finally { await sensors; }
    }

    private async Task SensorLoop()
    {
        Computer? computer = null;
        var msiFans = new MsiFanReader();
        try
        {
            int tick = 0;
            while (!stop.IsCancellationRequested)
            {
                var demand = ReadDemand();
                if (demand == null || !demand.HasHardwareReadings)
                {
                    hardwareStatus = "Paused: widget hidden/closed, monitoring request expired, or no hardware readings enabled.";
                    Volatile.Write(ref sensorState, null);
                    if (computer != null) { computer.Close(); computer = null; }
                    try { await Task.Delay(3000, stop.Token); } catch (OperationCanceledException) { break; }
                    continue;
                }
                bool cpuEnabled = demand.Cpu, gpuEnabled = demand.Gpu || demand.Fans;
                bool boardEnabled = motherboardFans && demand.Fans;
                if (computer == null || computer.IsCpuEnabled != cpuEnabled || computer.IsGpuEnabled != gpuEnabled
                    || computer.IsMotherboardEnabled != boardEnabled)
                {
                    Volatile.Write(ref sensorState, null);
                    if (computer != null) { computer.Close(); computer = null; }
                    hardwareStatus = "Starting selected hardware sensors...";
                    computer = new Computer {
                        IsCpuEnabled = cpuEnabled, IsGpuEnabled = gpuEnabled, IsMotherboardEnabled = boardEnabled,
                        IsMemoryEnabled = false, IsControllerEnabled = false, IsStorageEnabled = false, IsNetworkEnabled = false
                    };
                    computer.Open();
                    tick = 0;
                }
                var errors = new List<string>();
                var all = new List<IHardware>();
                foreach (var hardware in computer.Hardware) UpdateHardware(hardware, all, errors);
                var cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
                var gpu = all.OrderBy(h => GpuPriority(h.HardwareType)).FirstOrDefault(h => GpuPriority(h.HardwareType) < 99);
                var cpuTemp = CpuTemperature(cpu);
                var gpuTemp = Sensor(gpu, SensorType.Temperature, "GPU Core");
                var fans = all.SelectMany(h => h.Sensors.Where(x => x.SensorType == SensorType.Fan)
                    .Select(x => new FanState(x.Identifier.ToString(), x.Name, h.Name, Metrics.Finite(x.Value, 0, 30000))))
                    .GroupBy(f => f.id).Select(g => g.First()).OrderBy(f => f.id).ToArray();
                string fanSource = demand.Fans && fans.Any(f => f.rpm.HasValue) ? "LibreHardwareMonitor" : "Unavailable";
                string fanStatus = demand.Fans ? "General sensor library has no usable RPM reading." : "Fan readings disabled; MSI interface not polled.";
                string? msiRaw = null;
                if (MsiFanProtocol.NeedsFallback(demand.Fans, fans.Select(f => f.rpm)))
                {
                    var fallback = await msiFans.ReadAsync();
                    fanStatus = msiFans.Status;
                    if (fallback != null)
                    {
                        msiRaw = fallback.Raw;
                        fans = fallback.Rpm.Select((rpm, channel) => new FanState("/msi/wmi/fan/" + channel,
                            "Fan " + (channel + 1), "MSI ACPI WMI", rpm)).Where(f => f.rpm.HasValue).ToArray();
                        if (fans.Length > 0) fanSource = "MSI ACPI WMI";
                    }
                }
                else if (demand.Fans) fanStatus = "General sensor RPM available; MSI fallback not polled.";
                if (demand.Cpu && cpuTemp == null) errors.Add("CPU temperature not reported by the sensor library; check driver/sensor availability.");
                if (demand.Gpu && gpuTemp == null) errors.Add("GPU temperature not reported; the GPU may be sleeping or unsupported.");
                if (demand.Fans && !fans.Any(f => f.rpm.HasValue)) errors.Add("No usable fan RPM reading. " + fanStatus);
                hardwareStatus = "Active: " + (cpuEnabled ? "CPU " : "") + (gpuEnabled ? "GPU " : "")
                    + (boardEnabled ? "motherboard/fan " : "") + (fanSource == "MSI ACPI WMI" ? "MSI fan " : "")
                    + "(3-second hardware interval).";
                Volatile.Write(ref sensorState, new SensorState(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Metrics.Finite(cpuTemp?.Value, 0, 150), Metrics.Finite(gpuTemp?.Value, 0, 150),
                    Metrics.Finite(Sensor(gpu, SensorType.Load, "GPU Core")?.Value, 0, 100),
                    Metrics.Finite(Sensor(gpu, SensorType.SmallData, "GPU Memory Used")?.Value, 0, 1048576),
                    cpuTemp?.Name, gpuTemp?.Name, cpu?.Name ?? "CPU", gpu?.Name ?? "GPU unavailable", fans,
                    fanSource, fanStatus, msiRaw,
                    errors.Count > 0 ? string.Join("; ", errors) : null));
                if (tick++ % 30 == 0)
                {
                    var report = new StringBuilder("Cyborg Vitals sensor diagnostics\r\n").AppendLine(DateTimeOffset.Now.ToString("O"));
                    report.AppendLine("Fan source: " + fanSource).AppendLine("Fan status: " + fanStatus);
                    if (msiRaw != null) report.AppendLine("MSI Get_Fan raw: " + msiRaw);
                    foreach (var fan in fans) report.AppendLine($"Fan | {fan.name} | {fan.rpm} RPM | {fan.id}");
                    foreach (var hardware in all)
                    {
                        report.AppendLine(hardware.HardwareType + " | " + hardware.Name);
                        foreach (var sensor in hardware.Sensors)
                            report.AppendLine($"  {sensor.SensorType} | {sensor.Name} | {sensor.Value?.ToString(CultureInfo.InvariantCulture) ?? "N/A"} | {sensor.Identifier}");
                    }
                    foreach (var error in errors) report.AppendLine(error);
                    try { AtomicWrite("sensors.txt", report.ToString()); } catch (Exception e) { LogError("Writing sensor diagnostics: " + e); }
                }
                try { await Task.Delay(3000, stop.Token); } catch (OperationCanceledException) { break; }
            }
        }
        catch (Exception e)
        {
            LogError("Hardware sensors: " + e);
            Volatile.Write(ref sensorState, new SensorState(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null, null, null, null,
                null, null, "CPU", "GPU", Array.Empty<FanState>(), "Unavailable", "Hardware sensor worker failed.", null,
                e.GetType().Name + ": " + e.Message));
        }
        finally { try { computer?.Close(); } catch (Exception e) { LogError("Closing sensors: " + e); } }
    }
    private void LogError(string message)
    {
        try
        {
            string path = Path.Combine(output, "collector-errors.log");
            if (File.Exists(path) && new FileInfo(path).Length > 65536) File.WriteAllText(path, "");
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private void AtomicWrite(string filename, string text)
    {
        string target = Path.Combine(output, filename), temp = target + ".tmp";
        File.WriteAllText(temp, text, new UTF8Encoding(false));
        File.Move(temp, target, true);
    }

    private static void UpdateHardware(IHardware hardware, List<IHardware> all, List<string> errors)
    {
        try { hardware.Update(); all.Add(hardware); }
        catch (Exception e) { errors.Add(hardware.Name + ": " + e.Message); }
        foreach (var child in hardware.SubHardware) UpdateHardware(child, all, errors);
    }

    private static int GpuPriority(HardwareType type) => type switch {
        HardwareType.GpuNvidia => 0, HardwareType.GpuAmd => 1, HardwareType.GpuIntel => 2, _ => 99
    };

    private static ISensor? Sensor(IHardware? device, SensorType type, string name)
        => device?.Sensors.FirstOrDefault(s => s.SensorType == type && s.Value.HasValue
            && string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    private static ISensor? CpuTemperature(IHardware? cpu)
    {
        var temps = cpu?.Sensors.Where(s => s.SensorType == SensorType.Temperature
            && Metrics.Finite(s.Value, 0, 150).HasValue).ToList();
        if (temps == null) return null;
        // Match exact package/die sensors first; retain the actual label in the snapshot.
        return temps.FirstOrDefault(s => s.Name.Equals("CPU Package", StringComparison.OrdinalIgnoreCase))
            ?? temps.FirstOrDefault(s => s.Name.Equals("Core (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase))
            ?? temps.FirstOrDefault(s => s.Name.Equals("Core (Tdie)", StringComparison.OrdinalIgnoreCase))
            ?? temps.Where(s => s.Name.StartsWith("CPU Core", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.Value).FirstOrDefault();
    }
}

internal sealed class NativeCpu
{
    private ulong idle, kernel, user;
    private bool primed;
    [StructLayout(LayoutKind.Sequential)] private struct Time { public uint low, high; public ulong Value => ((ulong)high << 32) | low; }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out Time idle, out Time kernel, out Time user);
    public double? Read()
    {
        if (!GetSystemTimes(out var i, out var k, out var u)) return null;
        var value = primed ? Metrics.CpuPercent(idle, kernel, user, i.Value, k.Value, u.Value) : null;
        idle = i.Value; kernel = k.Value; user = u.Value; primed = true;
        return value;
    }
}

internal static class NativeMemory
{
    [StructLayout(LayoutKind.Sequential)] private struct Status {
        public uint length, memoryLoad;
        public ulong totalPhysical, availablePhysical, totalPageFile, availablePageFile,
            totalVirtual, availableVirtual, availableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref Status status);
    public static (double? used, double? total) Read()
    {
        var s = new Status { length = (uint)Marshal.SizeOf<Status>() };
        if (!GlobalMemoryStatusEx(ref s) || s.totalPhysical == 0 || s.availablePhysical > s.totalPhysical)
            return (null, null);
        const double gib = 1073741824.0;
        return ((s.totalPhysical - s.availablePhysical) / gib, s.totalPhysical / gib);
    }
}
