# Third-party components

This source archive restores dependencies through NuGet. Each dependency keeps its own license; the project's MIT license does not replace them. The archive includes our compiled Setup.exe; it includes no upstream binary. Setup uses the Windows .NET Framework at runtime. Other dependencies are restored by the source build.

- Microsoft.Gaming.XboxGameBar 7.3.2607010: Microsoft Game Bar SDK license. https://www.nuget.org/packages/Microsoft.Gaming.XboxGameBar/7.3.2607010
- Microsoft.NETCore.UniversalWindowsPlatform 6.2.14 and Windows build/runtime dependencies: licenses supplied with the relevant Microsoft packages. https://www.nuget.org/packages/Microsoft.NETCore.UniversalWindowsPlatform/6.2.14
- LibreHardwareMonitorLib 0.9.6: MPL-2.0, with additional component notices in its upstream repository. Unmodified source for this release is available at https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/3d331e3370efb858411f19511373eff65a218701 . License: https://www.mozilla.org/en-US/MPL/2.0/ . Dependencies include DiskInfoToolkit, HidSharp, RAMSPDToolkit-NDD, Mono.Posix.NETStandard and Microsoft runtime packages; consult each package's notices before redistributing a compiled build.
- System.Management 10.0.2: MIT, Microsoft .NET libraries; already a LibreHardwareMonitor dependency and now referenced explicitly for WMI. https://www.nuget.org/packages/System.Management/10.0.2
- Microsoft XboxGameBarSamples: MIT licensed reference for activation, manifest structure and opacity handling. https://github.com/microsoft/XboxGameBarSamples

If you redistribute compiled outputs, retain the dependency license notices from the restored packages and satisfy their individual redistribution terms. The source archive itself contains project-authored code and icons only.

MSI fan protocol facts (method 17, subfeature 0, 32-byte buffer, big-endian periods and 480000/raw RPM) are documented at https://docs.kernel.org/wmi/devices/msi-wmi-platform.html . The provider/decoder in this project are newly authored; no kernel driver code is bundled.
