using System;
using System.Globalization;

namespace CyborgVitals
{
    public static class LaunchPolicy
    {
        public static bool ReadRequest(string[] lines, long now, out bool motherboardFans)
        {
            motherboardFans = false;
            long timestamp; Guid requestId;
            if (lines == null || lines.Length != 4 || lines[0] != "1"
                || !long.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp)
                || timestamp <= 0 || (double)now - timestamp < -1000 || (double)now - timestamp > 15000
                || (lines[2] != "true" && lines[2] != "false") || !Guid.TryParse(lines[3], out requestId) || requestId == Guid.Empty) return false;
            motherboardFans = lines[2] == "true";
            return true;
        }
    }
}
