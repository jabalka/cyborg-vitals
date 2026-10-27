namespace CyborgVitals.Collector;

// Kept independent of Windows and the sensor library so edge cases can be tested.
internal static class Metrics
{
    public static bool StopRequestMatches(int requestedPid, int actualPid, long timestamp, long startedAt, long now)
        => requestedPid > 0 && requestedPid == actualPid && timestamp > 0
            && timestamp >= startedAt
            && (double)now - timestamp >= -1000 && (double)now - timestamp <= 15000;

    public static double? CpuPercent(ulong previousIdle, ulong previousKernel,
        ulong previousUser, ulong idle, ulong kernel, ulong user)
    {
        // Windows kernel time includes idle time. A reset can occur across sleep.
        if (idle < previousIdle || kernel < previousKernel || user < previousUser)
            return null;
        double total = (double)(kernel - previousKernel) + (user - previousUser);
        double idleDelta = idle - previousIdle;
        if (total <= 0 || idleDelta > total) return null;
        return Math.Clamp(100.0 * (total - idleDelta) / total, 0, 100);
    }

    public static double? Finite(double? value, double minimum, double maximum)
        => value is double n && double.IsFinite(n) && n >= minimum && n <= maximum
            ? n : null;
}
