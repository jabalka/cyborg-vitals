# Cyborg Vitals 1.2.0.7 — alert controls and guided npm setup

The complete installation/upload guide is [README.md](README.md). The npm package version is 1.2.7; the Windows manifest version is 1.2.0.7.

- Alert fields have adjacent minus/plus RepeatButtons: click for one step, hold for repetition after 400 ms at 70 ms intervals. Usage steps by 1%, temperatures by 1 displayed degree and fans by 100 RPM, within existing limits.
- Keyboard fields select their contents on focus. Enter or leaving the field saves a valid value; Up/Down step it. Empty, nonnumeric and nonfinite inputs are rejected without saving. Blink changes save immediately; Save thresholds and Defaults remain available.
- The settings page now receives the widget context for its visibility lifecycle. Full keyboard/pointer behavior still needs Windows verification; source checks alone cannot prove the earlier input failure is resolved.
- npm supplies a command-line wrapper with no runtime JS dependencies or lifecycle installation hooks. Run the explicit setup command to get interactive prerequisite checks and consent for missing Microsoft build tools. Existing SDKs/components are reused.
- No GitHub repository exists yet. README walks through uploading the project root, tagging v1.2.7, and installing it with npm from that repository on another PC. No npm registry publication is required.

Current local setup in Command Prompt after extracting the ZIP:

```cmd
cd /d "%USERPROFILE%\Desktop\CyborgVitals-1.2.0.7\CyborgVitals"
npm run setup
```

Without Node, run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1` from the same folder. Normal Windows administrator approval and Developer Mode are retained. Every source-build destination still needs the documented VS 2022 UWP tools and a suitable SDK. Read [VALIDATION.md](VALIDATION.md) before interpreting portable checks as a Windows installation test.

The previous fan fallback is included unchanged; actual MSI RPM is still pending laptop verification. Earlier implementation details follow.

# Cyborg Vitals 1.2.0.6 — MSI fan RPM fallback

## What changes

CPU/RAM continue to use Windows APIs. CPU/GPU temperatures keep their current working sensor source. When a visible fan row has no usable RPM value from LibreHardwareMonitor, the reader now tries a separate MSI ACPI WMI provider. A valid primary 0 RPM counts as a working reading and does not trigger fallback.

The initial MSI profile is restricted to the reported manufacturer Micro-Star International Co., Ltd. and model Cyborg 14 A13VF. Your class inventory confirms MSI_ACPI.Get_Fan exists; it does not yet confirm a successful RPM response. This is the first build containing that reader and needs an on-laptop check. Other brands/models retain Windows CPU/RAM and whatever temperatures/fans the general library supports. Additional vendor profiles require their own verification; MSI commands are never sent on other brands or unverified MSI models.

The MSI reader checks the class GUID, Get_Fan method ID and single active instance, then calls only Get_Fan subfeature 0 with a 32-byte request. It validates the response length/status and decodes up to four big-endian periods using 480000 / raw, as described by the Linux kernel's MSI WMI documentation. It does not call Set_Fan, set fan curves, write firmware or scrape the MSI Center window. MSI Center does not have to be open, but the existing MSI WMI provider must be available.

Zero-valued slots can represent absent channels. A slot first appears after returning a plausible nonzero RPM; it can subsequently report 0 RPM during this reader session. Until then, all-zero replies remain unavailable, rather than inventing four stopped fans. Generic Fan/Fan 1/Fan 2 labels are used until actual channel roles are known. MSI Center may round RPM differently.

MSI calls are sequential within this reader, at most one job in flight, and run only as a fallback for an enabled fan reading. The sensor loop waits at most 1.5 seconds for a job; a slow provider cannot queue additional jobs or stop the independent CPU/RAM heartbeat. WMI itself cannot be force-cancelled: an already-started query can finish after readings are hidden. Errors back off for 30 seconds and remain visible in diagnostics. No additional driver, service, login task or tray icon is installed.

## Install this complete update

Extract the ZIP into a new Desktop folder. Close Cyborg Vitals using Win+G before installing, then run these commands with Windows Extract All's default destination:

```powershell
cd "C:\Users\Jabalka\Desktop\CyborgVitals-1.2.0.6\CyborgVitals"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Stop-Monitor.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
```

If the build reports an error, retain its complete output. This includes the full source, Setup.exe and startup helper; no previous source folder is needed. Existing Windows build tools are sufficient. System.Management is now an explicit NuGet reference at the same version already used by LibreHardwareMonitor; no extra Visual Studio workload is needed.

## Check the fan

1. Open Win+G > Cyborg Vitals > gear > Readings and enable Fan RPM (or your saved fan rows).
2. Use Back to readings and pin the widget. Allow the normal administrator prompt if the reader needs starting. Wait about 10 seconds.
3. Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-Readings.ps1
```

A successful fallback reports Fan source: MSI ACPI WMI and one or more RPM values. Compare them with MSI Center under a steady workload; small differences are expected from sampling and rounding. If N/A remains, send the entire report, especially Fan fallback status and MSI Get_Fan raw reply. Do not change cooling profiles just to generate a reading. To limit competing monitoring calls during the first check, note the RPM in MSI Center and close its monitoring window before checking our widget; this does not establish that all OEM service activity stops.

The previous release's transparency, width, short CPU/GPU temperature labels, icons, per-reading switches and automatic startup are included. Transparency 100% is clear; compact width can be dragged down to 145 pixels while Win+G is open.

## Validation boundary

Portable decoder/policy/metric tests and collector compilation pass. Full UWP build/registration, Game Bar rendering and actual MSI RPM have not been tested on this remote Linux environment. This is an implementation to verify on your laptop, not a confirmed fan-reading success. Firmware interface concurrency with other monitoring programs still needs practical validation; a private reader lock cannot coordinate MSI Center's separate service. RECOVERY-FIRST.md retains the scoped native Performance recovery steps.

MSI interface reference: https://docs.kernel.org/wmi/devices/msi-wmi-platform.html

## Automatic reader behaviour

Open Win+G > Widget menu > Cyborg Vitals. If CPU/GPU temperatures, GPU usage or a fan row is enabled and there is no live reader, the widget requests its packaged desktop launcher. Accept the Windows administrator prompt when shown. The launcher has no window or tray icon and exits after requesting the reader; the reader remains the same background process as before.

Readings > **Start sensor reader with this widget** is on by default because automatic startup was requested. It can be turned off. **Start sensors / retry** lets you retry after cancelling UAC or stopping the reader. A fresh existing reader is reused. A stopped reader's last heartbeat is not treated as proof that its process is still alive.

There is at most one automatic launch attempt per widget activation. Cancelling or failing approval does not cause repeated UAC prompts. Stop-Monitor.ps1 remains respected; stopping while the widget is open does not trigger an immediate automatic restart. Start sensors / retry or closing/reopening the widget allows a new request. CPU/RAM-only views do not need or request the elevated reader.

The fan-probing option is migrated from the last explicit reader mode in existing telemetry when available; a fresh profile defaults it off. The Readings tab exposes **Probe motherboard fan sensors on next reader start**. Changing that switch requires restarting the reader. The MSI fallback is separate and does not require this option. You can leave general motherboard probing off when using the MSI fallback.

The package declares runFullTrust and allowElevation solely for its own desktop startup helper. The helper runs as the normal user and uses Windows RunAs for the existing administrator reader, preserving UAC. It accepts only a fresh, bounded request with fixed booleans and starts a fixed installed executable. No login task, Windows service, registry autorun entry, or UAC bypass is added. The background reader continues to pause low-level hardware access when the widget is hidden/closed, though its process remains running until stopped or Windows exits.

This is a new Windows integration path. The helper compiled and request-validation tests passed, but actual Game Bar launch, package registration, UAC, and profile-path behaviour must still be checked on the laptop. Manual Start-Monitor.ps1 remains available if automatic startup is blocked.

## Transparency, width and labels

Open the widget gear > Display and set transparency to 100%, or click Clear in the toolbar. Use Back to readings; the settings panel itself remains opaque for readable controls. Pin the widget and dismiss Win+G to check the readings background. The appearance diagnostic should show opacity 0%, transparency 100%, applied alpha 0 and backplate visible False.

With Win+G open, drag the left/right widget edge to narrow it. Close settings or dismiss Game Bar and confirm that width is retained. With a side graph, both columns narrow proportionally. Compact removes the graph and permits the narrowest list.

The CPU/GPU usage rows retain chip/graphics icons, and the CPU/GPU temperature rows use thermometer icons and temperature units. Clicking a blue selected row again clears its highlight while preserving the graph metric.

## Diagnose only if needed

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-Readings.ps1
```

This reports version, heartbeat, actual sensors, driver status, foreground transparency, opacity/transparency readback, last UI event and the last automatic-launch result. Start-Monitor.ps1 and Stop-Monitor.ps1 continue to work for manual testing. The new fan source and its raw response are included in the report. No driver changes are made by this release. Use RECOVERY-FIRST.md if the earlier native Performance issue returns.

Primary launch API: https://learn.microsoft.com/en-us/uwp/api/windows.applicationmodel.fulltrustprocesslauncher
