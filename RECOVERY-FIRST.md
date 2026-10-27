# Native Performance recovery and recurrence

The user confirmed on 27 October 2026 that the original Performance widget opens after restarting. Baseline recovery is complete; the original cause remains unconfirmed. See UPDATE-1.2.md for the staged custom-widget test.

If the problem returns:

1. Run Stop-Monitor.ps1 or use Setup > Stop reader. An older 1.2.0.1 reader still uses its tray icon > Exit. If a reader is stuck and cannot exit, use Task Manager to end only CyborgVitals.Collector.exe.
2. In normal PowerShell run: `Get-AppxPackage -Name "CyborgVitals" | Remove-AppxPackage`.
3. Save work, restart Windows and test Win+G > Widget menu > Performance without starting the custom app.
4. If necessary, open Game Bar Advanced options in Windows Settings and select Repair. Do not automatically reset or remove Microsoft packages.
5. Record whether the issue happens before starting the collector, with temperature-only monitoring, or with motherboard fan probing. This helps distinguish host activation from sensor-access issues.

No fan, voltage, frequency or power-setting controls are implemented, but third-party hardware access can still conflict with firmware/drivers or affect power use. Read-only monitoring does not imply zero overhead. High fan RPM is not by itself evidence of a fault.

MSI monitoring/EC guidance: https://www.msi.com/faq/faq-11142
Microsoft app repair guidance: https://support.microsoft.com/en-us/windows/apps/repair-apps-and-programs-in-windows
