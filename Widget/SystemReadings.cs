using System;
using Windows.System.Diagnostics;

namespace CyborgVitals
{
    // CPU and RAM stay available independently of the elevated temperature reader.
    internal sealed class SystemReadings
    {
        private SystemDiagnosticInfo system;
        private long idle, kernel, user;
        private bool primed;
        public string LastError { get; private set; }
        public double? Cpu, RamUsed, RamTotal, RamPercent;
        public void Sample(bool cpuEnabled, bool memoryEnabled)
        {
            Cpu = RamUsed = RamTotal = RamPercent = null;
            try
            {
                if (!cpuEnabled && !memoryEnabled) { primed = false; return; }
                if (system == null) system = SystemDiagnosticInfo.GetForCurrentSystem();
                if (cpuEnabled)
                {
                var cpu = system.CpuUsage.GetReport();
                long nextIdle = cpu.IdleTime.Ticks, nextKernel = cpu.KernelTime.Ticks, nextUser = cpu.UserTime.Ticks;
                if (primed) Cpu = VitalsLogic.CpuPercent(idle, kernel, user, nextIdle, nextKernel, nextUser);
                idle = nextIdle; kernel = nextKernel; user = nextUser; primed = true;
                }
                else primed = false;
                if (memoryEnabled)
                {
                var memory = system.MemoryUsage.GetReport();
                if (memory.TotalPhysicalSizeInBytes > 0 && memory.AvailableSizeInBytes <= memory.TotalPhysicalSizeInBytes)
                {
                    RamTotal = memory.TotalPhysicalSizeInBytes / 1073741824.0;
                    RamUsed = (memory.TotalPhysicalSizeInBytes - memory.AvailableSizeInBytes) / 1073741824.0;
                    RamPercent = 100 * RamUsed / RamTotal;
                }
                }
                LastError = null;
            }
            catch (Exception e) { LastError = e.GetType().Name + " / 0x" + e.HResult.ToString("X8") + ": " + e.Message; }
        }
    }
}
