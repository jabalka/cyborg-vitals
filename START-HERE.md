# Start here

For step-by-step setup, GitHub upload, npm installation and required tools, use [README.md](README.md). Version 1.2.0.7 adds editable alert controls and a guided npm/source installer.

# Cyborg Vitals 1.2.0.7

The reader now runs in the background with no Hidden Icons entry. Stop it through Setup > Stop reader or Stop-Monitor.ps1.

Version 1.2.0.7 adds automatic reader startup through a packaged desktop helper, a transparency control with 100% = clear, remembered narrow widths, and short CPU/GPU temperature labels. Windows UAC approval is still required when the elevated reader starts. The package adds runFullTrust / allowElevation for this helper; no service or login task is created. See UPDATE-1.2.md for the exact behaviour and Windows checks.

CPU/RAM and both CPU/GPU temperatures are confirmed live with PawnIO installed. The latest dark background was saved at 97% opacity, which is nearly solid; Clear removes it. No supported fan RPM sensor is exposed by the current library on the laptop.

A compact Xbox Game Bar widget for CPU/RAM/GPU use, CPU/GPU temperatures and supported fan RPM sensors. Start with **UPDATE-1.2.md** for the current, staged installation commands and controls.

The original Performance widget opens again after the user's restart. The earlier failure's cause is still unconfirmed. This update preserves the requested appearance controls while making hardware monitoring opt-in and dependent on enabled, visible readings. Live Windows compatibility and resource use still need testing on the laptop.

## Features

- Dense icon / label / right-aligned value rows, with optional icons.
- Individual reading switches, saved automatically.
- CPU/GPU temperature in Celsius or Fahrenheit; actual sensor names in tooltips.
- One row per supported RPM sensor; individual fan visibility settings.
- Transparent background by default, a 0–100% background-opacity slider, and optional use of Game Bar opacity. The display slider is labelled transparency: 100% is clear. Text stays readable.
- Compact list or a selected reading's 60-second graph, placed left/right/top/bottom.
- Content-height fitting with remembered manual widths, down to 145 pixels in compact mode.
- Toolbar hidden when Win+G is dismissed; no permanent footer.
- Adjustable red alert dots above thresholds, blinking or solid.
- Diagnostics for reader status, missing values and short process-resource measurements.

CPU/RAM use Windows APIs inside the widget. GPU/temperature/RPM readings need the separate elevated collector and supported hardware access; automatic launch is now available. CPU/RAM-only is the initial configuration. This is a custom widget with the requested controls; it does not extend Microsoft's built-in Performance widget or guarantee every native feature, such as FPS/game integration.

## Tools already installed on this laptop

Visual Studio 2022 with UWP C# tools, a Windows 11 SDK at least 10.0.22621.0, and a .NET SDK capable of targeting .NET 8. The user's .NET 9.0.320 and VS 2022 MSBuild 17.14 built the earlier source successfully. Developer Mode is required for local development registration. This update does not require extra C++ workloads, Copilot, IntelliCode, another SDK, or another Visual Studio installation.

The Windows tools must compile/register this version. The ZIP includes source and a compiled optional Setup.exe, rather than a prebuilt signed release package. The corrected installer extracts the generated MSIX and registers that layout, so it no longer assumes Visual Studio leaves an AppX staging directory. It can install signed Microsoft runtime dependencies supplied by the build.

## Sensor access and availability

The reader uses LibreHardwareMonitorLib 0.9.6. Some CPU/motherboard sensors need PawnIO; diagnostics detect its installation status, which does not prove the driver can load. No driver is installed automatically. Keep Windows security enabled if access is blocked and collect the actual error.

A general motherboard/fan probe is a separate, explicit option. Version 1.2.0.7 also adds an automatic MSI WMI fallback when no usable general fan RPM is present. Its first enabled profile is Cyborg 14 A13VF with matching MSI manufacturer/interface metadata. Actual RPM on the laptop is still pending verification. Other brands/models keep general sensor support; vendor-specific profiles are not assumed compatible. See UPDATE-1.2.md. The MSI path calls only Get_Fan subfeature 0 and does not scrape MSI Center or send raw EC commands.

A valid reported 0 RPM is preserved; missing RPM remains N/A. CPU Package is preferred, then supported CPU die/core temperatures. The GPU preference is NVIDIA, then AMD, then Intel. A sleeping discrete GPU may be unavailable; reading it can affect power use. GPU usage is the library's GPU Core sensor, which may differ from Task Manager's busiest engine. CPU non-idle time can also differ from Task Manager's frequency-aware CPU utility.

Hardware updates run every three seconds only for requested categories. Widget monitoring requests expire when the widget is hidden/closed, causing the reader to close its hardware session on its next check. Pinning a visible widget keeps requests active. Turning off a row hides it and, if no other reading needs that category, stops monitoring that category. Fan-only monitoring can still request GPU fan sensors. General motherboard probing additionally needs its explicit startup option; the supported MSI fallback does not.

The reader remains a background process until Stop reader or Stop-Monitor.ps1 is used. It has no tray icon by default; -ShowTrayIcon is an optional startup flag. No Windows service, scheduled task, or automatic login startup is created. Source inspection shows no fan-speed, voltage, clock or power-setting controls; it cannot establish that a hardware driver or Game Bar extension will never conflict. MSI documents possible monitoring-related EC timeouts: https://www.msi.com/faq/faq-11142 .

## Alerts

Usage >85%, CPU >90°C / 194°F, GPU >85°C / 185°F, and each fan >3,500 RPM are editable application defaults. They are attention thresholds, not hardware danger limits. Dots pulse with a 1.6-second cycle; disabling blinking makes them solid. Missing readings never trigger alerts. Conversions preserve raw Celsius comparisons. High fan speed can be normal cooling activity.

## Everyday use and removal

Open Cyborg Vitals from Start to access the optional reader controls, then open Win+G. With no reader, CPU/RAM still work. Hardware readings can request the reader automatically when enabled, subject to Windows administrator approval. The Readings tab can disable this. Command startup and troubleshooting are in UPDATE-1.2.md.

To stop sensors, use Setup > Stop reader or run Stop-Monitor.ps1. The older 1.2.0.1 reader must be exited through its tray icon once before upgrading. To remove only our widget/Start shortcut, run Remove-Widget.ps1, which first requests that our reader stop. Local runtime files are under `%LOCALAPPDATA%\CyborgVitals`; remove them manually only after exiting all our processes if no longer wanted. RECOVERY-FIRST.md describes restoring the native baseline if the previous issue returns.

## Source and checks

Widget/ contains the UWP UI; Collector/ contains the .NET 8 Windows background reader; Shared/ contains the bounded monitoring-request contract; Installer/ contains Setup.exe sources. Tests/ includes portable metric checks, PowerShell/XML checks and installer fixtures. VALIDATION.md distinguishes completed checks from pending Windows integration tests.

Communication uses local JSON files in this package's LocalState folder. There is no listener, external reporting or network capability in the widget. Initial NuGet restore/build needs internet access. The reader's sensors.txt diagnostics list detected sensor names/values locally.

Upstream: https://learn.microsoft.com/en-us/xbox/game-bar/ and https://github.com/LibreHardwareMonitor/LibreHardwareMonitor . No Microsoft or MSI endorsement is implied.
