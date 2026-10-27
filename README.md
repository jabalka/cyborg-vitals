# Cyborg Vitals

Compact, pinnable Windows Game Bar widget for CPU/RAM usage, CPU/GPU temperatures, GPU usage and available fan RPM. Version **1.2.0.7** of the Windows widget is distributed by npm package **1.2.7**.

**Current distribution: complete source plus a guided local build.** Installing through npm simplifies downloading and setup; it does not remove the Visual Studio/UWP build requirements. This package has not been published to npm or uploaded to GitHub yet. The GitHub commands below become usable after you upload it to your own repository. Replace `YOUR_USERNAME` with your GitHub username.

## 1. Requirements on each PC

| Requirement | Purpose | Guided setup |
| --- | --- | --- |
| Windows 11, Intel/AMD x64 | Current supported build target | Checked; ARM64/macOS/Linux are not supported targets |
| Xbox Game Bar, installed for the Windows user | Hosts and pins the widget | Offers its official Microsoft Store page if missing |
| Windows Developer Mode | Registers this local development build | Offers Windows Settings; you enable it explicitly |
| Visual Studio **2022** with C# UWP tools | Builds the UWP widget | Offers missing components; modifies an existing 2022 installation when possible |
| One Windows 11 SDK, **10.0.22621.0 or newer** | UWP metadata and packaging | Reuses an existing SDK; otherwise requests the 26100 SDK |
| .NET SDK **8 or newer** | Builds the sensor reader | Reuses a suitable SDK, including 9; offers .NET 8 SDK only if still missing |
| Node.js **20 or newer**, with npm | Optional npm command route | Install Node from https://nodejs.org/en/download if needed; use a supported LTS release |
| Git | Only for uploading to GitHub or installing with `github:...` syntax | Install from https://git-scm.com/download/win if needed |
| Internet during first build | NuGet restore and any approved tools | Required; the widget itself does not report telemetry externally |

Run setup as the Windows user who will use the widget. Hardware-reader elevation must use that **same administrator account**; a standard user supplying another account's credentials is not a verified configuration. Windows UAC prompts remain in place. Corporate device policies may require your IT administrator.

The supplied source route needs build tools on **every destination PC**. A future signed binary release could avoid that requirement; this archive is not such a release. Microsoft Visual Studio Community has license eligibility requirements; choose a licensed Professional/Enterprise 2022 installation when needed.

## 2. Install this ZIP on your current laptop first

1. Extract the complete ZIP to the Desktop. Do not run setup from inside the ZIP viewer.
2. Open **Command Prompt**. With Windows Extract All's default folder name, run:

```cmd
cd /d "%USERPROFILE%\Desktop\CyborgVitals-1.2.0.7\CyborgVitals"
node --version
npm --version
npm run setup
```

3. Follow the missing-tool prompts. Answer **y** only for the prerequisites you want installed. Existing working tools are reused. Visual Studio can take several GB; review its requested components. No `--includeRecommended` or `--includeOptional` flag is used.
4. If asked, enable Developer Mode in Windows Settings, or install Game Bar through Microsoft Store, then return to the terminal and press Enter. If a prerequisite requests a restart, restart Windows and run `npm run setup` again.
5. Setup stops our existing reader before upgrading. Close **Cyborg Vitals** in Win+G when prompted, then press Enter. Leave Microsoft's native Performance widget installed.
6. Wait for **Installation complete**. Open Win+G > Widget menu > Cyborg Vitals and pin it. Hardware readings can request their background reader with a normal administrator prompt.

There are no JavaScript dependencies to restore. A preliminary `npm install` in the extracted folder is unnecessary. `npm run setup` invokes the explicit installer; installing the npm package alone does not build/register Windows software.

If you prefer not to install Node.js, the same guided setup works directly in Command Prompt:

```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install.ps1
```

For PowerShell instead of Command Prompt, use `cd` without `/d` and `$env:USERPROFILE` instead of `%USERPROFILE%`.

## 3. Upload this project to GitHub yourself

1. Sign in at https://github.com/new . Create an empty repository named **cyborg-vitals**. Choose Public if anyone should be able to install it, or Private if access should be restricted. Do **not** initialize it with a README, license or gitignore; those files are already included here.
2. In Command Prompt, enter the extracted **CyborgVitals** directory from section 2. `package.json`, `README.md`, `Widget` and `Collector` must be at the repository root, not inside another nested CyborgVitals folder.
3. Check Git and initialize this new project:

```cmd
git --version
git init
git branch -M main
git add .
git status --short
```

4. Check that generated `dist`, `obj`, nested `bin`, `node_modules`, private signing keys and logs are absent from the staged files. The included `.gitignore` excludes them. The root `bin/cli.cjs`, our `Setup.exe` and our widget startup helper are intentional source-distribution files.
5. Commit, connect your new repository and push. Replace `YOUR_USERNAME` before running:

```cmd
git commit -m "Add Cyborg Vitals widget and guided npm installer"
git remote add origin https://github.com/YOUR_USERNAME/cyborg-vitals.git
git push -u origin main
git tag v1.2.7
git push origin v1.2.7
```

If Git asks for author identity, set it for this repository, then repeat the commit:

```cmd
git config user.name "Your Name"
git config user.email "YOUR_GITHUB_NOREPLY_EMAIL"
```

Use the actual private commit email shown in GitHub Settings > Emails if desired. Complete Git Credential Manager's normal browser sign-in when GitHub authentication is requested. Do not put a password/token into the repository URL.

6. Refresh GitHub and confirm `package.json` and `README.md` appear at the root. The **v1.2.7** tag now identifies this exact release. Optional: create a GitHub Release from that tag; no npm registry account or npm publishing is required for GitHub installation.

The npm package has `private: true` to prevent accidental publication to the shared npm registry. This does **not** prevent installation from your GitHub repository or a local tarball. No repository URL has been invented or hard-coded in the package.

## 4. Install from GitHub on another PC

1. Install Node.js with npm and Git if missing. Open a **new Command Prompt** and check:

```cmd
node --version
npm --version
git --version
```

2. Replace `YOUR_USERNAME`, then install the command and run setup:

```cmd
npm install --global github:YOUR_USERNAME/cyborg-vitals#v1.2.7
cyborg-vitals install
```

3. Approve the missing prerequisites you want, follow the Developer Mode/Game Bar prompts, and let the Windows build finish. Reopen the terminal or restart if requested, then run `cyborg-vitals install` again.
4. Open Win+G, open Cyborg Vitals, enable desired readings in its settings and pin it. Each user's installation and preferences are separate; repeat setup for the intended Windows user on each machine.

A public repository works without GitHub sign-in for downloading. A private repository requires an authorized GitHub account and normal Git credentials on that PC. This is not access granted by the widget installer.

An alternative one-time command, without globally keeping the npm CLI, is:

```cmd
npx --package=github:YOUR_USERNAME/cyborg-vitals#v1.2.7 cyborg-vitals install
```

Normal npm package-download confirmation may appear. The project deliberately has no install/postinstall lifecycle hook: prerequisite prompts run in the explicit, interactive `cyborg-vitals install` command.

## 5. Use a local npm package without GitHub

From this project's root:

```cmd
npm pack
npm install --global .\cyborg-vitals-1.2.7.tgz
cyborg-vitals install
```

Copy the generated `.tgz` to another PC and run its last two commands there. A tarball install copies the npm package independently of your extracted source folder. Keep that source/tarball until installation succeeds. The `.tgz` is ignored by Git, preventing recursive package uploads.

The CLI builds in its own temporary per-user source folder rather than changing npm's package/cache files. On success it removes only that staging folder. On failure it keeps the source/build output and prints the path for troubleshooting. Runtime files are copied separately under `%LOCALAPPDATA%\CyborgVitals`; the registered widget layout remains there.

## 6. Minimal manual tool setup

If WinGet is unavailable, update/install **App Installer** through Microsoft Store or install tools manually:

1. Install Visual Studio **2022** from Microsoft's downloads. In Visual Studio Installer > Modify, select **WinUI application development** (older versions label the workload **Universal Windows Platform development**). Include **Universal Windows Platform tools** for C# and one Windows 11 SDK >=22621. The included `.vsconfig` requests the workload, `Microsoft.VisualStudio.ComponentGroup.UWP.Support` and SDK 26100. Required dependencies are installed by Visual Studio; no optional C++ tools are requested.
2. Skip Copilot, IntelliCode, C++ WinUI/UWP toolsets, graphics profilers, USB device connectivity and additional SDK versions unless you separately need them. Do not remove tools used by other projects.
3. Install a .NET SDK >=8 if none is available; a runtime alone does not build the collector. Check `dotnet --list-sdks` in a new terminal.
4. Enable Windows Developer Mode and install/update Game Bar through Microsoft Store.
5. Retry the guided installer or build directly:

```cmd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Stop-Monitor.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build-And-Install.ps1
```

The installer checks for the actual C# XAML targets and MSBuild, not just a Visual Studio shortcut. Visual Studio 2026 or Build Tools alone are not verified replacements for this project's VS 2022 UWP build. If only those are installed, the wizard can offer a separate 2022 Community installation after confirmation.

NuGet automatically restores Microsoft.Gaming.XboxGameBar, Microsoft.NETCore.UniversalWindowsPlatform, LibreHardwareMonitorLib and System.Management during the Windows build. Do not install these as npm packages. The collector is published with its .NET runtime; .NET SDK is a build prerequisite, not a manual runtime installation after a successful build.

## 7. Readings and model compatibility

| Reading | Source and limitations |
| --- | --- |
| CPU/RAM usage | Windows APIs; available without the elevated sensor reader |
| CPU/GPU temperature and GPU usage | LibreHardwareMonitor; hardware/driver/model dependent |
| Fan RPM | General sensor library first; a valid 0 RPM is retained |
| MSI fan fallback | Only when fan readings are enabled and general RPM is unavailable; initial enabled model is MSI Cyborg 14 A13VF with matching manufacturer/interface metadata |

General readings can work on other brands, but not every laptop exposes temperatures/fan RPM. The MSI profile is not applied to other brands or unverified MSI models. The new MSI RPM path still needs on-laptop verification; class/method existence alone does not confirm a valid RPM response. No CPU/GPU fan identity is guessed for anonymous MSI channels.

Some CPU/motherboard sensors need **PawnIO**. It is optional for CPU/RAM-only use and is **not installed automatically**. Diagnose the actual missing reading first, then use the official signed installer at https://pawnio.eu if needed. Keep Windows security enabled. Firmware, fan curves, voltages and clocks are not changed by our code. The MSI fan fallback uses Windows WMI and does not require adding a separate raw-I/O driver of its own.

Hardware access is requested only for enabled, visible categories and pauses when the widget's request expires after hiding/closing. An in-flight driver/WMI call may finish later. The reader process remains in the background until stopped; no tray icon appears by default. There is no login startup task or Windows service. OEM/driver compatibility and power overhead still need testing on each model.

## 8. Alert controls and appearance

Open Win+G > widget gear > **Alerts**:

- Click **−/+** for one step: usage 1%, temperature 1 degree in the selected unit, fan 100 RPM.
- Hold a button: repeats start after 400 ms, then every 70 ms. Release to stop. Values stay inside the displayed supported limits.
- Click the number and type. Press **Enter** or leave the field to save it. **Up/Down** also adjust it. Invalid/empty values are not saved; correct the visible error or use a step button to recover from the last saved value.
- **Save thresholds** validates/saves all four fields; **Defaults** restores usage 85%, CPU 90°C, GPU 85°C and fan 3500 RPM. **Blink alert dots** saves immediately.

Temperature thresholds are stored in Celsius and displayed in Celsius/Fahrenheit. Thresholds are configurable attention levels, not a diagnosis of dangerous hardware conditions. High fan RPM can be normal cooling.

For a clear readings background, Display > **Background transparency 100%**, or click **Clear**. The settings panel stays opaque for readability. Dismiss Win+G to hide the readings toolbar. Drag a widget edge while Win+G is open to resize; compact minimum width is 145 pixels. Click an already selected blue row to deselect its highlight. Readings/icons/graph are individually configurable.

## 9. Commands, diagnostics and removal

After a global npm installation:

```cmd
cyborg-vitals doctor
cyborg-vitals diagnose
cyborg-vitals start
cyborg-vitals stop
cyborg-vitals uninstall
```

`doctor` checks prerequisites without installing anything. `diagnose` reports the current installed widget/reader, fan source and fallback response. `start` requests the elevated hardware reader; it does not silently replace an already-running reader's options. `stop` stops our reader. `uninstall` removes our registered widget/shortcut; it does not uninstall Microsoft Game Bar or your build tools. Remove the optional CLI separately with `npm uninstall --global cyborg-vitals`.

Without a globally installed CLI, run the equivalent `Diagnose-Readings.ps1`, `Start-Monitor.ps1 -EnableHardwareSensors`, `Stop-Monitor.ps1` or `Remove-Widget.ps1` with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File` from your source folder. Automatic reader launch normally avoids daily commands, subject to Windows approval. Start sensors / retry is available in widget settings.

If a build/setup fails, retain the **first actual error**, the installer output and the printed source path. For N/A fan RPM send diagnostics, especially **Fan source**, **Fan fallback status** and **MSI Get_Fan raw reply**. A missing reading is not evidence of a stopped fan. Do not reset all Windows packages or disable security to resolve it. See [RECOVERY-FIRST.md](RECOVERY-FIRST.md) if the earlier native Performance issue returns.

## 10. Validation and references

Portable numeric/protocol tests, Node CLI/staging tests, prerequisite-planning fixtures, PowerShell/XML checks and installer-package fixtures run independently of real hardware. Full UWP/XAML compilation, new edit/hold behavior, WinGet/VS modification and MSI RPM must still be checked on Windows. See [VALIDATION.md](VALIDATION.md) for the exact boundary. A successful npm pack/test does not prove successful Windows registration.

- [Microsoft Game Bar SDK](https://learn.microsoft.com/en-us/xbox/game-bar/)
- [UWP RepeatButton](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.primitives.repeatbutton)
- [Visual Studio 2022 component IDs](https://github.com/MicrosoftDocs/visualstudio-docs/blob/main/docs/install/includes/vs-2022/workload-component-id-vs-community.md)
- [Visual Studio command-line installation](https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio)
- [npm GitHub installation](https://docs.npmjs.com/cli/v11/commands/npm-install/)
- [MSI WMI fan interface](https://docs.kernel.org/wmi/devices/msi-wmi-platform.html)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

No Microsoft or MSI endorsement is implied.
