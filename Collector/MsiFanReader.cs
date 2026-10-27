using System.Management;

namespace CyborgVitals.Collector;

internal sealed record MsiFanSample(long Timestamp, double?[] Rpm, string Status, string? Raw);

// One caller (SensorLoop), at most one WMI job. There are no Set_* calls, raw
// EC accesses or firmware writes. An unresponsive WMI job is never queued twice.
internal sealed class MsiFanReader
{
    private Task<MsiFanSample>? pending;
    private long nextAttempt;
    private bool checkedModel, supported;
    private string? instancePath;
    private string modelStatus = "Not checked.";
    private readonly MsiFanChannels channels = new();
    public string Status { get; private set; } = "Not requested.";
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<MsiFanSample?> ReadAsync()
    {
        if (pending != null && !pending.IsCompleted)
        {
            Status = "MSI WMI read still pending; no additional request queued.";
            return null;
        }
        if (pending == null)
        {
            if (Now < nextAttempt) return null;
            pending = Task.Run(ReadOnce);
            if (await Task.WhenAny(pending, Task.Delay(1500)) != pending)
            {
                Status = "MSI WMI read delayed; CPU/RAM continue. Only one request is in flight.";
                return null;
            }
        }
        var completed = pending;
        pending = null;
        try
        {
            var sample = await completed;
            Status = sample.Status;
            if (!supported) nextAttempt = long.MaxValue;
            if (Now - sample.Timestamp > 5000)
            {
                Status = "Delayed MSI fan response discarded because it is stale.";
                return null;
            }
            return sample;
        }
        catch (Exception e)
        {
            nextAttempt = Now + 30000;
            Status = "MSI fan fallback unavailable (retry in 30 seconds): " + e.GetType().Name + ": " + e.Message;
            return null;
        }
    }

    private MsiFanSample ReadOnce()
    {
        if (!checkedModel)
        {
            using var searcher = Search(@"root\cimv2", "SELECT Manufacturer, Model FROM Win32_ComputerSystem");
            using var systems = searcher.Get();
            int count = 0;
            foreach (ManagementObject system in systems)
            {
                using (system)
                {
                    count++;
                    supported = MsiFanProtocol.SupportsModel(system["Manufacturer"] as string, system["Model"] as string);
                    modelStatus = supported ? "MSI Cyborg 14 A13VF" : "MSI fallback has no enabled profile for this model; general sensors remain available.";
                }
            }
            if (count != 1) { supported = false; modelStatus = "MSI fallback: computer model could not be identified uniquely."; }
            checkedModel = true;
        }
        if (!supported) return new MsiFanSample(Now, new double?[4], modelStatus, null);

        var scope = new ManagementScope(@"\\.\root\wmi");
        if (instancePath == null)
        {
            using var schema = new ManagementClass(scope, new ManagementPath("MSI_ACPI"), null);
            schema.Get();
            if (!Guid.TryParse(Convert.ToString(schema.Qualifiers["guid"].Value), out var guid)
                || guid != new Guid("ABBC0F6E-8EA1-11D1-00A0-C90629100000"))
                throw new InvalidDataException("MSI_ACPI GUID does not match the supported fan interface.");
            var method = schema.Methods["Get_Fan"];
            if (Convert.ToInt32(method.Qualifiers["WmiMethodId"].Value) != 17)
                throw new InvalidDataException("MSI Get_Fan method identifier does not match.");
            using var searcher = Search(@"root\wmi", "SELECT * FROM MSI_ACPI WHERE Active = TRUE");
            using var instances = searcher.Get();
            var paths = new List<string>();
            foreach (ManagementObject instance in instances)
                using (instance) { paths.Add(instance.Path.RelativePath); }
            if (paths.Count != 1) throw new InvalidDataException("Expected one active MSI_ACPI instance; found " + paths.Count + ".");
            instancePath = paths[0];
        }
        using var acpi = new ManagementObject(scope, new ManagementPath(instancePath), null);
        using var packageClass = new ManagementClass(scope, new ManagementPath("Package_32"), null);
        using var payload = packageClass.CreateInstance();
        if (payload == null) throw new InvalidDataException("MSI Package_32 could not be constructed.");
        payload["Bytes"] = new byte[32]; // Subfeature 0: RPM. No fan curve subfeatures.
        using var input = acpi.GetMethodParameters("Get_Fan");
        input["Data"] = payload;
        using var output = acpi.InvokeMethod("Get_Fan", input, new InvokeMethodOptions());
        using var data = output?["Data"] as ManagementBaseObject;
        var bytes = data?["Bytes"] as byte[];
        double?[] rpm;
        try { rpm = channels.Read(bytes); }
        catch (InvalidDataException e)
        {
            string raw = bytes == null ? "missing" : Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 64)));
            throw new InvalidDataException(e.Message + " Raw reply: " + raw, e);
        }
        string status = rpm.Any(value => value.HasValue)
            ? "MSI_ACPI.Get_Fan subfeature 0: live RPM (480000 / raw period)."
            : "MSI Get_Fan replied, but no spinning fan channel has been identified yet. Zero slots are not assumed to be installed fans.";
        return new MsiFanSample(Now, rpm, status, Convert.ToHexString(bytes!));
    }

    private static ManagementObjectSearcher Search(string scope, string query)
        => new(new ManagementScope(@"\\.\" + scope), new ObjectQuery(query),
            new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(2), ReturnImmediately = true });
}
