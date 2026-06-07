using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class InstallerProgram
{
    private const string AppName = "WinZoneTrigger";
    private const string DisplayName = "위치 자동 실행";
    private const string Publisher = "WinZoneTrigger";
    private const string Version = "1.0.0";
    private const string AppResourceName = "WinZoneTrigger.exe";
    private const string ReadmeResourceName = "README.md";

    private static readonly Color UiBackground = Color.FromArgb(246, 247, 242);
    private static readonly Color UiSurface = Color.FromArgb(253, 253, 249);
    private static readonly Color UiSurfaceMuted = Color.FromArgb(239, 243, 235);
    private static readonly Color UiBorder = Color.FromArgb(211, 218, 207);
    private static readonly Color UiText = Color.FromArgb(35, 45, 47);
    private static readonly Color UiTextMuted = Color.FromArgb(97, 111, 103);
    private static readonly Color UiAccent = Color.FromArgb(31, 122, 92);
    private static readonly Color UiAccentDark = Color.FromArgb(20, 91, 69);
    private static readonly Color UiAccentSoft = Color.FromArgb(220, 240, 229);
    private static readonly Color UiDanger = Color.FromArgb(176, 70, 61);
    private static readonly Color UiLogBackground = Color.FromArgb(35, 43, 43);
    private static readonly Color UiLogText = Color.FromArgb(232, 238, 232);

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool uninstall = HasArg(args, "/uninstall") || HasArg(args, "-uninstall");
        bool install = HasArg(args, "/install") || HasArg(args, "-install");
        bool silent = HasArg(args, "/silent") || HasArg(args, "-silent") || HasArg(args, "/quiet") || HasArg(args, "-quiet");
        bool noStartup = HasArg(args, "/nostartup") || HasArg(args, "-nostartup");
        bool noLaunch = HasArg(args, "/nolaunch") || HasArg(args, "-nolaunch");

        if (uninstall)
        {
            return RunWithoutUi(() => InstallerActions.Uninstall(null), silent);
        }

        if (install || silent)
        {
            return RunWithoutUi(() => InstallerActions.Install(!noStartup, !noLaunch && !silent, null), silent);
        }

        Application.Run(new InstallerForm());
        return 0;
    }

    private static int RunWithoutUi(Action action, bool silent)
    {
        try
        {
            action();
            if (!silent)
            {
                MessageBox.Show("작업이 완료되었습니다.", DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
            {
                MessageBox.Show(ex.Message, DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 1;
        }
    }

    private static bool HasArg(string[] args, string value)
    {
        foreach (string arg in args)
        {
            if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class InstallerForm : Form
    {
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _launchCheck;
        private readonly TextBox _logBox;
        private readonly Button _installButton;
        private readonly Button _uninstallButton;
        private readonly Button _closeButton;

        public InstallerForm()
        {
            Text = DisplayName + " 설치";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(600, 460);
            BackColor = UiBackground;
            ForeColor = UiText;
            Font = new Font("Malgun Gothic", 9.25F);

            Panel header = new Panel
            {
                BackColor = UiAccentSoft,
                Location = new Point(0, 0),
                Size = new Size(600, 102)
            };
            Controls.Add(header);

            Label title = new Label
            {
                AutoSize = false,
                Text = "위치 자동 실행 설치",
                Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
                ForeColor = UiAccentDark,
                Location = new Point(24, 22),
                Size = new Size(512, 28)
            };
            header.Controls.Add(title);

            Label description = new Label
            {
                AutoSize = false,
                Text = "현재 사용자 계정에 앱을 설치하고 시작 메뉴, 자동 시작, 삭제 항목을 등록합니다.",
                ForeColor = UiTextMuted,
                Location = new Point(26, 54),
                Size = new Size(512, 24)
            };
            header.Controls.Add(description);

            Label pathLabel = new Label
            {
                AutoSize = false,
                Text = "설치 위치",
                ForeColor = UiTextMuted,
                Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold),
                Location = new Point(24, 124),
                Size = new Size(92, 24)
            };
            Controls.Add(pathLabel);

            TextBox pathBox = new TextBox
            {
                ReadOnly = true,
                Text = Paths.InstallDir,
                BackColor = UiSurface,
                ForeColor = UiText,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(122, 120),
                Size = new Size(454, 25)
            };
            Controls.Add(pathBox);

            _startupCheck = new CheckBox
            {
                Text = "Windows 시작 시 자동 실행",
                Checked = true,
                AutoSize = true,
                FlatStyle = FlatStyle.Standard,
                BackColor = UiBackground,
                ForeColor = UiText,
                Location = new Point(122, 164)
            };
            Controls.Add(_startupCheck);

            _launchCheck = new CheckBox
            {
                Text = "설치 후 앱 실행",
                Checked = true,
                AutoSize = true,
                FlatStyle = FlatStyle.Standard,
                BackColor = UiBackground,
                ForeColor = UiText,
                Location = new Point(122, 194)
            };
            Controls.Add(_launchCheck);

            _logBox = new TextBox
            {
                ReadOnly = true,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = UiLogBackground,
                ForeColor = UiLogText,
                Font = new Font("Consolas", 9F),
                BorderStyle = BorderStyle.None,
                Location = new Point(24, 238),
                Size = new Size(552, 148)
            };
            Controls.Add(_logBox);

            _installButton = new Button
            {
                Text = "설치",
                Location = new Point(304, 410),
                Size = new Size(88, 32)
            };
            StyleButton(_installButton, true);
            _installButton.Click += InstallButtonClick;
            Controls.Add(_installButton);

            _uninstallButton = new Button
            {
                Text = "제거",
                Location = new Point(400, 410),
                Size = new Size(88, 32)
            };
            StyleDangerButton(_uninstallButton);
            _uninstallButton.Click += UninstallButtonClick;
            Controls.Add(_uninstallButton);

            _closeButton = new Button
            {
                Text = "닫기",
                Location = new Point(496, 410),
                Size = new Size(80, 32)
            };
            StyleButton(_closeButton, false);
            _closeButton.Click += delegate { Close(); };
            Controls.Add(_closeButton);

            AcceptButton = _installButton;
            CancelButton = _closeButton;
            Log("설치 준비가 완료되었습니다.");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (Icon != null)
            {
                Icon icon = Icon;
                Icon = null;
                icon.Dispose();
            }

            base.OnFormClosed(e);
        }

        private void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Standard;
            button.UseVisualStyleBackColor = true;
            button.Cursor = Cursors.Hand;
            button.ForeColor = SystemColors.ControlText;
        }

        private void StyleDangerButton(Button button)
        {
            button.FlatStyle = FlatStyle.Standard;
            button.UseVisualStyleBackColor = true;
            button.Cursor = Cursors.Hand;
            button.ForeColor = SystemColors.ControlText;
        }

        private void InstallButtonClick(object sender, EventArgs e)
        {
            RunUiAction("설치", () => InstallerActions.Install(_startupCheck.Checked, _launchCheck.Checked, Log));
        }

        private void UninstallButtonClick(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("위치 자동 실행을 제거할까요?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            RunUiAction("제거", () => InstallerActions.Uninstall(Log));
        }

        private void RunUiAction(string name, Action action)
        {
            SetBusy(true);
            try
            {
                action();
                Log(name + " 완료");
                MessageBox.Show(name + "가 완료되었습니다.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log(name + " 실패: " + ex.Message);
                MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            _installButton.Enabled = !busy;
            _uninstallButton.Enabled = !busy;
            _closeButton.Enabled = !busy;
            UseWaitCursor = busy;
        }

        private void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            _logBox.AppendText(line + Environment.NewLine);
        }
    }

    private static class Paths
    {
        public static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppName);

        public static readonly string InstallExe = Path.Combine(InstallDir, AppName + ".exe");
        public static readonly string UninstallExe = Path.Combine(InstallDir, AppName + "_Uninstall.exe");
        public static readonly string ReadmePath = Path.Combine(InstallDir, "README.md");

        public static readonly string StartMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            AppName);

        public static readonly string AppShortcut = Path.Combine(StartMenuDir, DisplayName + ".lnk");
        public static readonly string UninstallShortcut = Path.Combine(StartMenuDir, DisplayName + " 제거.lnk");
        public static readonly string OldStartupShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            AppName + ".lnk");
    }

    private static class InstallerActions
    {
        public static void Install(bool registerStartup, bool launchAfterInstall, Action<string> log)
        {
            Log(log, "실행 중인 앱을 정리합니다.");
            StopRunningApp();

            Log(log, "설치 폴더를 준비합니다.");
            Directory.CreateDirectory(Paths.InstallDir);

            Log(log, "앱 파일을 복사합니다.");
            ExtractResource(AppResourceName, Paths.InstallExe);
            ExtractResource(ReadmeResourceName, Paths.ReadmePath);
            File.Copy(Application.ExecutablePath, Paths.UninstallExe, true);

            Log(log, "시작 메뉴 바로가기를 만듭니다.");
            Directory.CreateDirectory(Paths.StartMenuDir);
            CreateShortcut(Paths.AppShortcut, Paths.InstallExe, "", Paths.InstallDir, Paths.InstallExe, "위치 자동 실행");
            CreateShortcut(Paths.UninstallShortcut, Paths.UninstallExe, "/uninstall", Paths.InstallDir, Paths.UninstallExe, "위치 자동 실행 제거");

            DeleteIfExists(Paths.OldStartupShortcut);

            if (registerStartup)
            {
                Log(log, "Windows 시작 시 자동 실행을 등록합니다.");
                SetRunKey(true);
            }
            else
            {
                Log(log, "Windows 시작 시 자동 실행을 해제합니다.");
                SetRunKey(false);
            }

            Log(log, "프로그램 제거 항목을 등록합니다.");
            RegisterUninstallEntry();

            if (launchAfterInstall)
            {
                Log(log, "앱을 실행합니다.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.InstallExe,
                    Arguments = "--minimized",
                    WorkingDirectory = Paths.InstallDir,
                    UseShellExecute = true
                });
            }
        }

        public static void Uninstall(Action<string> log)
        {
            Log(log, "실행 중인 앱을 종료합니다.");
            StopRunningApp();

            Log(log, "Windows 시작 자동 실행을 제거합니다.");
            SetRunKey(false);

            Log(log, "시작 메뉴 바로가기를 제거합니다.");
            DeleteDirectoryIfExists(Paths.StartMenuDir);
            DeleteIfExists(Paths.OldStartupShortcut);

            Log(log, "프로그램 제거 항목을 제거합니다.");
            UnregisterUninstallEntry();

            Log(log, "설치 파일을 제거합니다.");
            DeleteInstallDirectory();
        }

        private static void StopRunningApp()
        {
            foreach (Process process in Process.GetProcessesByName(AppName))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch
                {
                    // The app may already be closing or owned by another session.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static void ExtractResource(string resourceName, string destinationPath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream input = assembly.GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    if (resourceName == ReadmeResourceName)
                    {
                        return;
                    }

                    throw new InvalidOperationException("설치 리소스를 찾을 수 없습니다: " + resourceName);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                using (FileStream output = File.Create(destinationPath))
                {
                    input.CopyTo(output);
                }
            }
        }

        private static void SetRunKey(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Windows 시작 실행 레지스트리를 열 수 없습니다.");
                }

                if (enabled)
                {
                    key.SetValue(AppName, Quote(Paths.InstallExe) + " --startup --minimized", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }
            }
        }

        private static void RegisterUninstallEntry()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("프로그램 제거 레지스트리를 열 수 없습니다.");
                }

                key.SetValue("DisplayName", DisplayName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", Version, RegistryValueKind.String);
                key.SetValue("Publisher", Publisher, RegistryValueKind.String);
                key.SetValue("InstallLocation", Paths.InstallDir, RegistryValueKind.String);
                key.SetValue("DisplayIcon", Paths.InstallExe, RegistryValueKind.String);
                key.SetValue("UninstallString", Quote(Paths.UninstallExe) + " /uninstall", RegistryValueKind.String);
                key.SetValue("QuietUninstallString", Quote(Paths.UninstallExe) + " /uninstall /silent", RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", EstimateInstalledSizeKb(), RegistryValueKind.DWord);
            }
        }

        private static void UnregisterUninstallEntry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                if (key != null)
                {
                    key.DeleteSubKeyTree(AppName, false);
                }
            }
        }

        private static int EstimateInstalledSizeKb()
        {
            long size = 0;
            string[] files = new[] { Paths.InstallExe, Paths.UninstallExe, Paths.ReadmePath };
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    size += new FileInfo(file).Length;
                }
            }

            return (int)Math.Max(1, size / 1024);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconPath, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("Windows 바로가기 기능을 사용할 수 없습니다.");
            }

            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    Marshal.ReleaseComObject(shortcut);
                }

                if (shell != null && Marshal.IsComObject(shell))
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
        }

        private static void DeleteInstallDirectory()
        {
            if (!Directory.Exists(Paths.InstallDir))
            {
                return;
            }

            string currentExe = Path.GetFullPath(Application.ExecutablePath);
            string installDir = Path.GetFullPath(Paths.InstallDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool runningFromInstallDir = currentExe.StartsWith(installDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (!runningFromInstallDir)
            {
                Directory.Delete(Paths.InstallDir, true);
                return;
            }

            foreach (string file in Directory.GetFiles(Paths.InstallDir, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFullPath(file), currentExe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string directory in Directory.GetDirectories(Paths.InstallDir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch
                {
                }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c timeout /t 2 /nobreak >nul & rmdir /s /q " + Quote(Paths.InstallDir),
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        private static void Log(Action<string> log, string message)
        {
            if (log != null)
            {
                log(message);
            }
        }
    }
}
