using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;

namespace CyborgVitals
{
    internal static class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref uint length, StringBuilder name);
        [STAThread]
        private static int Main()
        {
            string output = null;
            try
            {
                uint length = 0;
                GetCurrentPackageFamilyName(ref length, null);
                if (length == 0 || length > 256) return 1;
                var family = new StringBuilder((int)length);
                if (GetCurrentPackageFamilyName(ref length, family) != 0 || !family.ToString().StartsWith("CyborgVitals_", StringComparison.Ordinal)) return 1;
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                output = Path.Combine(local, "Packages", family.ToString(), "LocalState");
                using (var mutex = new Mutex(false, @"Local\CyborgVitalsLaunchBridge"))
                {
                    bool held = false;
                    try
                    {
                        try { held = mutex.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                        if (!held) return 0;
                        string request = Path.Combine(output, "reader-launch.txt");
                        bool fans;
                        if (!File.Exists(request) || new FileInfo(request).Length > 1024
                            || !LaunchPolicy.ReadRequest(File.ReadAllLines(request), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), out fans))
                            throw new InvalidOperationException("No fresh, valid request from this widget.");
                        string telemetry = Path.Combine(output, "telemetry.json");
                        if (ReaderAlive(telemetry))
                        { Status(output, "Reader already connected."); return 0; }
                        string exe = Path.Combine(local, "CyborgVitals", "dist", "Collector", "CyborgVitals.Collector.exe");
                        if (!File.Exists(exe)) throw new FileNotFoundException("The installed sensor reader is missing. Run Build-And-Install.ps1.", exe);
                        Status(output, "Requesting Windows administrator approval for the sensor reader.");
                        var info = new ProcessStartInfo(exe, "--output \"" + output + "\" --hardware-sensors" + (fans ? " --motherboard-fans" : ""))
                        { UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(exe) };
                        using (var process = Process.Start(info))
                        { Status(output, "Reader launch accepted" + (process == null ? "." : "; PID " + process.Id + ".")); }
                        return 0;
                    }
                    finally { if (held) mutex.ReleaseMutex(); }
                }
            }
            catch (Win32Exception e)
            {
                if (output != null) Status(output, e.NativeErrorCode == 1223 ? "Administrator approval cancelled. Use Start sensors to try again." : "Launch failed: " + e.NativeErrorCode + " / " + e.Message);
                return 1;
            }
            catch (Exception e) { if (output != null) Status(output, "Launch failed: " + e.GetType().Name + " / " + e.Message); return 1; }
        }
        private static void Status(string output, string message)
        {
            try { File.WriteAllText(Path.Combine(output, "reader-launch-status.txt"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) + "\n" + message, new UTF8Encoding(false)); }
            catch { }
        }
        private static bool ReaderAlive(string telemetry)
        {
            try
            {
                if (!File.Exists(telemetry) || new FileInfo(telemetry).Length > 65536
                    || (DateTime.UtcNow - File.GetLastWriteTimeUtc(telemetry)).TotalSeconds >= 5) return false;
                var match = Regex.Match(File.ReadAllText(telemetry), "\\\"readerProcessId\\\"\\s*:\\s*([0-9]+)");
                int id;
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out id)) return false;
                using (var process = Process.GetProcessById(id))
                using (var self = Process.GetCurrentProcess())
                    return process.ProcessName == "CyborgVitals.Collector" && process.SessionId == self.SessionId && !process.HasExited;
            }
            catch { return false; }
        }
    }
}
