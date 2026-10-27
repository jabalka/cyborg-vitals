using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Gaming.XboxGameBar;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace CyborgVitals
{
    public sealed partial class VitalsPage : Page
    {
        private XboxGameBarWidget widget;
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly List<Reading> history = new List<Reading>();
        private readonly Dictionary<string, MetricRow> rows = new Dictionary<string, MetricRow>();
        private readonly Dictionary<string, string> fanDetails = new Dictionary<string, string>();
        private readonly AlertBadge selectedBadge = new AlertBadge(16);
        private readonly SystemReadings native = new SystemReadings();
        private string lastReadError = "Sensor reader has not connected.";
        private string demandError;
        private string uiError;
        private bool settingsOpen;
        private readonly SemaphoreSlim uiStatusGate = new SemaphoreSlim(1);
        private bool autoStartSensors, autoStartHandled;
        private readonly long widgetOpenedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private long observedReaderTimestamp;
        private long launchRequestedAt;
        private bool launchStatusFinal;
        private string autoStartStatus = "Reader not requested yet.";
        private int resizing;
        private double compactWidth = 230, sideWidth = 410;
        private bool? lastDemandActive;
        private long lastDemandSent;
        private string lastDemandFlags;
        private long lastCollectorTime;
        private double backgroundOpacity = 0;
        private bool useHostOpacity;
        private int graphPosition = 1;
        private double requestedWidth, requestedHeight;
        private Reading current;
        private long lastTimestamp;
        private bool reading, graphVisible = true, fahrenheit, showIcons = true, blink = true, highlightSelected;
        private string selected = "cpu";
        private double usageThreshold = 85, cpuThreshold = 90, gpuThreshold = 85, fanThreshold = 3500;
        private readonly CultureInfo culture = CultureInfo.InvariantCulture;
        private readonly SolidColorBrush accent = new SolidColorBrush(Color.FromArgb(255, 93, 208, 244));

        public VitalsPage()
        {
            InitializeComponent();
            ReadPreferences();
            AddRow("cpu", "CPU"); AddRow("ram", "RAM"); AddRow("cpuTemp", "CPU");
            AddRow("gpu", "GPU"); AddRow("gpuTemp", "GPU"); AddRow("fan:unavailable", "Fan");
            SelectedBadgeHost.Content = selectedBadge;
            SettingsFrame.NavigationFailed += (s, e) => { e.Handled = true; SettingsFailed(e.Exception); };
            SizeChanged += WidgetSizeChanged;
            timer.Tick += async (s, e) => await Poll();
            Loaded += async (s, e) => { ApplyDisplayMode(); ApplyLayout(); if (widget == null || widget.Visible) { timer.Start(); await Poll(); } RequestFit(); };
            Unloaded += (s, e) => { timer.Stop(); WidgetPreferences.Changed -= PreferencesChanged; WidgetPreferences.SensorStartRequested -= StartSensorsRequested; DetachWidget(); StopAlerts(); };
        }
        private void ReadPreferences()
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (settings.ContainsKey("metric")) selected = settings["metric"] as string ?? "cpu";
            if (!new[] { "cpu", "ram", "cpuTemp", "gpu", "gpuTemp" }.Contains(selected) && !VitalsLogic.IsFan(selected)) selected = "cpu";
            graphVisible = SavedBool("graphV2", false);
            backgroundOpacity = SavedThreshold("backgroundOpacity", 0, 0, 1);
            useHostOpacity = SavedBool("hostOpacity", false);
            graphPosition = (int)SavedThreshold("graphPosition", 1, 0, 3);
            fahrenheit = SavedBool("fahrenheit", false);
            showIcons = SavedBool("icons", true);
            blink = SavedBool("blink", true);
            usageThreshold = SavedThreshold("usageThreshold", 85, 1, 100);
            cpuThreshold = SavedThreshold("cpuThresholdC", 90, 30, 120);
            gpuThreshold = SavedThreshold("gpuThresholdC", 85, 30, 110);
            fanThreshold = SavedThreshold("fanThresholdRpm", 3500, 500, 30000);
            highlightSelected = SavedBool("metricHighlight", false);
            autoStartSensors = SavedBool("autoStartSensors", true);
            compactWidth = SavedThreshold("compactWidth", 230, 145, 850);
            sideWidth = SavedThreshold("sideWidth", 410, 300, 850);
        }
        private bool SavedBool(string key, bool fallback)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value) && value is bool ? (bool)value : fallback;
        }
        private double SavedThreshold(string key, double fallback, double minimum, double maximum)
        {
            object value;
            return ApplicationData.Current.LocalSettings.Values.TryGetValue(key, out value) && value is double
                ? VitalsLogic.CheckedThreshold((double)value, fallback, minimum, maximum) : fallback;
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            widget = e.Parameter as XboxGameBarWidget;
            if (widget != null)
            {
                widget.RequestedOpacityChanged += OpacityChanged;
                widget.GameBarDisplayModeChanged += DisplayModeChanged;
                widget.VisibleChanged += WidgetVisibilityChanged;
            }
            WidgetPreferences.Changed += PreferencesChanged;
            WidgetPreferences.SensorStartRequested += StartSensorsRequested;
            ApplyOpacity();
        }
        private void DetachWidget()
        {
            if (widget != null)
            {
                widget.RequestedOpacityChanged -= OpacityChanged;
                widget.GameBarDisplayModeChanged -= DisplayModeChanged;
                widget.VisibleChanged -= WidgetVisibilityChanged;
            }
            widget = null;
        }
        private void StopAlerts()
        {
            selectedBadge.Stop();
            foreach (var row in rows.Values) row.Badge.Stop();
        }
        private async void OpacityChanged(XboxGameBarWidget sender, object args)
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ApplyOpacity); }
            catch (Exception) { /* Window closed while dispatching. */ }
        }
        private void ApplyOpacity()
        {
            double opacity = useHostOpacity && widget != null ? widget.RequestedOpacity : backgroundOpacity;
            opacity = Math.Max(0, Math.Min(1, opacity > 1 ? opacity / 100 : opacity));
            Background = new SolidColorBrush(Colors.Transparent);
            if (Frame != null) Frame.Background = new SolidColorBrush(Colors.Transparent);
            // Remove the visual at zero; use brush alpha rather than a retained opacity layer.
            byte alpha = VitalsLogic.BackgroundAlpha(opacity);
            Backplate.Background = new SolidColorBrush(Color.FromArgb(alpha, 22, 24, 30));
            Backplate.Visibility = alpha == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (!settingsOpen)
            {
                var status = new JsonObject {
                    ["timestampUnixMs"] = JsonValue.CreateNumberValue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                    ["configuredOpacityPercent"] = JsonValue.CreateNumberValue(backgroundOpacity * 100),
                    ["configuredTransparencyPercent"] = JsonValue.CreateNumberValue(100 - backgroundOpacity * 100),
                    ["useGameBarOpacity"] = JsonValue.CreateBooleanValue(useHostOpacity),
                    ["gameBarRequestedOpacityRaw"] = JsonValue.CreateNumberValue(widget == null ? 0 : widget.RequestedOpacity),
                    ["displayMode"] = JsonValue.CreateStringValue(widget == null ? "Preview" : widget.GameBarDisplayMode.ToString()),
                    ["pinned"] = JsonValue.CreateBooleanValue(widget != null && widget.Pinned),
                    ["appliedBackgroundAlpha"] = JsonValue.CreateNumberValue(alpha),
                    ["backplateVisible"] = JsonValue.CreateBooleanValue(Backplate.Visibility == Visibility.Visible),
                    ["frameBackground"] = JsonValue.CreateStringValue(BrushDescription(Frame == null ? null : Frame.Background))
                };
                _ = WriteAppearanceStatus(status.Stringify());
            }
        }
        private static string BrushDescription(Brush brush)
            => brush == null ? "none" : brush is SolidColorBrush ? ((SolidColorBrush)brush).Color.ToString() : brush.GetType().Name;
        private async Task WriteAppearanceStatus(string json)
        {
            await uiStatusGate.WaitAsync();
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("appearance-status.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception) { }
            finally { uiStatusGate.Release(); }
        }
        private void ClearBackground(object sender, RoutedEventArgs e)
        {
            WidgetPreferences.ClearBackground(); ReadPreferences(); ApplyOpacity();
        }
        private async void DisplayModeChanged(XboxGameBarWidget sender, object args)
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { ApplyDisplayMode(); RequestFit(); }); }
            catch (Exception) { }
        }
        private async void WidgetVisibilityChanged(XboxGameBarWidget sender, object args)
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (widget != null && widget.Visible) timer.Start();
                else { timer.Stop(); StopAlerts(); }
            }); } catch (Exception) { }
        }
        private void ApplyDisplayMode()
        {
            bool foreground = widget == null || widget.GameBarDisplayMode == XboxGameBarDisplayMode.Foreground;
            Toolbar.Visibility = foreground ? Visibility.Visible : Visibility.Collapsed;
            if (!foreground && settingsOpen) CloseSettingsView();
            ApplyOpacity();
        }
        private async Task Poll()
        {
            if (reading) return;
            reading = true;
            try
            {
                await PublishDemand();
                if (!rows.Keys.Any(IsReadingVisible)) return;
                native.Sample(IsReadingVisible("cpu"), IsReadingVisible("ram"));
                Reading snapshot = null;
                try
                {
                    var file = await ApplicationData.Current.LocalFolder.GetFileAsync("telemetry.json");
                    var json = JsonObject.Parse(await FileIO.ReadTextAsync(file));
                    if (json.GetNamedNumber("schemaVersion") != 1) throw new FormatException("Unsupported readings format.");
                    if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("autoFanProbing"))
                    {
                        IJsonValue priorMode;
                        if (json.TryGetValue("motherboardFanProbingEnabled", out priorMode) && priorMode.ValueType == JsonValueType.Boolean)
                            ApplicationData.Current.LocalSettings.Values["autoFanProbing"] = priorMode.GetBoolean();
                    }
                    var sensor = new Reading(json);
                    lastCollectorTime = sensor.Timestamp;
                    if (!VitalsLogic.IsFresh(sensor.Timestamp, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
                        throw new InvalidOperationException("Sensor reader data is older than five seconds. Start the reader again.");
                    snapshot = sensor;
                    lastReadError = sensor.Error;
                }
                catch (Exception e) { lastReadError = e.GetType().Name + " / 0x" + e.HResult.ToString("X8") + ": " + e.Message; }
                if (snapshot == null)
                {
                    await TryStartSensors(false);
                    await ReadLaunchStatus();
                    snapshot = new Reading();
                }
                else
                {
                    if (observedReaderTimestamp > 0 && snapshot.Timestamp > observedReaderTimestamp) autoStartHandled = true;
                    observedReaderTimestamp = snapshot.Timestamp;
                    autoStartStatus = "Sensor reader data received.";
                }
                snapshot.Cpu = native.Cpu ?? snapshot.Cpu;
                snapshot.Ram = native.RamPercent ?? snapshot.Ram;
                snapshot.RamUsed = native.RamUsed ?? snapshot.RamUsed;
                snapshot.RamTotal = native.RamTotal ?? snapshot.RamTotal;
                snapshot.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                current = snapshot; SyncFans(snapshot);
                if (lastTimestamp != 0 && (snapshot.Timestamp - lastTimestamp > 5000 || snapshot.Timestamp < lastTimestamp)) history.Clear();
                history.Add(snapshot); history.RemoveAll(x => snapshot.Timestamp - x.Timestamp > 60000); lastTimestamp = snapshot.Timestamp;
                ConnectionDot.Fill = new SolidColorBrush(string.IsNullOrWhiteSpace(lastReadError) ? Colors.LightGreen : Colors.Orange);
                ToolTipService.SetToolTip(ConnectionDot, string.IsNullOrWhiteSpace(lastReadError) ? "Readings live" : "Open diagnostics for sensor-reader status");
                Render();
            }
            finally { reading = false; WidgetPreferences.Diagnostics = "Captured " + DateTimeOffset.Now.ToString("HH:mm:ss") + "\n" + DiagnosticsReport(); }
        }
        private string DiagnosticsReport()
        {
            string age = lastCollectorTime <= 0 ? "not received" : ((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastCollectorTime) / 1000.0).ToString("0.0", culture) + " s ago";
            return "CPU/RAM API: " + (native.LastError ?? "working") + "\nSensor update: " + age
                + "\nSensor reader: " + (lastReadError ?? "connected") + "\nMonitoring request: " + (demandError ?? "sent")
                + "\nHardware: " + (current == null ? "not received" : current.MonitoringStatus ?? "not received")
                + "\nDriver: " + (current == null ? "not received" : current.DriverStatus ?? "not received")
                + "\nFan source: " + (current == null ? "not received" : current.FanSource ?? "not reported")
                + "\nFan status: " + (current == null ? "not received" : current.FanStatus ?? "not reported")
                + "\nBackground: " + (useHostOpacity ? "Game Bar" : (backgroundOpacity * 100).ToString("0", culture) + "%")
                + "\nSettings UI: " + (uiError ?? (settingsOpen ? "open" : "ready"))
                + "\nAutomatic startup: " + autoStartStatus
                + "\nData file: " + ApplicationData.Current.LocalFolder.Path + "\\telemetry.json"
                + "\nTemperatures and fan RPM need the elevated sensor reader. Run Diagnose-Readings.ps1 if they remain unavailable.";
        }
        private async Task PublishDemand()
        {
            try
            {
                bool cpu = IsReadingVisible("cpuTemp"), gpu = IsReadingVisible("gpu") || IsReadingVisible("gpuTemp");
                bool fans = rows.Keys.Any(k => VitalsLogic.IsFan(k) && IsReadingVisible(k));
                bool active = cpu || gpu || fans;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string flags = cpu.ToString() + gpu.ToString() + fans.ToString();
                // Refresh active requests every two seconds. An inactive view writes only once.
                if (lastDemandFlags == flags && (lastDemandActive == false || now >= lastDemandSent && now - lastDemandSent < 2000)) return;
                var json = new JsonObject {
                    ["SchemaVersion"] = JsonValue.CreateNumberValue(1),
                    ["TimestampUnixMs"] = JsonValue.CreateNumberValue(now),
                    ["Cpu"] = JsonValue.CreateBooleanValue(cpu),
                    ["Gpu"] = JsonValue.CreateBooleanValue(gpu),
                    ["Fans"] = JsonValue.CreateBooleanValue(fans)
                };
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("reader-demand.tmp", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, json.Stringify());
                await file.RenameAsync("reader-demand.json", NameCollisionOption.ReplaceExisting);
                lastDemandSent = now; lastDemandActive = active; lastDemandFlags = flags;
                demandError = null;
            }
            catch (Exception e) { demandError = e.GetType().Name + ": " + e.Message; }
        }
        private void AddRow(string key, string caption)
        {
            var row = new MetricRow(key, caption); row.Click += SelectMetric;
            row.ShowIcons(showIcons); rows.Add(key, row); MetricList.Children.Add(row);
            row.Visibility = IsReadingVisible(key) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void SyncFans(Reading snapshot)
        {
            if (snapshot.Fans.Count > 0 && rows.ContainsKey("fan:unavailable"))
            {
                var placeholder = rows["fan:unavailable"]; placeholder.Badge.Stop();
                MetricList.Children.Remove(placeholder); rows.Remove("fan:unavailable");
                if (selected == "fan:unavailable") selected = "fan:" + snapshot.Fans.Keys.First();
            }
            foreach (var fan in snapshot.Fans.Values)
            {
                string key = "fan:" + fan.Id;
                fanDetails[key] = fan.Hardware + " · " + fan.Name;
                if (!rows.ContainsKey(key)) { AddRow(key, FanCaption(fan, snapshot.Fans.Count)); RequestFit(); }
            }
            WidgetPreferences.UpdateFans(rows.Keys.Where(k => VitalsLogic.IsFan(k) && k != "fan:unavailable")
                .Select(k => new FanOption(k, Caption(k))).ToArray());
            // Disappearing sensors keep their row but become N/A; they are not relabelled or reused.
        }
        private string FanCaption(FanReading fan, int count)
        {
            if (fan.Name.IndexOf("cpu", StringComparison.OrdinalIgnoreCase) >= 0) return "CPU fan";
            if (fan.Name.IndexOf("gpu", StringComparison.OrdinalIgnoreCase) >= 0) return "GPU fan";
            if (count == 1) return "Fan";
            return fan.Name;
        }
        private string Caption(string key)
        {
            if (key == "cpu") return "CPU";
            if (key == "ram") return "RAM";
            if (key == "cpuTemp") return "CPU";
            if (key == "gpu") return "GPU";
            if (key == "gpuTemp") return "GPU";
            FanReading fan;
            if (current != null && current.Fans.TryGetValue(key.Substring(4), out fan)) return FanCaption(fan, current.Fans.Count);
            return key == "fan:unavailable" ? "Fan" : "Fan (offline)";
        }
        private string Format(double? n, string suffix, int decimals = 0)
            => n.HasValue ? n.Value.ToString(decimals == 0 ? "0" : "0.0", culture) + suffix : "N/A";
        private string Units(string key) => VitalsLogic.IsFan(key) ? " RPM" : VitalsLogic.IsTemperature(key) ? (fahrenheit ? "°F" : "°C") : "%";
        private double Threshold(string key) => key == "cpuTemp" ? cpuThreshold : key == "gpuTemp" ? gpuThreshold : VitalsLogic.IsFan(key) ? fanThreshold : usageThreshold;
        private string AlertTooltip(string key)
        {
            double shown = VitalsLogic.DisplayValue(Threshold(key), key, fahrenheit).Value;
            return "Alert above " + Format(shown, Units(key), VitalsLogic.IsTemperature(key) ? 1 : 0)
                + (VitalsLogic.IsFan(key) ? ". High fan speed is an activity/noise alert, not a fault diagnosis." : ". Configurable attention threshold.");
        }
        private void Render()
        {
            if (MetricList == null) return;
            UnitButton.Content = fahrenheit ? "°F" : "°C";
            var visibleKeys = rows.Keys.Where(IsReadingVisible).ToList();
            if (!visibleKeys.Contains(selected))
            {
                selected = visibleKeys.Count > 0 ? visibleKeys[0] : "cpu"; highlightSelected = false;
                var settings = ApplicationData.Current.LocalSettings.Values;
                settings["metric"] = selected; settings["metricHighlight"] = false;
            }
            foreach (var entry in rows)
            {
                string key = entry.Key;
                double? value = current == null ? null : current.Value(key);
                string detail = VitalsLogic.IsFan(key) && fanDetails.ContainsKey(key) ? fanDetails[key] + "\n" : "";
                entry.Value.Visibility = IsReadingVisible(key) ? Visibility.Visible : Visibility.Collapsed;
                if (entry.Value.Visibility == Visibility.Collapsed) { entry.Value.Badge.Stop(); continue; }
                if (key == "ram" && current != null) detail += Format(current.RamUsed, "", 1) + " / " + Format(current.RamTotal, " GiB", 1) + "\n";
                if (key == "cpuTemp" && current != null) detail += current.CpuName + " · " + (current.CpuLabel ?? "sensor unavailable") + "\n";
                if (key == "gpuTemp" && current != null) detail += current.GpuName + " · " + (current.GpuLabel ?? "sensor unavailable") + "\n";
                entry.Value.ShowIcons(showIcons);
                entry.Value.Update(Caption(key), Format(VitalsLogic.DisplayValue(value, key, fahrenheit), Units(key)),
                    key == selected && highlightSelected, VitalsLogic.IsAbove(value, Threshold(key)), blink, detail + AlertTooltip(key));
            }
            var visible = MetricList.Children.OfType<MetricRow>().Where(r => r.Visibility == Visibility.Visible).ToList();
            if (!visible.Any(r => r.Metric == selected)) selected = visible.Count > 0 ? visible[0].Metric : "cpu";
            SelectedTitle.Text = Caption(selected);
            double? raw = visible.Count == 0 || current == null ? null : current.Value(selected);
            selectedBadge.Update(Format(VitalsLogic.DisplayValue(raw, selected, fahrenheit), Units(selected)),
                VitalsLogic.IsAbove(raw, Threshold(selected)), blink, AlertTooltip(selected));
            ToolTipService.SetToolTip(Chart, "60-second history · " + AlertTooltip(selected));
            DrawGraph();
        }
        private void SelectMetric(object sender, RoutedEventArgs e)
        {
            string clicked = ((MetricRow)sender).Metric;
            highlightSelected = clicked != selected || !highlightSelected;
            selected = clicked;
            ApplicationData.Current.LocalSettings.Values["metric"] = selected;
            ApplicationData.Current.LocalSettings.Values["metricHighlight"] = highlightSelected;
            Render();
        }
        private void ToggleUnits(object sender, RoutedEventArgs e)
        {
            fahrenheit = !fahrenheit;
            WidgetPreferences.Set("fahrenheit", fahrenheit);
            Render();
        }
        private void ToggleGraph(object sender, RoutedEventArgs e)
        {
            graphVisible = !graphVisible;
            WidgetPreferences.Set("graphV2", graphVisible);
            ApplyLayout(); RequestFit();
        }
        private bool IsReadingVisible(string key) => WidgetPreferences.Visible(key);
        private async void PreferencesChanged()
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                bool wasAuto = autoStartSensors; ReadPreferences(); if (!wasAuto && autoStartSensors) autoStartHandled = false;
                ApplyOpacity(); Render(); requestedWidth = requestedHeight = 0; RequestFit();
            }); } catch (Exception) { }
        }
        private async void StartSensorsRequested()
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => { autoStartHandled = false; await TryStartSensors(true); }); }
            catch (Exception e) { autoStartStatus = e.Message; }
        }
        private async Task TryStartSensors(bool explicitRequest)
        {
            if (autoStartHandled || (!autoStartSensors && !explicitRequest) || widget == null || !widget.Visible) return;
            bool hardwareWanted = IsReadingVisible("cpuTemp") || IsReadingVisible("gpu") || IsReadingVisible("gpuTemp")
                || rows.Keys.Any(k => VitalsLogic.IsFan(k) && IsReadingVisible(k));
            if (!hardwareWanted) { autoStartStatus = "Enable a temperature, GPU or fan reading to start sensors."; return; }
            if (!explicitRequest)
            {
                try
                {
                    var stopFile = await ApplicationData.Current.LocalFolder.GetFileAsync("reader-stop.json");
                    var request = JsonObject.Parse(await FileIO.ReadTextAsync(stopFile));
                    if (request.GetNamedNumber("timestampUnixMs") >= widgetOpenedAt)
                    { autoStartHandled = true; autoStartStatus = "Reader stopped by request. Use Start sensors to resume."; return; }
                }
                catch (Exception) { }
            }
            autoStartHandled = true;
            try
            {
                launchRequestedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                launchStatusFinal = false;
                string request = "1\n" + launchRequestedAt.ToString(culture) + "\n"
                    + (SavedBool("autoFanProbing", false) ? "true" : "false") + "\n" + Guid.NewGuid().ToString("N");
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("reader-launch.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, request);
                autoStartStatus = "Requesting reader startup. Accept Windows administrator approval if shown.";
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            }
            catch (Exception e) { autoStartStatus = "Startup failed: " + e.GetType().Name + " / " + e.Message; await WriteUiStatus(autoStartStatus); }
        }
        private async Task ReadLaunchStatus()
        {
            if (launchRequestedAt == 0 || launchStatusFinal) return;
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync("reader-launch-status.txt");
                string text = await FileIO.ReadTextAsync(file); int split = text.IndexOf('\n'); long timestamp;
                if (split > 0 && long.TryParse(text.Substring(0, split), out timestamp) && timestamp >= launchRequestedAt)
                {
                    autoStartStatus = text.Substring(split + 1);
                    launchStatusFinal = !autoStartStatus.StartsWith("Requesting Windows", StringComparison.Ordinal);
                }
            }
            catch (Exception) { }
        }
        private void WidgetSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (settingsOpen || resizing > 0 || !IsLoaded || requestedWidth <= 0 || e.NewSize.Width < 145 || Math.Abs(e.NewSize.Width - requestedWidth) < 2) return;
            bool side = graphVisible && graphPosition < 2 && rows.Values.Any(r => r.Visibility == Visibility.Visible);
            if (side) sideWidth = Math.Max(300, e.NewSize.Width); else compactWidth = e.NewSize.Width;
            ApplicationData.Current.LocalSettings.Values[side ? "sideWidth" : "compactWidth"] = side ? sideWidth : compactWidth;
            requestedWidth = e.NewSize.Width;
        }
        private async void OpenSettings(object sender, RoutedEventArgs e) { await ShowSettings(0); }
        private async void OpenDiagnostics(object sender, RoutedEventArgs e) { await ShowSettings(3); }
        private async Task ShowSettings(int tab)
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                    WidgetPreferences.RequestSettings(tab);
                    settingsOpen = true; RootPanel.Visibility = Visibility.Collapsed;
                    SettingsContainer.Visibility = Visibility.Visible;
                    SettingsErrorBanner.Visibility = Visibility.Collapsed; uiError = null;
                    try
                    {
                        if (SettingsFrame.Content == null && !SettingsFrame.Navigate(typeof(SettingsPage), widget))
                            throw new InvalidOperationException("The settings page could not be loaded.");
                        _ = WriteUiStatus("Settings opened in expanded widget view.");
                    }
                    catch (Exception e) { SettingsFailed(e); }
                    requestedWidth = requestedHeight = 0; RequestFit();
                });
            }
            catch (Exception e) { uiError = "Opening settings: " + e.Message; await WriteUiStatus(uiError); }
        }
        private void SettingsFailed(Exception error)
        {
            uiError = error.GetType().Name + " / 0x" + error.HResult.ToString("X8") + ": " + error.Message;
            SettingsErrorBanner.Text = "Settings error: " + uiError;
            SettingsErrorBanner.Visibility = Visibility.Visible;
            _ = WriteUiStatus(error.ToString());
        }
        private async Task WriteUiStatus(string message)
        {
            await uiStatusGate.WaitAsync();
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("widget-ui-status.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, DateTimeOffset.Now.ToString("O") + "\n" + message);
            }
            catch (Exception) { }
            finally { uiStatusGate.Release(); }
        }
        private void CloseSettings(object sender, RoutedEventArgs e) { CloseSettingsView(); }
        private void CloseSettingsView()
        {
            settingsOpen = false; SettingsContainer.Visibility = Visibility.Collapsed;
            SettingsFrame.Content = null; SettingsFrame.BackStack.Clear(); SettingsFrame.ForwardStack.Clear();
            RootPanel.Visibility = Visibility.Visible;
            ApplyOpacity();
            requestedWidth = requestedHeight = 0; RequestFit();
            _ = WriteUiStatus("Returned to compact readings view.");
        }
        private void ApplyLayout()
        {
            GraphToggle.Content = graphVisible ? "Compact" : "Graph";
            int count = rows.Values.Count(r => r.Visibility == Visibility.Visible);
            bool graph = graphVisible && count > 0;
            GraphArea.Visibility = graph ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumnSpan(MetricScroll, 1); Grid.SetColumnSpan(GraphArea, 1);
            Grid.SetRow(MetricScroll, 0); Grid.SetRow(GraphArea, 0);
            Grid.SetColumn(MetricScroll, 0); Grid.SetColumn(GraphArea, 1);
            ContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            ContentGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            ContentGrid.RowDefinitions[1].Height = new GridLength(0);
            GraphArea.Margin = new Thickness(0);
            double listHeight = Math.Min(600, count * 24);
            if (graph && graphPosition < 2)
            {
                int listCol = graphPosition == 0 ? 1 : 0, graphCol = 1 - listCol;
                Grid.SetColumn(MetricScroll, listCol); Grid.SetColumn(GraphArea, graphCol);
                ContentGrid.ColumnDefinitions[listCol].Width = new GridLength(0.46, GridUnitType.Star);
                ContentGrid.ColumnDefinitions[graphCol].Width = new GridLength(0.54, GridUnitType.Star);
                GraphArea.Margin = graphCol == 0 ? new Thickness(0, 0, 8, 0) : new Thickness(8, 0, 0, 0);
                ContentGrid.Height = Math.Max(110, listHeight);
            }
            else if (graph)
            {
                int graphRow = graphPosition == 2 ? 0 : 1, listRow = 1 - graphRow;
                Grid.SetRow(MetricScroll, listRow); Grid.SetRow(GraphArea, graphRow); Grid.SetColumn(GraphArea, 0);
                ContentGrid.RowDefinitions[listRow].Height = new GridLength(listHeight);
                ContentGrid.RowDefinitions[graphRow].Height = new GridLength(126);
                GraphArea.Margin = graphRow == 0 ? new Thickness(0, 0, 0, 6) : new Thickness(0, 6, 0, 0);
                ContentGrid.Height = listHeight + 126;
            }
            else ContentGrid.Height = listHeight;
            Render();
        }
        private async void RequestFit()
        {
            if (widget == null || ContentGrid == null) return;
            ApplyLayout();
            bool side = graphVisible && graphPosition < 2 && rows.Values.Any(r => r.Visibility == Visibility.Visible);
            double width = side ? sideWidth : compactWidth;
            double height = Math.Max(48, ContentGrid.Height + 8 + (Toolbar.Visibility == Visibility.Visible ? 28 : 0));
            if (settingsOpen) { width = 420; height = 620; }
            if (Math.Abs(width - requestedWidth) < 1 && Math.Abs(height - requestedHeight) < 1) return;
            requestedWidth = width; requestedHeight = height;
            try
            {
                resizing++;
                widget.MinWindowSize = new Size(settingsOpen ? 340 : side ? 300 : 145, settingsOpen ? 360 : 48);
                bool resized = await widget.TryResizeWindowAsync(new Size(width, height));
                if (settingsOpen && !resized && (ActualWidth < width - 1 || ActualHeight < height - 1))
                    SettingsFailed(new InvalidOperationException("Game Bar declined the settings size. Drag the widget's lower edge to enlarge it."));
            }
            catch (Exception e) { if (settingsOpen) SettingsFailed(e); }
            finally { resizing--; }
        }
        private void ChartSizeChanged(object sender, SizeChangedEventArgs e) { DrawGraph(); }
        private double GraphCeiling()
        {
            if (VitalsLogic.IsTemperature(selected)) return 120;
            if (!VitalsLogic.IsFan(selected)) return 100;
            double max = Math.Max(5000, fanThreshold * 1.2);
            foreach (var sample in history)
            { var n = sample.Value(selected); if (n.HasValue) max = Math.Max(max, n.Value * 1.1); }
            return Math.Ceiling(max / 1000) * 1000;
        }
        private void DrawGraph()
        {
            if (settingsOpen) return;
            if (Chart == null) return;
            Chart.Children.Clear();
            if (GraphArea.Visibility != Visibility.Visible) return;
            double width = Chart.ActualWidth, height = Chart.ActualHeight;
            if (width <= 0 || height <= 0 || history.Count == 0) return;
            Chart.Clip = new RectangleGeometry { Rect = new Rect(0, 0, width, height) };
            double ceiling = GraphCeiling();
            for (int line = 1; line <= 3; line++)
                Chart.Children.Add(new Line { X1 = 0, X2 = width, Y1 = height * line / 4,
                    Y2 = height * line / 4, Stroke = new SolidColorBrush(Color.FromArgb(90, 67, 83, 100)), StrokeThickness = 1 });
            double thresholdY = height * (1.0 - Threshold(selected) / ceiling);
            if (thresholdY >= 0 && thresholdY <= height)
                Chart.Children.Add(new Line { X1 = 0, X2 = width, Y1 = thresholdY, Y2 = thresholdY,
                    Stroke = new SolidColorBrush(Color.FromArgb(155, 255, 96, 108)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 3, 3 } });
            Polyline segment = null;
            long latest = history[history.Count - 1].Timestamp, previous = 0;
            foreach (var sample in history)
            {
                var n = sample.Value(selected);
                if (previous != 0 && sample.Timestamp - previous > 2500) segment = null;
                previous = sample.Timestamp;
                if (!n.HasValue) { segment = null; continue; }
                if (segment == null) { segment = new Polyline { Stroke = accent, StrokeThickness = 1.5 }; Chart.Children.Add(segment); }
                double x = width * (1.0 - (latest - sample.Timestamp) / 60000.0);
                double y = height * (1.0 - Math.Max(0, Math.Min(ceiling, n.Value)) / ceiling);
                segment.Points.Add(new Point(x, y));
            }
        }
        private sealed class FanReading
        {
            public string Id, Name, Hardware;
            public double? Rpm;
        }
        private sealed class Reading
        {
            public long Timestamp;
            public double? Cpu, Ram, CpuTemp, Gpu, GpuTemp, RamUsed, RamTotal;
            public string CpuName = "CPU", GpuName = "GPU", CpuLabel, GpuLabel, Error, DriverStatus, MonitoringStatus, FanSource, FanStatus;
            public readonly Dictionary<string, FanReading> Fans = new Dictionary<string, FanReading>();
            public Reading() { }
            public Reading(JsonObject j)
            {
                Timestamp = (long)j.GetNamedNumber("timestampUnixMs");
                Cpu = Number(j, "cpuUsagePercent", 100); Ram = Number(j, "ramUsagePercent", 100);
                CpuTemp = Number(j, "cpuTemperatureC", 150); GpuTemp = Number(j, "gpuTemperatureC", 150);
                Gpu = Number(j, "gpuUsagePercent", 100); RamUsed = Number(j, "ramUsedGiB", 1048576); RamTotal = Number(j, "ramTotalGiB", 1048576);
                CpuName = Text(j, "cpuName") ?? "CPU"; GpuName = Text(j, "gpuName") ?? "GPU";
                CpuLabel = Text(j, "cpuTemperatureLabel"); GpuLabel = Text(j, "gpuTemperatureLabel"); Error = Text(j, "sensorError");
                DriverStatus = Text(j, "sensorDriverStatus"); MonitoringStatus = Text(j, "hardwareMonitoringStatus");
                FanSource = Text(j, "fanSource"); FanStatus = Text(j, "fanStatus");
                IJsonValue a;
                if (j.TryGetValue("fans", out a) && a.ValueType == JsonValueType.Array)
                    foreach (var item in a.GetArray())
                    {
                        if (item.ValueType != JsonValueType.Object) continue;
                        var f = item.GetObject(); var id = Text(f, "id");
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        Fans[id] = new FanReading { Id = id, Name = Text(f, "name") ?? "Fan",
                            Hardware = Text(f, "hardwareName") ?? "Hardware", Rpm = Number(f, "rpm", 30000) };
                    }
            }
            private static double? Number(JsonObject j, string key, double maximum)
            {
                IJsonValue v;
                if (!j.TryGetValue(key, out v) || v.ValueType != JsonValueType.Number) return null;
                double n = v.GetNumber();
                return !double.IsNaN(n) && !double.IsInfinity(n) && n >= 0 && n <= maximum ? (double?)n : null;
            }
            private static string Text(JsonObject j, string key)
            { IJsonValue v; return j.TryGetValue(key, out v) && v.ValueType == JsonValueType.String ? v.GetString() : null; }
            public double? Value(string key)
            {
                if (VitalsLogic.IsFan(key)) { FanReading fan; return Fans.TryGetValue(key.Substring(4), out fan) ? fan.Rpm : null; }
                return key == "ram" ? Ram : key == "cpuTemp" ? CpuTemp : key == "gpu" ? Gpu : key == "gpuTemp" ? GpuTemp : Cpu;
            }
        }
    }
}
