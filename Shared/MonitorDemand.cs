namespace CyborgVitals
{
    // Shared contract: the elevated reader accepts only these bounded monitoring flags.
    // A closed, hidden or unresponsive widget cannot leave hardware polling active.
    public sealed class MonitorDemand
    {
        public int SchemaVersion { get; set; }
        public long TimestampUnixMs { get; set; }
        public bool Cpu { get; set; }
        public bool Gpu { get; set; }
        public bool Fans { get; set; }

        public bool IsFresh(long now)
        {
            double age = (double)now - TimestampUnixMs;
            return SchemaVersion == 1 && TimestampUnixMs > 0 && age >= -1000 && age <= 5000;
        }
        public bool HasHardwareReadings { get { return Cpu || Gpu || Fans; } }
    }
}
