# Cyborg Vitals

A compact, pinnable **Xbox Game Bar widget for Windows 11**. Keep CPU/RAM usage, CPU/GPU temperatures, GPU usage and supported fan speeds visible while you work or play.

**Stable npm release: [`cyborg-vitals@1.2.7`](https://www.npmjs.com/package/cyborg-vitals/v/1.2.7)** · **Windows widget version: `1.2.0.7`**

This release runs through **Win+G**. It includes the widget source, a background sensor reader and a guided Windows installer. Installation builds the application locally, so **Visual Studio UWP tools, a Windows SDK and a .NET SDK are still required on each destination PC**. This is not a prebuilt Windows installer release.

## Install from npm

Install [Node.js with npm](https://nodejs.org/en/download), using a supported LTS release. Open **Command Prompt** as the Windows user who will use the widget, then run:

```cmd
npm install --global cyborg-vitals@1.2.7
cyborg-vitals install
```

The version is pinned to the stable release documented here. Git, a GitHub download and an npm account are not required for this public npm installation.

For a one-command setup without retaining a global CLI installation:

```cmd
npx cyborg-vitals@1.2.7 install
```

The installer checks existing tools and offers missing prerequisites. Installing the npm package alone does not build or register the Windows widget; `cyborg-vitals install` performs that step. There are no install/postinstall hooks that silently run Windows setup.

### Finish setup

1. Follow the prerequisite prompts. Answer **y** only for the missing tools you want installed; compatible existing tools are reused.
2. If requested, enable **Developer Mode** in Windows Settings or install **Xbox Game Bar** through Microsoft Store. Return to the terminal when finished.
3. Close any existing **Cyborg Vitals** widget through Win+G before registration. Setup also attempts to stop its existing sensor reader before replacing runtime files. If stopping fails, use that reader's existing controls and retry.
4. If a tool installer requires a restart, restart Windows and run `cyborg-vitals install` again.
5. Wait for **Installation complete**, then press **Win+G → Widget menu → Cyborg Vitals**.
6. Open the widget's settings, choose the readings you want and click its **pin** to keep it visible after dismissing Game Bar.

Hardware readings may trigger a normal administrator approval prompt. CPU/RAM usage works without the elevated hardware reader. Run setup and approve sensor access using the **same Windows administrator account**; supplying credentials for a different account is not a verified configuration.

## Features

- Live CPU and RAM usage from Windows APIs.
- CPU/GPU temperatures, GPU usage and available fan RPM through supported hardware interfaces.
- Optional icons and individual reading switches for a compact layout.
- Celsius/Fahrenheit selection, including temperature alert thresholds.
- Adjustable background transparency and a **Clear** action.
- Optional graph positioned to the left, right, top or bottom of the readings.
- Configurable blinking alert dots, with editable thresholds and repeating **−/+** buttons.
- Automatic hardware-reader launch when requested by the widget, subject to Windows approval.
- Background monitoring without a notification-area icon by default.

Hardware availability varies by model and driver. A working MSI Center reading does not guarantee that a general sensor library exposes the same reading.

## Requirements

| Requirement | Why it is needed |
| --- | --- |
| Windows 11 on an Intel/AMD x64 PC | Supported build target; ARM64, macOS and Linux are not supported targets for this release |
| Xbox Game Bar | Hosts, displays and pins the widget |
| Windows Developer Mode | Registers this locally built development package |
| Visual Studio **2022** with C# UWP tools | Compiles the Windows widget |
| One Windows 11 SDK, **10.0.22621.0 or newer** | Provides UWP metadata and packaging tools |
| .NET SDK **8 or newer** | Builds the sensor reader; a suitable newer SDK, including .NET 9, is reused |
| Node.js **20 or newer**, with npm | Runs the npm installation command; use a supported LTS release |
| Internet access during setup | Downloads the npm package, restores NuGet dependencies and obtains approved missing prerequisites |

Git is only needed for working with a cloned repository or using a GitHub-based npm URL. It is not required for installation from the npm registry.

The guided installer uses WinGet when installing missing Microsoft tools. If WinGet is unavailable, install/update **App Installer** through Microsoft Store or use the manual steps below. Corporate device policies may require help from your administrator. Visual Studio Community use is subject to Microsoft's license eligibility requirements.

### Minimal Visual Studio setup

In **Visual Studio Installer → Visual Studio 2022 → Modify**:

1. Select **WinUI application development**; older installer versions may call the workload **Universal Windows Platform development**.
2. Include **Universal Windows Platform tools** for C#.
3. Include **one** Windows 11 SDK, version **22621 or newer**.

C++ WinUI/UWP toolsets, Copilot, IntelliCode, graphics profilers, USB device connectivity and multiple SDK versions are not requested by this project. Keep any components required by your other projects. Microsoft's required workload dependencies will still be installed.

The included `.vsconfig` requests the UWP workload, `Microsoft.VisualStudio.ComponentGroup.UWP.Support` and SDK 26100. Setup checks for actual MSBuild/C# XAML targets. Visual Studio 2026 or Build Tools alone are not verified substitutes for this release's Visual Studio 2022 UWP configuration.

Check your .NET SDK separately:

```cmd
dotnet --list-sdks
```

A runtime alone cannot build the reader. After a successful installation, the published reader includes its .NET runtime. NuGet restores the C#/UWP/sensor libraries during the build; these are not npm dependencies to install manually.

### Can other PCs skip the build tools?

Not with the current `1.2.7` source distribution. That requires a separate release containing the compiled widget, compiled reader and a deployment installer. Normal packaged distribution also requires appropriate Windows package signing and trust. Publishing this source package to npm simplifies downloading, but does not turn it into that prebuilt distribution.

## Configure the widget

Press **Win+G**, then click the widget's **gear**. Settings use an expanded, scrollable view; return to the readings when finished.

| Setting | Behaviour |
| --- | --- |
| Readings | Enable only the rows you need; supported fans can be selected individually |
| Icons | Show or hide the icons beside labels |
| Temperature unit | Switch between Celsius and Fahrenheit |
| Background transparency | **100%** makes the readings background clear; **0%** makes it opaque |
| Clear | Clears the readings background and disables the Game Bar opacity override |
| Graph | Show/hide it and select its placement |
| Selected reading | Click a row to select its graph metric; click it again to remove the blue highlight |
| Width | Resize a widget edge while Win+G is open; the compact minimum width is 145 pixels |

The readings toolbar hides when Game Bar is dismissed. The settings surface remains opaque for readability. Game Bar controls its own surrounding frame and pin behaviour.

### Blinking alerts

Open **Settings → Alerts**. Alert dots blink when a reported value exceeds its configured threshold.

| Alert | Default | Change per click |
| --- | --- | --- |
| Usage | 85% | 1 percentage point |
| CPU temperature | 90°C | 1 degree in the selected display unit |
| GPU temperature | 85°C | 1 degree in the selected display unit |
| Fan speed | 3500 RPM | 100 RPM |

- Click **−/+** once for a single adjustment.
- Hold a button to repeat: repetition starts after 400 ms, then runs every 70 ms.
- Type into the number field and press **Enter** or leave the field to save. **Up/Down** also adjust it.
- **Save thresholds** validates all fields. **Defaults** restores the values above.
- **Blink alert dots** switches blinking on or off.

Invalid, empty or out-of-range input is not saved. Temperature thresholds are stored in Celsius and shown in the selected unit. These are configurable attention thresholds, not manufacturer safety limits; high fan RPM can be normal cooling behaviour.

## Sensors and supported hardware

| Reading | Source | Notes |
| --- | --- | --- |
| CPU/RAM usage | Windows APIs | Works without the elevated sensor reader |
| CPU/GPU temperature and GPU usage | LibreHardwareMonitor | Depends on hardware support, drivers and access permissions |
| Fan RPM | General sensor library first | A reported **0 RPM** remains valid; missing RPM is shown as **N/A** |
| MSI fan fallback | MSI WMI interface | Scoped to **MSI Cyborg 14 A13VF** with matching manufacturer/interface metadata; used when fan readings are requested and general RPM is unavailable |

General readings can work on other brands and models. The MSI-specific fallback is not applied to other brands or unverified MSI models. Its presence does not establish that every supported interface returns usable RPM. Fan fallback status and raw replies are included in diagnostics; anonymous channels are not guessed to be CPU or GPU fans.

Some CPU/motherboard sensors require **PawnIO**. It is optional for CPU/RAM-only use and is not installed automatically. If diagnostics indicate missing access, obtain the signed installer from [pawnio.eu](https://pawnio.eu). Keep Windows security enabled. The MSI WMI fallback does not add its own raw-I/O driver.

The application reads monitoring data; it does not provide controls for fan curves, clocks or voltages. Hardware polling is requested only for enabled, visible categories and pauses when the widget's monitoring request expires after hiding or closing. An already running hardware call may finish later. The background process remains until stopped, and hardware monitoring still has resource and power costs.

### Startup and saved layout

Version `1.2.7` does not install a Windows service or sign-in startup task and does not automatically open the widget at Windows startup. Open it through **Win+G** and pin it. The widget saves its own display preferences; Game Bar manages the pin, screen position and restoration behaviour. Exact position restoration after a reboot is not guaranteed by this package.

Automatic **sensor-reader** launch when the widget requests hardware readings is separate from opening the widget at sign-in. Windows may still request administrator approval.

## Commands and diagnostics

After installing the global npm CLI:

| Command | Action |
| --- | --- |
| `cyborg-vitals --version` | Shows the npm CLI version |
| `cyborg-vitals doctor` | Checks prerequisites without installing them; missing requirements produce a failing exit status |
| `cyborg-vitals install` | Checks prerequisites, builds and registers the widget for this Windows user |
| `cyborg-vitals diagnose` | Reports installed widget/reader status, readings, monitoring requests and sensor/fan diagnostics |
| `cyborg-vitals start` | Requests the elevated hardware reader; open the widget through Win+G separately |
| `cyborg-vitals stop` | Requests a graceful stop of our reader |
| `cyborg-vitals uninstall` | Removes our Game Bar widget and its Start shortcut |

CPU/RAM usage remains available inside the widget without the reader. Changing reader startup options requires stopping the existing reader first. No tray icon is shown by default.

### Temperatures or fan speed show N/A

1. Enable the desired reading in the widget's settings.
2. Use **Start sensors / retry** in settings and approve Windows' administrator prompt under the same account.
3. Run `cyborg-vitals diagnose` and check telemetry freshness, sensor-driver status, monitoring requests and any error text.
4. For fan issues, include **Fan source**, **Fan fallback status** and **MSI Get_Fan raw reply** when reporting the problem.

**N/A does not mean a fan has stopped.** The hardware may not expose a supported reading even if its manufacturer's app can display one.

For a five-second CPU/memory sample of our processes, run this from a source checkout:

```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-Readings.ps1 -MeasureResources
```

The sample excludes Game Bar host and kernel-driver overhead and is not a battery measurement.

### Installation problems

- Keep the **first actual error**, terminal output and the retained source/build path.
- If setup cannot stop an older reader, close our widget and use the reader's existing stop/exit controls before retrying. Avoid starting a second reader with different options.
- If **Installation complete** is followed only by an `EBUSY` error removing a temporary source folder, the final cleanup failed. Check Win+G and diagnostics before treating it as a build failure. The installer in this release may leave that temporary folder locked.
- If prerequisites were just installed, reopen the terminal or restart when requested, then retry.
- If the native Microsoft Performance widget has problems, see [RECOVERY-FIRST.md](RECOVERY-FIRST.md). Do not reset all Microsoft packages or disable Windows security as a troubleshooting shortcut.

Runtime files live under `%LOCALAPPDATA%\CyborgVitals`. Widget preferences and telemetry use the installed package's `LocalState` directory under `%LOCALAPPDATA%\Packages`; diagnostics prints the actual path. The npm CLI builds in its own per-user source folder and retains failed build output for diagnosis.

## Install from a source checkout or ZIP

Open **Command Prompt** in the folder containing `package.json`, `Widget` and `Collector`. For example:

```cmd
cd /d "D:\Repos\CyborgVitals"
npm run setup
```

No preliminary `npm install` is needed for this dependency-free CLI. If using a ZIP, extract the whole archive first. The actual directory can differ from the example.

Without Node.js, run the same guided installer directly:

```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

This still needs the Windows/.NET/UWP prerequisites above. `Build-And-Install.ps1` is the lower-level build command for machines whose prerequisites are already ready.

For a local npm tarball:

```cmd
npm pack
npm install --global .\cyborg-vitals-1.2.7.tgz
cyborg-vitals install
```

GitHub is useful for obtaining source and contributing; the published npm commands above are the simplest download route for this release.

## Remove Cyborg Vitals

Close our widget in Win+G, then run:

```cmd
cyborg-vitals uninstall
npm uninstall --global cyborg-vitals
```

Run widget removal before removing the CLI. Widget removal stops our reader first and removes only our registered widget and shortcut. It does not uninstall Microsoft Game Bar, Microsoft's Performance widget or your build tools. Check the output before manually deleting retained application files or preferences.

## Development and validation

From a source checkout:

```cmd
npm test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Verify-Source.ps1
dotnet run --project .\Tests\MetricsChecks.csproj
```

Additional fixture scripts in `Tests` cover prerequisite planning and package selection/extraction. See [VALIDATION.md](VALIDATION.md) for test history and Windows/hardware validation limits. Passing npm tests or creating a tarball does not establish successful Windows registration or compatibility with every sensor.

The published npm release is **1.2.7**; the embedded Windows package uses the four-part version **1.2.0.7**. These are different version formats for the same release. Updating a README on GitHub does not replace the README already included in the published npm tarball.

## License and acknowledgements

Licensed under [MIT](LICENSE.txt). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency notices.

- [Microsoft Game Bar SDK](https://learn.microsoft.com/en-us/xbox/game-bar/)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- [PawnIO](https://pawnio.eu)
- [MSI WMI fan interface documentation](https://docs.kernel.org/wmi/devices/msi-wmi-platform.html)

No Microsoft or MSI endorsement is implied.
