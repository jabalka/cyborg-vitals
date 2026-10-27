using System;
using System.Globalization;
using Microsoft.Gaming.XboxGameBar;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace CyborgVitals
{
    public sealed partial class SettingsPage : Page
    {
        private bool populating = true, fahrenheit;
        private XboxGameBarWidget widget;
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly CultureInfo culture = CultureInfo.InvariantCulture;
        public SettingsPage()
        {
            InitializeComponent();
            timer.Tick += (s, e) => RefreshDiagnostics();
            Loaded += (s, e) => {
                WidgetPreferences.Changed += PreferencesChanged;
                WidgetPreferences.FansChanged += FansChanged;
                WidgetPreferences.SettingsRequested += SettingsRequested;
                FillAppearance(); FillAlerts(); FillReadings(); SelectRequestedTab(); RefreshDiagnostics();
                if (widget == null || widget.Visible) timer.Start();
            };
            Unloaded += (s, e) => {
                timer.Stop(); WidgetPreferences.Changed -= PreferencesChanged;
                WidgetPreferences.FansChanged -= FansChanged; WidgetPreferences.SettingsRequested -= SettingsRequested;
                if (widget != null) widget.VisibleChanged -= VisibilityChanged;
            };
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            widget = e.Parameter as XboxGameBarWidget;
            if (widget != null) widget.VisibleChanged += VisibilityChanged;
        }
        private async void VisibilityChanged(XboxGameBarWidget sender, object args)
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (widget != null && widget.Visible) { timer.Start(); RefreshDiagnostics(); }
                else timer.Stop();
            }); } catch (Exception) { }
        }
        private async void PreferencesChanged()
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                bool oldUnit = fahrenheit; FillAppearance(); if (oldUnit != fahrenheit) FillAlerts();
            }); } catch (Exception) { }
        }
        private async void FansChanged()
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, FillReadings); } catch (Exception) { }
        }
        private async void SettingsRequested()
        {
            try { await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, SelectRequestedTab); } catch (Exception) { }
        }
        private void SelectRequestedTab() { SettingsTabs.SelectedIndex = WidgetPreferences.SettingsTab; }
        private void RefreshDiagnostics() { DiagnosticsText.Text = WidgetPreferences.Diagnostics; }
        private void FillAppearance()
        {
            populating = true;
            try
            {
                fahrenheit = WidgetPreferences.Bool("fahrenheit", false);
                UnitToggle.IsOn = fahrenheit; IconsToggle.IsOn = WidgetPreferences.Bool("icons", true);
                HostOpacityToggle.IsOn = WidgetPreferences.Bool("hostOpacity", false);
                OpacitySlider.IsEnabled = !HostOpacityToggle.IsOn;
                OpacitySlider.Value = 100 - WidgetPreferences.Number("backgroundOpacity", 0, 0, 1) * 100;
                TransparencyReadout.Text = OpacitySlider.Value.ToString("0", culture) + "% transparent (100% = clear). Text stays visible.";
                AutoStartToggle.IsOn = WidgetPreferences.Bool("autoStartSensors", true);
                FanProbingToggle.IsOn = WidgetPreferences.Bool("autoFanProbing", false);
                GraphToggle.IsOn = WidgetPreferences.Bool("graphV2", false);
                int position = (int)WidgetPreferences.Number("graphPosition", 1, 0, 3);
                new[] { LeftPosition, RightPosition, TopPosition, BottomPosition }[position].IsChecked = true;
            }
            finally { populating = false; }
        }
        private void IconsChanged(object sender, RoutedEventArgs e) { if (!populating) WidgetPreferences.Set("icons", IconsToggle.IsOn); }
        private void UnitsChanged(object sender, RoutedEventArgs e)
        {
            if (populating) return;
            fahrenheit = UnitToggle.IsOn; WidgetPreferences.Set("fahrenheit", fahrenheit); FillAlerts();
        }
        private void GraphChanged(object sender, RoutedEventArgs e) { if (!populating) WidgetPreferences.Set("graphV2", GraphToggle.IsOn); }
        private void OpacityModeChanged(object sender, RoutedEventArgs e)
        {
            if (populating) return;
            OpacitySlider.IsEnabled = !HostOpacityToggle.IsOn;
            WidgetPreferences.Set("hostOpacity", HostOpacityToggle.IsOn);
        }
        private void OpacityChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (populating) return;
            TransparencyReadout.Text = e.NewValue.ToString("0", culture) + "% transparent (100% = clear). Text stays visible.";
            WidgetPreferences.Set("backgroundOpacity", VitalsLogic.TransparencyToOpacity(e.NewValue));
        }
        private void AutoStartChanged(object sender, RoutedEventArgs e) { if (!populating) WidgetPreferences.Set("autoStartSensors", AutoStartToggle.IsOn); }
        private void FanProbingChanged(object sender, RoutedEventArgs e) { if (!populating) WidgetPreferences.Set("autoFanProbing", FanProbingToggle.IsOn); }
        private void StartSensors(object sender, RoutedEventArgs e) { WidgetPreferences.RequestSensors(); }
        private void PositionChanged(object sender, RoutedEventArgs e)
        { if (!populating) WidgetPreferences.Set("graphPosition", double.Parse((string)((RadioButton)sender).Tag, culture)); }
        private void FitClicked(object sender, RoutedEventArgs e) { WidgetPreferences.Notify(); }
        private void ClearBackground(object sender, RoutedEventArgs e) { WidgetPreferences.ClearBackground(); FillAppearance(); }
        private void FillReadings()
        {
            VisibilityOptions.Children.Clear();
            AddReading("cpu", "CPU usage"); AddReading("ram", "RAM usage");
            AddReading("cpuTemp", "CPU temperature"); AddReading("gpu", "GPU usage"); AddReading("gpuTemp", "GPU temperature");
            var fans = WidgetPreferences.Fans;
            if (fans.Length == 0) AddReading("fan:unavailable", "Fan RPM (when supported)");
            else foreach (var fan in fans) AddReading(fan.Key, fan.Caption);
        }
        private void AddReading(string key, string caption)
        {
            var check = new CheckBox { Tag = key, Content = caption, IsChecked = WidgetPreferences.Visible(key), MinHeight = 32 };
            check.Checked += ReadingChanged; check.Unchecked += ReadingChanged;
            VisibilityOptions.Children.Add(check);
        }
        private void ReadingChanged(object sender, RoutedEventArgs e)
        {
            var check = (CheckBox)sender; string key = (string)check.Tag;
            bool visible = check.IsChecked == true;
            var s = ApplicationData.Current.LocalSettings.Values;
            s["show:" + key] = visible;
            if (key == "fan:unavailable") s["fansVisible"] = visible;
            WidgetPreferences.Notify();
        }
        private void FillAlerts()
        {
            bool previous = populating; populating = true;
            try {
            UsageInput.Text = WidgetPreferences.Number("usageThreshold", 85, 1, 100).ToString("0.#", culture);
            CpuThresholdInput.Header = "CPU temperature (" + (fahrenheit ? "°F" : "°C") + ")";
            GpuThresholdInput.Header = "GPU temperature (" + (fahrenheit ? "°F" : "°C") + ")";
            CpuThresholdInput.Text = VitalsLogic.DisplayValue(WidgetPreferences.Number("cpuThresholdC", 90, 30, 120), "cpuTemp", fahrenheit).Value.ToString("0.##", culture);
            GpuThresholdInput.Text = VitalsLogic.DisplayValue(WidgetPreferences.Number("gpuThresholdC", 85, 30, 110), "gpuTemp", fahrenheit).Value.ToString("0.##", culture);
            FanThresholdInput.Text = WidgetPreferences.Number("fanThresholdRpm", 3500, 500, 30000).ToString("0", culture);
            BlinkToggle.IsOn = WidgetPreferences.Bool("blink", true); SettingsError.Text = "";
            } finally { populating = previous; }
        }
        private bool Parse(string text, out double value)
            => AlertValues.TryParse(text, CultureInfo.CurrentCulture, out value);
        private TextBox ThresholdInput(string name)
        {
            switch (name) {
                case "UsageInput": return UsageInput;
                case "CpuThresholdInput": return CpuThresholdInput;
                case "GpuThresholdInput": return GpuThresholdInput;
                case "FanThresholdInput": return FanThresholdInput;
                default: return null;
            }
        }
        private void ThresholdRange(TextBox input, out string key, out double minimum, out double maximum, out double step, out double saved)
        {
            if (input == UsageInput) { key = "usageThreshold"; minimum = 1; maximum = 100; step = 1; saved = WidgetPreferences.Number(key, 85, minimum, maximum); }
            else if (input == FanThresholdInput) { key = "fanThresholdRpm"; minimum = 500; maximum = 30000; step = 100; saved = WidgetPreferences.Number(key, 3500, minimum, maximum); }
            else {
                bool cpu = input == CpuThresholdInput;
                key = cpu ? "cpuThresholdC" : "gpuThresholdC";
                minimum = 30; maximum = cpu ? 120 : 110; step = 1;
                saved = WidgetPreferences.Number(key, cpu ? 90 : 85, minimum, maximum);
                if (fahrenheit) { minimum = VitalsLogic.ToFahrenheit(minimum); maximum = VitalsLogic.ToFahrenheit(maximum); saved = VitalsLogic.ToFahrenheit(saved); }
            }
        }
        private bool CommitThreshold(TextBox input)
        {
            string key; double minimum, maximum, step, saved, value;
            ThresholdRange(input, out key, out minimum, out maximum, out step, out saved);
            if (!Parse(input.Text, out value) || value < minimum || value > maximum) {
                SettingsError.Text = "Enter a value from " + minimum.ToString("0.##", culture) + " to " + maximum.ToString("0.##", culture) + ".";
                return false;
            }
            bool temperature = input == CpuThresholdInput || input == GpuThresholdInput;
            WidgetPreferences.Set(key, temperature && fahrenheit ? VitalsLogic.ToCelsius(value) : value);
            SettingsError.Text = "Saved.";
            return true;
        }
        private void Adjust(TextBox input, int direction)
        {
            string key; double minimum, maximum, step, saved, value;
            ThresholdRange(input, out key, out minimum, out maximum, out step, out saved);
            if (!Parse(input.Text, out value)) value = saved;
            input.Text = AlertValues.Step(value, minimum, maximum, step, direction).ToString("0.##", culture);
            CommitThreshold(input);
        }
        private void AdjustThreshold(object sender, RoutedEventArgs e)
        {
            if (populating) return;
            var parts = ((string)((RepeatButton)sender).Tag).Split(':');
            var input = ThresholdInput(parts[0]);
            if (input != null) Adjust(input, parts[1] == "-" ? -1 : 1);
        }
        private void ThresholdFocused(object sender, RoutedEventArgs e) { ((TextBox)sender).SelectAll(); }
        private void ThresholdLostFocus(object sender, RoutedEventArgs e) { if (!populating) CommitThreshold((TextBox)sender); }
        private void ThresholdKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var input = (TextBox)sender;
            if (e.Key == Windows.System.VirtualKey.Enter) { CommitThreshold(input); e.Handled = true; }
            else if (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down) {
                Adjust(input, e.Key == Windows.System.VirtualKey.Up ? 1 : -1); e.Handled = true;
            }
        }
        private void BlinkChanged(object sender, RoutedEventArgs e) { if (!populating) WidgetPreferences.Set("blink", BlinkToggle.IsOn); }
        private void AlertTabChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsTabs != null && SettingsTabs.SelectedIndex == 2 && UsageInput != null) UsageInput.Focus(FocusState.Programmatic);
        }
        private void SaveAlerts(object sender, RoutedEventArgs e)
        {
            double usage, cpu, gpu, fan;
            if (!Parse(UsageInput.Text, out usage) || !Parse(CpuThresholdInput.Text, out cpu)
                || !Parse(GpuThresholdInput.Text, out gpu) || !Parse(FanThresholdInput.Text, out fan))
            { SettingsError.Text = "Enter a number in every threshold field."; return; }
            if (fahrenheit) { cpu = VitalsLogic.ToCelsius(cpu); gpu = VitalsLogic.ToCelsius(gpu); }
            if (double.IsNaN(usage) || double.IsNaN(cpu) || double.IsNaN(gpu) || double.IsNaN(fan)
                || usage < 1 || usage > 100 || cpu < 30 || cpu > 120 || gpu < 30 || gpu > 110 || fan < 500 || fan > 30000)
            { SettingsError.Text = "Usage: 1–100%. CPU: " + (fahrenheit ? "86–248°F" : "30–120°C")
                + ". GPU: " + (fahrenheit ? "86–230°F" : "30–110°C") + ". Fan: 500–30,000 RPM."; return; }
            PersistAlerts(usage, cpu, gpu, fan, BlinkToggle.IsOn); SettingsError.Text = "Saved.";
        }
        private void PersistAlerts(double usage, double cpu, double gpu, double fan, bool blink)
        {
            var s = ApplicationData.Current.LocalSettings.Values;
            s["usageThreshold"] = usage; s["cpuThresholdC"] = cpu; s["gpuThresholdC"] = gpu;
            s["fanThresholdRpm"] = fan; s["blink"] = blink; WidgetPreferences.Notify();
        }
        private void ResetAlerts(object sender, RoutedEventArgs e) { PersistAlerts(85, 90, 85, 3500, true); FillAlerts(); }
    }
}
