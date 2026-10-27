# Validation record — 27 October 2026

Completed in the build workspace:

- Compiled the sensor reader's C# sources with Roslyn 4.11 against .NET 8, Windows Desktop and LibreHardwareMonitorLib 0.9.6 reference assemblies. Windows Forms' generated ApplicationConfiguration was represented by a verification stub. This was a compile check, not a Windows executable run or a successful `dotnet publish`.
- Executed 43 edge-case checks against the actual `Collector/Metrics.cs`: CPU idle/kernel/user arithmetic, busy/idle intervals, zero intervals, counter resets, inconsistent counters, missing temperature, NaN/infinity, negative sentinel values and valid sensor preservation. All passed. The added checks cover Celsius/Fahrenheit display and input conversion, strict alert boundaries, RPM zero vs unavailable, saved-threshold validation and freshness rules.
- Parsed all application C# source with Roslyn using C# 7.3 for the widget and C# 12 for the collector/tests. No syntax errors.
- Parsed all PowerShell scripts with Microsoft's PowerShell parser. No syntax errors.
- Parsed the XAML, project XML and manifests; verified named XAML fields/handlers and referenced assets are present.

Not yet verified:

- The user has confirmed the Windows UWP/XAML build and MSIX packaging succeeded. Windows registration and runtime activation with the corrected installer are still pending.
- Game Bar discovery, activation, pinning, transparency, click-through and layout at Windows display scaling settings, including optional icons, the settings flyout and alert-dot animation.
- Live CPU/RAM readings, MSI Cyborg 14 temperature sensors and motherboard/fan sensor support, including any sensor-driver permission or compatibility issue.
- Script execution on Windows PowerShell 5.1; parsing was checked with the PowerShell 7.4 parser and the scripts use Windows PowerShell-compatible constructs.
- Resource use, update rate under load, sleep/resume behaviour and fullscreen compatibility on the laptop.

An attempted cross-platform widget code check cannot replace the Windows UWP build: the complete Windows SDK union metadata/XAML toolchain is required. A Linux workspace cannot run the Game Bar host. Treat this as a first source version awaiting Windows integration validation, not a tested installer.

## Laptop acceptance check

1. Build/register using `Build-And-Install.ps1` and keep the output if it fails.
2. Start the reader and open **Cyborg Vitals** from Game Bar's Widget menu.
3. Check that CPU and RAM respond to a workload; allow at least two CPU samples after startup.
4. Compare the temperature with MSI Center, noting the exact sensor label in the widget.
5. Pin the widget, change Game Bar's pinned opacity, and test over your browser/VS Code/DaVinci Resolve.
6. Stop the reader from its tray icon. Confirm readings change to N/A within five seconds, then restart it.
7. Check Hide/Show graph, all five selections, window resizing and your normal display scaling.
8. Open the gear, switch icons off/on, close/reopen the widget and confirm the preference persists.
9. Switch °C/°F; verify readings, threshold fields and graph labels convert together.
10. Set a low temporary usage threshold, confirm red dots pulse only above it, then restore defaults. Switch blinking off and verify a solid dot; stop the reader and confirm all dots clear with stale data.
11. If fan sensors are reported, confirm each has its own row, a graph in RPM and the actual device/sensor name in the tooltip.
12. After sleep/resume, confirm current readings recover and old graph history is cleared if there was a gap.

Reproduce the platform-independent metric checks with .NET 8 SDK:

```powershell
dotnet run --project .\Tests\MetricsChecks.csproj
```

Reproduce PowerShell and XML validation:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Verify-Source.ps1
```

## Windows build feedback and packaging fix — 27 October 2026

The user built the collector successfully with .NET SDK 9.0.320 and compiled the UWP widget with Visual Studio 2022 MSBuild 17.14. Packaging then failed with APPX1673 because PhoneIdentity was missing from our source manifest. Version 1.1.0.1 adds the documented mp namespace and PhoneIdentity GUID metadata; Verify-Source.ps1 now checks it. The local manifest was parsed and the GUIDs and placement checked, but a successful Windows package/install run after this fix is still pending. Fix-Manifest.ps1 provides an idempotent in-place repair for earlier source copies.

## Deployment-layout correction and graphical setup

The user's Windows output confirms that MSBuild produced `Widget_1.1.0.1_x64_Debug.msix`. The previous installer incorrectly required a fixed AppX staging path. The updated installer selects the built MSIX by its manifest identity, version and x64 architecture, then extracts it into a stable per-user runtime folder and registers the extracted manifest. It also installs the collector/runtime start scripts and a Start shortcut. `-PreferExistingBuild` allows the user's successful output to be reused.

Setup.exe was compiled with Roslyn against .NET Framework 4.7.2 reference assemblies as a Windows GUI executable. It runs setup/start scripts with redirected output and no console window; it displays progress and stores a local log. Setup itself is not elevated; the existing sensor reader requests its own UAC elevation.

The graphical executable's actual Windows appearance, UAC flow, dependency installation, Start shortcut and development registration still require the user's Windows integration test. It is not a signed, bundled release installer. No production signing certificate is installed.

All 7 installer fixture checks were executed successfully using PowerShell 7.4.6 in the workspace. They are in `Tests/InstallerChecks.ps1`. They inspect/extract temporary synthetic MSIX ZIPs and never register an AppX package or touch installed files. These cover the exact missing-staging-layout regression, correct version/architecture selection, repeated extraction and rejection of a missing payload executable.

## Version 1.2 compact UI, reading diagnostics and setup repair

The user reported no visible readings and no setup window. The exact cause on their Windows machine is still unconfirmed; a security block has not been established.

Changes: the UWP widget samples system CPU/RAM directly, records the actual reader/file error in a diagnostics flyout, removes the footer, supports per-reading visibility and left/right/top/bottom graphs, offers its own opacity slider, and hides its toolbar using GameBarDisplayMode.PinnedOnly. It requests content-fit sizes on layout/visibility/display-mode changes. The collector publishes its heartbeat independently of hardware polling. Setup now has explicit runtime configuration, a conventional CLR startup stub and startup logging. Repair-Setup.ps1 rebuilds it with a Windows compiler already on the laptop; Test-SetupLaunch.ps1 captures Windows launch errors, exit codes and relevant Application events.

The updated collector and GUI sources compiled against their real reference assemblies. The GUI also passed a C# 5 compilation check for the framework-compiler fallback. The partial UWP metadata check reports no errors outside missing SDK union metadata for await projection; it does not replace a Windows UWP/XAML build. Forty-three metric/conversion/freshness checks and seven installer fixtures passed. PowerShell parsing and XML/manifest/config checks passed. Windows appearance, native fallback CPU/RAM behaviour, live sensors, pinned resizing and setup launch still need the user's Windows check.

## Reported native Performance-widget failure — rollout paused

The user reports the original Performance widget stopped working after custom installation and could not be re-enabled. No Windows event log or before/after resource measurements have been supplied, so cause and hardware state are unconfirmed. Current code contains no fan-control, clock, voltage or power-setting calls and does not edit built-in Game Bar files/settings; it does register an additional Game Bar extension and can load sensor-library drivers when enabled. Source inspection cannot establish absence of host/driver conflicts.

Before further installation, the recovery instructions stop the reader, remove only CyborgVitals, restart, and test the native Performance widget. The working update defaults to CPU/RAM-only display with no automatic reader startup; temperature scanning is explicit opt-in, motherboard/fan scanning is a separate opt-in, hardware polling is slowed to three seconds, hidden-widget UI polling stops and hidden graphs are not redrawn. Compatibility, host resizing and runtime overhead remain unverified. MSI's monitoring-conflict guidance is https://jp.msi.com/faq/faq-11142 .

## Baseline recovered; version 1.2.0.1 staged test

The user confirms the original Performance widget opens after a restart. This establishes baseline recovery, not the original cause or coexistence under custom monitoring. Deployment instructions now test custom host activation, temperatures and motherboard/fan access separately.

The reader now accepts fresh bounded monitoring requests from the widget. Requests expire after five seconds; hardware polling closes/pauses on the next reader check when no active request exists. Only requested CPU/GPU/motherboard categories are opened. The motherboard category additionally requires the startup opt-in. A stalled library/driver call can delay pausing. The tray process remains alive until explicitly exited. The widget sends active requests every two seconds and inactive requests only on state change.

Diagnostics now include library-reported PawnIO installation status, selected monitoring mode, CPU/GPU sensor labels, actual fan RPM and an optional five-second CPU/working-set sample of our processes (host/driver overhead excluded). A missing low-level driver and unsupported MSI laptop fans remain distinct possible limitations. No driver is installed automatically.

Executed 51 portable checks, including missing/stale/future/unknown-schema monitoring requests and CPU/RAM-only request inactivity. All passed. Recompiled the collector against real references; compiled Setup.exe as AnyCPU against .NET Framework 4.7.2, including the C# 5 fallback check. Seven package-selection/extraction fixtures and PowerShell/XML checks pass. The widget code check remains limited by missing Windows SDK union metadata for await projection; no other compile diagnostics were reported. Full version 1.2.0.1 Windows UWP/XAML build, request-file access, actual hardware-session pausing, live temperatures/fans, GUI startup, native Performance coexistence, opacity/layout and runtime load still need laptop validation.

## Version 1.2.0.2 — feedback from the laptop and display fixes

User-supplied 1.2.0.1 diagnostics confirm native CPU/RAM updates and a live GPU Core temperature of 37°C; the follow-up screenshot confirms 36°C in the widget. The reader reports PawnIO not installed, missing CPU temperature, zero detected fans and motherboard fan probing disabled. The supplied five-second sample was 0.19% total CPU / 114.9 MiB working set for the widget and 0.06% / 115.2 MiB for the collector. Those figures exclude Game Bar host/kernel driver work and do not establish battery impact or steady-state load.

The screenshot shows a dark surface despite the requested transparent background. The old height binding and default frame/button visuals are possible contributors, not a conclusively identified Windows root cause. This update explicitly sets a transparent Frame/root/ScrollViewer, removes pointer-state background fills on metric buttons, lays out the backdrop with the content, and collapses the backdrop at zero alpha. The widget remains responsible only for its content surface; Game Bar controls its own title bar.

The cramped in-widget flyouts are replaced with a registered Type=Settings Game Bar window using SettingsSupported and ActivateSettingsAsync. Each view keeps its own XboxGameBarWidget connection. Settings are scrollable and graph placement uses radio buttons; cross-view preference notifications dispatch to each view's own UI thread. Status snapshots are timestamped, and the settings timer stops when that view is hidden. Clicking the same reading twice deselects its highlight while keeping the graph metric; this state is saved separately.

PowerShell/XML checks now verify the parent/settings registration link and unique extension IDs. Those checks, all XAML event-handler checks, seven installer fixtures, C# syntax checks and all 51 portable checks pass. Partial metadata compilation has no errors other than the known missing Windows SDK union metadata for await projection. Full 1.2.0.2 XAML build, settings activation/scrolling, cross-view notifications, alpha rendering, deselection and saved state still need verification on Windows. No fan RPM or CPU-temperature success is claimed before testing with the required access and supported sensors.

Primary references: https://learn.microsoft.com/en-us/xbox/game-bar/guide/settings-widget ; https://learn.microsoft.com/en-us/xbox/game-bar/guide/transparency ; https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/discussions/2149 ; https://pawnio.eu/ .

### Background reader without a notification-area icon

The user requested no additional Hidden Icons entry. The reader now creates a NotifyIcon only when --show-tray / -ShowTrayIcon is explicitly supplied. Its normal message loop uses a non-visible Control for orderly shutdown dispatch; it is still a separate background process, not a Windows service. Setup now includes Stop reader. Stop-Monitor.ps1 sends a bounded, timestamped request addressed to the PID published in this widget's telemetry. The reader validates its own PID, process lifetime, age and timestamp before cancelling sensor work and exiting after the current call. Removal requests a stop first. Old 1.2.0.1 readers must be exited through their existing tray icon once during this upgrade.

Recompiled the updated collector and Setup sources (including C# 5) successfully. All 58 portable checks pass, including wrong PID, stale/reused-lifetime, future and missing stop timestamps; all seven installer fixtures and PowerShell/XML checks pass. Actual background-only Windows launch and stop-file IPC remain pending laptop verification. Hiding the tray icon does not remove monitoring overhead, and a stalled hardware call can delay graceful exit.

## Version 1.2.0.3 — settings activation workaround and current diagnostics

The latest screenshot confirms live CPU 4% and RAM 34%, but CPU/GPU temperatures and Fan show N/A. The user reports the 1.2.0.2 gear does not open settings. No new reader/driver diagnostic output accompanies that report, so a stopped reader, missing PawnIO, expired request or unavailable sensors cannot yet be distinguished. The earlier GPU temperature success remains evidence that the GPU sensor worked in the earlier state.

The gear now directly expands the same widget and hosts SettingsPage in a nested Frame, with Back to readings and a 420×620 resize request. There is no second Game Bar extension or ActivateSettingsAsync dependency. Dismissing Game Bar closes the settings view. Navigation/resize errors are displayed and serialized to widget-ui-status.txt, preventing the previous silent failure path. This bypasses the failing activation mechanism; it is not a confirmed diagnosis of why Windows did not open the separate window.

Start-Monitor.ps1 now waits for a fresh heartbeat matching the launched PID and prints sensor-driver status. Diagnose-Readings.ps1 includes widget-request age/flags and the saved settings UI event. No driver or firmware changes are made automatically. Full Windows XAML rendering/resizing, settings interaction and updated reader status still require the laptop test.

For 1.2.0.3, C# and PowerShell syntax, XML/manifest registration checks and all XAML handler references passed. Partial metadata compilation reports only the known missing Windows SDK union metadata for await projection, with no other errors. The portable logic suite (58 checks) and installer fixture suite (7 checks) are unchanged from the previously passing versions; these do not validate the new Windows UI at runtime.

## Version 1.2.0.4 — confirmed missing foreground-transparency opt-in

The new logs show live CPU/RAM and GPU Core temperature 36°C, fresh hardware samples, all request flags true, fan probing enabled, no tray icon, PawnIO not installed and zero fan sensors. The initial Waiting status is superseded by the later Active samples. These facts establish that the reader/demand link works; they do not establish CPU/fan compatibility without the missing access layer. The reported resource samples were 0.14–0.16% total CPU for the widget and 0.02–0.10% for the collector; these remain short samples excluding host/kernel-driver cost.

Compared the manifest with the official Game Bar package guide. Our Window element omitted AllowForegroundTransparency=true. The guide explicitly states that without it transparency applies only in PinnedOnly mode. Added this flag and a source regression check. This explains why clearing XAML alone is insufficient for foreground transparency, but pinned-mode opacity still requires validation.

Added Clear / Fully transparent background actions that set zero opacity and disable host override atomically. The widget Frame is cleared after SDK connection and on opacity application. Appearance changes in the readings view write a diagnostic snapshot of actual background alpha/visibility, saved opacity, host raw opacity, display mode, pin state and Frame brush; no new per-second appearance polling is added. Diagnostics check the installed flag directly and query Windows driver inventory read-only.

Executed 64 portable checks, including six alpha conversion/boundary checks; all passed. PowerShell syntax, XML and the new manifest check pass. XAML handlers are present. Partial metadata compilation reports no errors outside the known missing SDK union metadata for await projection. Actual Windows alpha rendering in Foreground/PinnedOnly, post-driver CPU temperature and MSI fan sensor availability remain pending laptop verification.

Primary sources: https://learn.microsoft.com/en-us/xbox/game-bar/guide/pkg-manifest ; https://learn.microsoft.com/en-us/xbox/game-bar/api/xgb-widget ; https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/discussions/2149 ; https://pawnio.eu/ .

## Version 1.2.0.5 — requested automatic startup, width and labels

The latest laptop output confirms CPU Package 42°C, GPU Core 36°C, PawnIO installed/running, all request flags active, motherboard fan probing enabled and zero fan sensors. Appearance readback is PinnedOnly with saved opacity 97%, actual alpha 247 and a transparent Frame (#00FFFFFF). This establishes a nearly opaque configured backplate; it is not evidence that zero opacity is being ignored. The new UI labels the slider Background transparency (100 = clear) and preserves the old underlying setting accurately.

A packaged .NET Framework 4.7.2 helper is declared through windows.fullTrustProcess, runFullTrust and allowElevation. The UWP widget requests it when hardware readings need a reader. The helper validates package identity, a bounded four-field request, timestamp, nonce and boolean fan opt-in, checks an existing heartbeat against a live reader PID/session, and invokes only the fixed installed collector using RunAs. It has no window/tray icon and exits after launching. Startup is attempted once per widget activation, so denied UAC does not repeat automatically. A stop request from the current activation suppresses automatic restart; an explicit Start sensors / retry allows it. Prior manual fan-probing mode is migrated when present. No task, service, autorun registry entry, driver installation or UAC bypass is introduced. Runtime files are copied before widget registration to avoid an activation race.

The compact minimum width is 145px. Manual width changes are saved separately for compact/top/bottom versus side-graph layouts; fitting preserves that width. Settings use a temporary size and restore the selected reading width. Side columns use proportional sizing. Temperature row labels are CPU/GPU, with units/icons and descriptive reading settings.

The launcher compiled against real .NET Framework references with C# 5 and an asInvoker manifest. All 79 portable checks pass, including launch-schema/nonce/time/boolean rejection and transparency conversion. Seven installer fixtures, source syntax, PowerShell/XML and package-wiring checks pass. Partial UWP metadata compilation has only the known missing SDK union metadata errors, and no other errors. These checks do not validate actual full-trust activation, UAC, app-data paths, package registration, width persistence or UI rendering on Windows. Those remain pending laptop verification. Fan RPM remains unsupported by the current detected sensor set; no success is claimed.

## Version 1.2.0.6 — MSI fan fallback

The user identified the laptop as Micro-Star International Co., Ltd., Cyborg 14 A13VF and supplied WMI metadata confirming MSI_ACPI.Get_Fan. Existing logs confirm CPU/RAM and CPU/GPU temperatures work; the general library exposes zero fan sensors despite PawnIO and motherboard probing. Method presence alone is not evidence of actual RPM.

Added a separate model-scoped MSI WMI reader, called only for requested fan readings when every general fan RPM is invalid/missing. Valid general zero RPM takes priority. The reader validates GUID/method/active-instance metadata and invokes only Get_Fan with subfeature 0. It does not add firmware writes, raw EC I/O or fan controls. Four channels decode from validated 32-byte packets using big-endian periods and integer 480000/raw. Zero slots are initially hidden until identified spinning, then can report stopped. Invalid/failed/stale readings never become guessed RPM.

At most one asynchronous WMI job can be pending; a 1.5-second await limit keeps the sensor loop from waiting indefinitely and cannot cancel the underlying WMI job. New requests are suppressed while that job is pending. Errors retry after 30 seconds. These safeguards coordinate only this process, not OEM services. The independent native CPU/RAM publisher is unchanged. New telemetry and UI diagnostics expose fan source/status/raw replies. Other brands and unverified MSI models keep the general reader without vendor-method invocation.

Collector source compiled using .NET 8/Windows Desktop, actual LHM metadata and System.Management API references. All 101 portable checks passed, including fallback priority, model gating, endian conversion, missing/failed/truncated/invalid packets and stopped-versus-absent channels. Full Windows UWP compilation, WMI invocation/RPM accuracy, package launch and coexistence with OEM monitoring remain pending laptop verification.

## Version 1.2.0.7 — editable alerts and npm source installer

Added native RepeatButtons (400 ms delay / 70 ms interval) next to the four alert fields. Each click adjusts one displayed unit or 100 RPM; bounds are retained. Keyboard entry selects on focus, accepts Enter/focus-loss commits and Up/Down adjustments, rejects empty/nonfinite/out-of-range input, and retains Save all/Defaults. Blink toggles persist immediately. The settings page now receives its owning widget context. The exact earlier input failure cannot be diagnosed or considered resolved without the user's Windows test.

Added a dependency-free npm CLI with explicit install, doctor, start, stop, diagnose and uninstall commands. Installation stages source in a fresh per-user folder, invokes PowerShell with an argument array and no shell, preserves failed build output, and cleans only its successful source stage. No npm install hooks run Windows software. Explicit setup checks Windows x64/Game Bar/Developer Mode, VS 2022 C# UWP targets, Windows SDK and .NET SDK; each missing Microsoft tool category requires an interactive yes before setup. Existing tools are reused; a ready VS instance is selected instead of blindly picking the newest edition. Normal UAC remains. SDK detection accepts newer compatible .NET SDKs. No signed binary release or absence of build tools is claimed.

The full README covers current-PC setup, fresh PCs, minimal/manual tools, GitHub repository creation/tagging, GitHub npm install, local npm tarballs, diagnostics, model limits and removal. The repository has not been uploaded or npm-published.

Passed: 111 portable numeric/protocol/alert checks; 7 Node CLI/staging tests; 7 package-installer fixtures; prerequisite-plan fixtures; actual local tarball installation through npm and CLI shim execution; package-content inspection; C#/PowerShell syntax and XML checks. Partial UWP compilation has only the same 76 known missing SDK union-metadata errors, no other errors. No real WinGet installation, VS modification, UWP/XAML build, Game Bar key input or hold-repeat rendering is executed in Linux. Those remain Windows integration tests. MSI fan access was not changed or newly verified in this release.
