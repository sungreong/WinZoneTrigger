using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal static class Program
    {
        private static Mutex _singleInstanceMutex;

        [STAThread]
        private static int Main(string[] args)
        {
            if (HasArgument(args, "--scan-helper"))
            {
                return ScanHelper.Run(args);
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                DiagnosticsLog.Write("UI 스레드 예외", e == null ? null : e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                DiagnosticsLog.Write("처리되지 않은 앱 예외", e == null ? null : e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
            {
                DiagnosticsLog.Write("관찰되지 않은 작업 예외", e == null ? null : e.Exception);
                if (e != null)
                {
                    e.SetObserved();
                }
            };
            Application.ApplicationExit += delegate
            {
                DiagnosticsLog.WriteEvent("앱 종료: ApplicationExit");
            };

            ConfigureCrashDumps();

            List<ProcessParentInfo> parentChain = ProcessDiagnostics.GetParentChain(Process.GetCurrentProcess().Id, 8);
            DiagnosticsLog.WriteEvent("앱 시작: pid=" + Process.GetCurrentProcess().Id
                + " / exe=" + Application.ExecutablePath
                + " / args=" + FormatArguments(args)
                + " / parents=" + ProcessDiagnostics.FormatParentChain(parentChain));

            try
            {
                bool startMinimized = args != null && args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
                bool startedFromWindowsStartup = args != null && args.Any(a => string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));
                TryRepairStartupRegistration();
                if (startMinimized)
                {
                    if (!TryAcquireSingleInstance(@"Local\WinZoneTrigger.BackgroundInstance", "백그라운드"))
                    {
                        return 0;
                    }

                    DiagnosticsLog.WriteEvent("최소화 시작: 백그라운드 자동 실행 컨텍스트를 사용합니다.");
                    Application.Run(new BackgroundAutomationContext());
                    return 0;
                }

                if (!TryAcquireSingleInstance(@"Local\WinZoneTrigger.SettingsInstance", "설정 화면"))
                {
                    return 0;
                }

                EnsureBackgroundAutomationStartedForSettings();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(startMinimized, startedFromWindowsStartup, false));
                return 0;
            }
            finally
            {
                ReleaseSingleInstance();
            }
        }

        private static bool TryAcquireSingleInstance(string mutexName, string instanceName)
        {
            try
            {
                bool createdNew;
                _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
                if (createdNew)
                {
                    DiagnosticsLog.WriteEvent(instanceName + " 단일 인스턴스 잠금 획득");
                    return true;
                }

                DiagnosticsLog.WriteEvent("기존 " + instanceName + " 인스턴스 감지: 새 인스턴스를 종료합니다.");
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return false;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write(instanceName + " 단일 인스턴스 잠금 실패", ex);
                return true;
            }
        }

        private static void ReleaseSingleInstance()
        {
            if (_singleInstanceMutex == null)
            {
                return;
            }

            try
            {
                _singleInstanceMutex.ReleaseMutex();
                DiagnosticsLog.WriteEvent("단일 인스턴스 잠금 해제");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("단일 인스턴스 잠금 해제 실패", ex);
            }
            finally
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }

        private static void ConfigureCrashDumps()
        {
            try
            {
                string dumpFolder = Path.Combine(ConfigStore.ConfigDirectory, "dumps");
                Directory.CreateDirectory(dumpFolder);

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\Windows Error Reporting\LocalDumps\WinZoneTrigger.exe"))
                {
                    if (key == null)
                    {
                        return;
                    }

                    key.SetValue("DumpFolder", dumpFolder, RegistryValueKind.ExpandString);
                    key.SetValue("DumpCount", 5, RegistryValueKind.DWord);
                    key.SetValue("DumpType", 2, RegistryValueKind.DWord);
                }

                DiagnosticsLog.WriteEvent("크래시 덤프 설정: " + dumpFolder);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("크래시 덤프 설정 실패", ex);
            }
        }

        private static string FormatArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "none";
            }

            return string.Join(" ", args.Select(QuoteArgument).ToArray());
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static bool HasArgument(string[] args, string value)
        {
            return args != null && args.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
        }

        private static void TryRepairStartupRegistration()
        {
            try
            {
                AppConfig config = ConfigStore.Load();
                config.Normalize();
                StartupManager.EnsurePreferredRegistration(config.StartMinimized);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("자동 시작 등록 자가 복구 확인 실패", ex);
            }
        }

        private static void EnsureBackgroundAutomationStartedForSettings()
        {
            try
            {
                AppConfig config = ConfigStore.Load();
                config.Normalize();
                if (!HasBackgroundAutomationWork(config))
                {
                    DiagnosticsLog.WriteEvent("설정 화면 시작: 백그라운드 자동 실행 대상이 없습니다.");
                    return;
                }

                if (IsBackgroundInstanceRunning())
                {
                    DiagnosticsLog.WriteEvent("설정 화면 시작: 백그라운드 자동 실행 프로세스가 이미 실행 중입니다.");
                    return;
                }

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--minimized";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                Process.Start(startInfo);
                DiagnosticsLog.WriteEvent("설정 화면 시작: 백그라운드 자동 실행 프로세스를 시작했습니다.");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("설정 화면의 백그라운드 자동 실행 시작 확인 실패", ex);
            }
        }

        private static bool HasBackgroundAutomationWork(AppConfig config)
        {
            return config != null
                && (config.BrightnessScheduleEnabled
                    || (config.Zones != null
                        && config.Zones.Any(z => z != null
                            && z.Enabled
                            && (z.RunOnceAtStartup.GetValueOrDefault(true)
                                || z.MonitoringEnabled.GetValueOrDefault(false)
                                || z.GetEnabledAppWatchItems().Any()))));
        }

        private static bool IsBackgroundInstanceRunning()
        {
            try
            {
                using (Mutex.OpenExisting(@"Local\WinZoneTrigger.BackgroundInstance"))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }
    }
}
