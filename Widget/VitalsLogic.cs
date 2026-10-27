using System;

namespace CyborgVitals
{
    // Platform-independent so unit conversion and alert rules can be executed in tests.
    public static class VitalsLogic
    {
        public static double TransparencyToOpacity(double percent)
            => 1 - CheckedThreshold(percent, 100, 0, 100) / 100.0;
        public static byte BackgroundAlpha(double opacity)
            => double.IsNaN(opacity) || double.IsInfinity(opacity) ? (byte)0
                : (byte)Math.Round(255 * Math.Max(0, Math.Min(1, opacity)));
        public static double? CpuPercent(long oldIdle, long oldKernel, long oldUser, long idle, long kernel, long user)
        {
            if (idle < oldIdle || kernel < oldKernel || user < oldUser) return null;
            double total = (double)(kernel - oldKernel) + (user - oldUser);
            double idleDelta = idle - oldIdle;
            return total > 0 && idleDelta <= total ? (double?)(100 * (total - idleDelta) / total) : null;
        }
        public static bool IsTemperature(string metric) => metric == "cpuTemp" || metric == "gpuTemp";
        public static bool IsFan(string metric) => metric != null && metric.StartsWith("fan:", StringComparison.Ordinal);
        public static double ToFahrenheit(double celsius) => celsius * 9.0 / 5.0 + 32.0;
        public static double ToCelsius(double fahrenheit) => (fahrenheit - 32.0) * 5.0 / 9.0;
        public static double? DisplayValue(double? raw, string metric, bool fahrenheit)
            => raw.HasValue && fahrenheit && IsTemperature(metric) ? (double?)ToFahrenheit(raw.Value) : raw;
        public static bool IsAbove(double? raw, double threshold)
            => raw.HasValue && !double.IsNaN(raw.Value) && !double.IsInfinity(raw.Value) && raw.Value > threshold;
        public static double CheckedThreshold(double proposed, double fallback, double minimum, double maximum)
            => !double.IsNaN(proposed) && !double.IsInfinity(proposed) && proposed >= minimum && proposed <= maximum ? proposed : fallback;
        public static bool IsFresh(long sample, long now)
            => sample > 0 && sample >= now - 5000 && sample <= now + 5000;
    }
}
