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
    internal sealed class AppWatchCheckResult
    {
        public string ProcessName { get; set; }
        public int ProcessCount { get; set; }
        public int MainWindowCount { get; set; }
        public int VisibleWindowCount { get; set; }
        public bool IsRunning { get; set; }
        public bool RequiresVisibleWindow { get; set; }
        public bool HasVisibleWindow { get; set; }
        public bool MeetsRequirement { get; set; }
        public bool LaunchAttempted { get; set; }
        public IntPtr WindowHandle { get; set; }
        public string Summary { get; set; }
    }

    internal sealed class AppWatchZoneResult
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string LaunchTarget { get; set; }
        public DateTime CheckedAtLocal { get; set; }
        public DateTime? NextCheckAtLocal { get; set; }
        public AppWatchCheckResult Result { get; set; }
        public string Error { get; set; }
    }

    internal static class AppWatchdog
    {
        public static string InferProcessName(string target)
        {
            return NormalizeProcessName(target);
        }

        public static string NormalizeProcessName(string value)
        {
            string name = (value ?? "").Trim().Trim('"');
            if (name.Length == 0)
            {
                return "";
            }

            string appsFolderName = TryGetAppsFolderProcessName(name);
            if (!string.IsNullOrWhiteSpace(appsFolderName))
            {
                return appsFolderName;
            }

            try
            {
                string fileName = Path.GetFileName(name);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    name = fileName;
                }
            }
            catch
            {
            }

            string extension = "";
            try
            {
                extension = Path.GetExtension(name);
            }
            catch
            {
            }

            if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    name = Path.GetFileNameWithoutExtension(name);
                }
                catch
                {
                }
            }

            return name.Trim();
        }

        private static string TryGetAppsFolderProcessName(string target)
        {
            string value = (target ?? "").Trim();
            const string prefix = @"shell:AppsFolder\";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string appId = value.Substring(prefix.Length);
            int bang = appId.IndexOf('!');
            if (bang > 0)
            {
                appId = appId.Substring(0, bang);
            }

            int underscore = appId.IndexOf('_');
            if (underscore > 0)
            {
                appId = appId.Substring(0, underscore);
            }

            int dot = appId.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < appId.Length)
            {
                appId = appId.Substring(dot + 1);
            }

            return appId.Trim();
        }

        public static AppWatchCheckResult Check(string processName)
        {
            return Check(processName, false);
        }

        public static AppWatchCheckResult Check(string processName, bool requireVisibleWindow)
        {
            string normalized = NormalizeProcessName(processName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("확인할 프로세스 이름이 비어 있습니다.");
            }

            Process[] processes = Process.GetProcessesByName(normalized);
            try
            {
                int count = processes == null ? 0 : processes.Length;
                bool isRunning = count > 0;
                AppWatchCheckResult result = new AppWatchCheckResult
                {
                    ProcessName = normalized,
                    ProcessCount = count,
                    MainWindowCount = 0,
                    VisibleWindowCount = 0,
                    IsRunning = isRunning,
                    RequiresVisibleWindow = false,
                    HasVisibleWindow = isRunning,
                    MeetsRequirement = isRunning,
                    LaunchAttempted = false,
                    WindowHandle = IntPtr.Zero
                };
                result.Summary = BuildCheckSummary(result);
                return result;
            }
            finally
            {
                if (processes != null)
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        public static AppWatchCheckResult EnsureRunning(string processName, string launchTarget, bool requireVisibleWindow, Action<string> log)
        {
            AppWatchCheckResult current = Check(processName, false);
            if (current.MeetsRequirement)
            {
                return current;
            }

            string target = (launchTarget ?? "").Trim();
            if (target.Length == 0)
            {
                throw new InvalidOperationException("다시 실행할 앱 대상이 비어 있습니다.");
            }

            if (log != null)
            {
                log("앱 감시: 꺼진 상태라 다시 실행합니다: " + current.ProcessName);
            }

            AppLauncher.LaunchApp(target, log ?? delegate { });
            AppWatchCheckResult afterLaunch = WaitForRequirement(processName, false);

            afterLaunch.LaunchAttempted = true;
            afterLaunch.Summary = BuildLaunchSummary(afterLaunch);
            return afterLaunch;
        }

        private static AppWatchCheckResult WaitForRequirement(string processName, bool requireVisibleWindow)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(requireVisibleWindow ? 8000 : 2000);
            AppWatchCheckResult latest = Check(processName, requireVisibleWindow);
            while (!latest.MeetsRequirement && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
                latest = Check(processName, requireVisibleWindow);
            }

            return latest;
        }

        private static string BuildCheckSummary(AppWatchCheckResult result)
        {
            if (result == null || !result.IsRunning)
            {
                return "실행 중 아님: " + (result == null ? "" : result.ProcessName);
            }

            string windowSummary = result.VisibleWindowCount > 0
                ? ", 표시 창 " + result.VisibleWindowCount + "개"
                : result.MainWindowCount > 0 ? ", 숨겨진 창 " + result.MainWindowCount + "개" : ", 창 없음";

            if (result.RequiresVisibleWindow && !result.HasVisibleWindow)
            {
                return "실행 중이지만 표시 창 없음: " + result.ProcessName + " (" + result.ProcessCount + "개" + windowSummary + ")";
            }

            return "실행 중: " + result.ProcessName + " (" + result.ProcessCount + "개" + windowSummary + ")";
        }

        private static string BuildLaunchSummary(AppWatchCheckResult result)
        {
            if (result == null)
            {
                return "실행 요청 완료, 아직 프로세스 미확인";
            }

            if (result.MeetsRequirement)
            {
                return result.RequiresVisibleWindow
                    ? "다시 열림: " + result.ProcessName + " (" + result.ProcessCount + "개, 표시 창 " + result.VisibleWindowCount + "개)"
                    : "다시 실행됨: " + result.ProcessName + " (" + result.ProcessCount + "개)";
            }

            if (result.IsRunning && result.RequiresVisibleWindow)
            {
                return "실행 요청 완료, 표시 창 아직 없음: " + result.ProcessName + " (" + result.ProcessCount + "개)";
            }

            return "실행 요청 완료, 아직 프로세스 미확인: " + result.ProcessName;
        }

    }
}
