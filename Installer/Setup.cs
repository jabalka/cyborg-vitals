using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CyborgVitals.Setup
{
    internal static class Program
    {
        private static string startupLog;
        private static int reported;
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hwnd, string text, string caption, uint type);

        [STAThread]
        private static int Main(string[] args)
        {
            // Keep the earliest entry point independent of Windows Forms initialization.
            try
            {
                string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyborgVitals", "Logs");
                Directory.CreateDirectory(logs);
                startupLog = Path.Combine(logs, "setup-startup.log");
                File.WriteAllText(startupLog, DateTime.Now.ToString("O") + " Managed setup entry reached.\r\n" +
                    "Executable: " + System.Reflection.Assembly.GetExecutingAssembly().Location + "\r\n" +
                    "CLR: " + Environment.Version + "; 64-bit process: " + Environment.Is64BitProcess + "\r\n", new UTF8Encoding(false));
            }
            catch
            {
                try { startupLog = Path.Combine(Path.GetTempPath(), "CyborgVitals-setup-startup.log"); File.WriteAllText(startupLog, "Managed setup entry reached.\r\n"); }
                catch { startupLog = null; }
            }
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Report(e.ExceptionObject as Exception ?? new Exception("Unhandled setup error."));
            try { return RunGui(args); }
            catch (Exception ex) { Report(ex); return 1; }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int RunGui(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => { Report(e.Exception); Environment.ExitCode = 1; Application.Exit(); };
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            WriteStartup("Windows Forms initialized.");
            string root = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length == 1 && args[0] == "--start")
            {
                string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyborgVitals");
                string script = Path.Combine(installed, "Start-Monitor.ps1");
                if (!File.Exists(script)) script = Path.Combine(root, "Start-Monitor.ps1");
                if (!File.Exists(script)) throw new FileNotFoundException("Install Cyborg Vitals first using Setup.exe in the extracted project folder.");
                using (var process = Process.Start(Runner.Info(script, "")))
                {
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    string output = stdout.GetAwaiter().GetResult();
                    string error = stderr.GetAwaiter().GetResult();
                    WriteStartup(output + Environment.NewLine + error);
                    if (process.ExitCode != 0) throw new InvalidOperationException(output + Environment.NewLine + error);
                }
                Process.Start(new ProcessStartInfo("ms-gamebar:") { UseShellExecute = true });
                return 0;
            }
            WriteStartup("Constructing setup window.");
            using (var form = new SetupForm(root))
            {
                form.Shown += (s, e) => WriteStartup("Setup window shown.");
                Application.Run(form);
            }
            WriteStartup("Setup window closed.");
            return Environment.ExitCode;
        }
        private static void WriteStartup(string text)
        {
            try { if (startupLog != null) File.AppendAllText(startupLog, DateTime.Now.ToString("O") + " " + text + Environment.NewLine); } catch { }
        }
        private static void Report(Exception error)
        {
            WriteStartup(error.ToString());
            if (System.Threading.Interlocked.Exchange(ref reported, 1) != 0) return;
            try { MessageBoxW(IntPtr.Zero, error.Message + "\r\n\r\nStartup log: " + (startupLog ?? "could not write a log"), "Cyborg Vitals setup error", 0x10); } catch { }
        }
    }

    internal static class Runner
    {
        public static ProcessStartInfo Info(string script, string arguments)
        {
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            return new ProcessStartInfo(powershell, "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" " + arguments)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(script)
            };
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly string root;
        private readonly string installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CyborgVitals");
        private readonly string logPath;
        private readonly TextBox log = new TextBox();
        private readonly Button install = new Button(), start = new Button(), stopReader = new Button(), gamebar = new Button(), openLog = new Button();
        private readonly CheckBox reuse = new CheckBox(), autoStart = new CheckBox(), fanProbing = new CheckBox();
        private readonly Label status = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly object logLock = new object();
        private bool busy;

        public SetupForm(string root)
        {
            this.root = root;
            string logs = Path.Combine(installRoot, "Logs");
            Directory.CreateDirectory(logs);
            logPath = Path.Combine(logs, "setup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            Text = "Cyborg Vitals Setup"; ClientSize = new Size(800, 595); MinimumSize = new Size(720, 535);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(21, 24, 30); ForeColor = Color.White;
            AutoScaleMode = AutoScaleMode.Font;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 7 };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(outer);
            outer.Controls.Add(new Label { Text = "Cyborg Vitals", Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true }, 0, 0);
            outer.Controls.Add(new Label { Text = "Install or repair your Game Bar widget. Progress and errors appear here.\r\nSensor readings require a separate Windows administrator prompt.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(186, 197, 211) }, 0, 1);
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            reuse.Text = "Reuse the build you already completed"; reuse.Checked = true; reuse.AutoSize = true;
            autoStart.Text = "Start optional temperature reader after installation"; autoStart.Checked = false; autoStart.AutoSize = true;
            fanProbing.Text = "Include optional motherboard fan probing when starting the reader"; fanProbing.Checked = false; fanProbing.AutoSize = true;
            options.Controls.Add(reuse); options.Controls.Add(autoStart); options.Controls.Add(fanProbing); outer.Controls.Add(options, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            ConfigureButton(install, "Install / repair", 135); install.BackColor = Color.FromArgb(28, 99, 136);
            ConfigureButton(start, "Start temperature reader", 170);
            ConfigureButton(stopReader, "Stop reader", 105);
            ConfigureButton(gamebar, "Open Game Bar", 140);
            ConfigureButton(openLog, "Open log", 100);
            buttons.Controls.AddRange(new Control[] { install, start, stopReader, gamebar, openLog }); outer.Controls.Add(buttons, 0, 3);
            progress.Dock = DockStyle.Fill; progress.Style = ProgressBarStyle.Blocks; progress.Margin = new Padding(0, 8, 0, 10); outer.Controls.Add(progress, 0, 4);
            log.Dock = DockStyle.Fill; log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Both;
            log.WordWrap = false; log.BackColor = Color.FromArgb(12, 15, 20); log.ForeColor = Color.FromArgb(210, 223, 233);
            log.Font = new Font("Consolas", 9); log.BorderStyle = BorderStyle.FixedSingle; outer.Controls.Add(log, 0, 5);
            status.Text = "Ready. Close any running Cyborg Vitals sensor reader before installing."; status.Dock = DockStyle.Fill;
            status.Padding = new Padding(0, 6, 0, 0); outer.Controls.Add(status, 0, 6);
            install.Enabled = File.Exists(Path.Combine(root, "Build-And-Install.ps1"));
            reuse.Enabled = autoStart.Enabled = install.Enabled;
            if (!install.Enabled) status.Text = "Installed launcher. Use Start sensor reader, then Open Game Bar.";
            install.Click += async (s, e) => await Install();
            start.Click += async (s, e) => await StartReader();
            stopReader.Click += async (s, e) => await StopReader();
            gamebar.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("ms-gamebar:") { UseShellExecute = true }); } catch (Exception ex) { Append(ex.Message); } };
            openLog.Click += (s, e) => { try { Process.Start(new ProcessStartInfo("notepad.exe", "\"" + logPath + "\"") { UseShellExecute = true }); } catch (Exception ex) { Append(ex.Message); } };
            FormClosing += (s, e) => { if (busy) { e.Cancel = true; status.Text = "Please wait for the current operation to finish."; } };
            Append("Cyborg Vitals Setup\r\nProject folder: " + root + "\r\nLog: " + logPath);
        }
        private void ConfigureButton(Button b, string caption, int width)
        {
            b.Text = caption; b.Width = width; b.Height = 34; b.FlatStyle = FlatStyle.Flat;
            b.BackColor = Color.FromArgb(38, 45, 56); b.ForeColor = Color.White; b.Margin = new Padding(0, 3, 8, 3);
        }
        private void Append(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try { lock (logLock) File.AppendAllText(logPath, text + Environment.NewLine, new UTF8Encoding(false)); } catch (IOException) { }
            if (IsDisposed || Disposing) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(AppendDisplay), text); } catch (InvalidOperationException) { } }
            else AppendDisplay(text);
        }
        private void AppendDisplay(string text) { if (!IsDisposed && !log.IsDisposed) log.AppendText(text + Environment.NewLine); }
        private void SetBusy(bool value)
        {
            busy = value; install.Enabled = !value && File.Exists(Path.Combine(root, "Build-And-Install.ps1"));
            start.Enabled = stopReader.Enabled = gamebar.Enabled = fanProbing.Enabled = !value; reuse.Enabled = autoStart.Enabled = !value && install.Enabled;
            progress.Style = value ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        }
        private async Task<int> Run(string script, string args)
        {
            if (!File.Exists(script)) throw new FileNotFoundException("Required setup file is missing. Extract the entire ZIP together.", script);
            using (var process = new Process { StartInfo = Runner.Info(script, args), EnableRaisingEvents = true })
            {
                process.OutputDataReceived += (s, e) => { if (e.Data != null) Append(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) Append(e.Data); };
                process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                return await Task.Run(() => { process.WaitForExit(); return process.ExitCode; });
            }
        }
        private async Task Install()
        {
            SetBusy(true); status.Text = "Installing...";
            try
            {
                int result = await Run(Path.Combine(root, "Build-And-Install.ps1"), reuse.Checked ? "-PreferExistingBuild" : "");
                if (result != 0) { status.Text = "Installation failed. Click Open log and share the last error."; return; }
                status.Text = "Installed. Open Win+G > Widget menu > Cyborg Vitals and pin it.";
                if (autoStart.Checked)
                {
                    result = await Run(StartScript(), ReaderArguments());
                    if (result != 0) status.Text = "Widget installed; sensor reader did not start. See the log.";
                    else Process.Start(new ProcessStartInfo("ms-gamebar:") { UseShellExecute = true });
                }
            }
            catch (Exception ex) { Append(ex.ToString()); status.Text = "Setup failed. Click Open log for details."; }
            finally { SetBusy(false); }
        }
        private string StartScript()
        {
            string script = Path.Combine(installRoot, "Start-Monitor.ps1");
            return File.Exists(script) ? script : Path.Combine(root, "Start-Monitor.ps1");
        }
        private async Task StartReader()
        {
            SetBusy(true); status.Text = "Starting sensor reader; check for the Windows administrator prompt...";
            try { int result = await Run(StartScript(), ReaderArguments()); status.Text = result == 0 ? "Reader started. Enable temperature/fan rows in the widget settings." : "Reader did not start. See the log."; }
            catch (Exception ex) { Append(ex.ToString()); status.Text = "Reader did not start. See the log."; }
            finally { SetBusy(false); }
        }
        private string ReaderArguments() { return "-EnableHardwareSensors" + (fanProbing.Checked ? " -EnableMotherboardFanProbing" : ""); }
        private async Task StopReader()
        {
            SetBusy(true); status.Text = "Stopping the background reader...";
            try
            {
                string script = Path.Combine(installRoot, "Stop-Monitor.ps1");
                if (!File.Exists(script)) script = Path.Combine(root, "Stop-Monitor.ps1");
                int result = await Run(script, "");
                status.Text = result == 0 ? "Reader stopped. CPU/RAM remain available in the widget." : "Reader has not stopped. See the log.";
            }
            catch (Exception ex) { Append(ex.ToString()); status.Text = "Reader has not stopped. See the log."; }
            finally { SetBusy(false); }
        }
    }
}
