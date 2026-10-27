using System;
using System.Linq;
using Windows.Storage;

namespace CyborgVitals
{
    public sealed class FanOption
    {
        public string Key { get; private set; }
        public string Caption { get; private set; }
        public FanOption(string key, string caption) { Key = key; Caption = caption; }
    }

    // Notifications cross UI views only after subscribers dispatch to their own UI thread.
    public static class WidgetPreferences
    {
        private static readonly object gate = new object();
        private static FanOption[] fans = new FanOption[0];
        public static event Action Changed;
        public static event Action FansChanged;
        public static event Action SettingsRequested;
        public static event Action SensorStartRequested;
        public static int SettingsTab { get; private set; }
        public static volatile string Diagnostics = "Waiting for the monitoring widget. Keep it visible while diagnosing.";
        public static FanOption[] Fans { get { lock (gate) { return (FanOption[])fans.Clone(); } } }
        public static void UpdateFans(FanOption[] options)
        {
            lock (gate)
            {
                if (fans.Select(x => x.Key + x.Caption).SequenceEqual(options.Select(x => x.Key + x.Caption))) return;
                fans = options;
            }
            var handler = FansChanged; if (handler != null) handler();
        }
        public static void RequestSettings(int tab)
        {
            SettingsTab = tab;
            var handler = SettingsRequested; if (handler != null) handler();
        }
        public static bool Bool(string key, bool fallback)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }
        public static double Number(string key, double fallback, double min, double max)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value) && value is double
                ? VitalsLogic.CheckedThreshold((double)value, fallback, min, max) : fallback;
        }
        public static bool Visible(string key)
            => Bool("show:" + key, VitalsLogic.IsFan(key) ? Bool("fansVisible", false) : key == "cpu" || key == "ram");
        public static void Set(string key, object value)
        {
            ApplicationData.Current.LocalSettings.Values[key] = value;
            Notify();
        }
        public static void Notify() { var handler = Changed; if (handler != null) handler(); }
        public static void RequestSensors() { var handler = SensorStartRequested; if (handler != null) handler(); }
        public static void ClearBackground()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            settings["hostOpacity"] = false; settings["backgroundOpacity"] = 0.0;
            Notify();
        }
    }
}
