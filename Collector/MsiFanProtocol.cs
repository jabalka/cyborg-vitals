namespace CyborgVitals.Collector;

// Pure decoding/policy, separate from WMI so malformed replies can be tested.
internal static class MsiFanProtocol
{
    public static bool SupportsModel(string? manufacturer, string? model)
        => string.Equals(manufacturer?.Trim(), "Micro-Star International Co., Ltd.", StringComparison.OrdinalIgnoreCase)
            && string.Equals(model?.Trim(), "Cyborg 14 A13VF", StringComparison.OrdinalIgnoreCase);

    public static bool NeedsFallback(bool fansRequested, IEnumerable<double?> primaryRpm)
        => fansRequested && !primaryRpm.Any(rpm => Metrics.Finite(rpm, 0, 30000).HasValue);

    public static double?[] Decode(byte[]? bytes)
    {
        if (bytes == null || bytes.Length != 32 || bytes[0] == 0)
            throw new InvalidDataException("MSI Get_Fan returned an invalid length or failure status.");
        var rpm = new double?[4];
        for (int channel = 0; channel < rpm.Length; channel++)
        {
            int offset = 1 + channel * 2;
            int raw = (bytes[offset] << 8) | bytes[offset + 1];
            // Zero slots can mean absent sensors; do not manufacture four stopped fans.
            // A channel is exposed at zero only after this reader has seen it spinning.
            rpm[channel] = raw == 0 ? 0 : raw == ushort.MaxValue ? null
                : Metrics.Finite(480000 / raw, 0, 30000);
        }
        return rpm;
    }
}

internal sealed class MsiFanChannels
{
    private readonly bool[] observed = new bool[4];
    public double?[] Read(byte[]? bytes)
    {
        var rpm = MsiFanProtocol.Decode(bytes);
        for (int channel = 0; channel < rpm.Length; channel++)
        {
            if (rpm[channel] > 0) observed[channel] = true;
            else if (rpm[channel] == 0 && !observed[channel]) rpm[channel] = null;
        }
        return rpm;
    }
}
