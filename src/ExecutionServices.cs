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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace WinZoneTrigger
{
    internal static class ZoneExecutor
    {
        public static void Execute(ZoneRule zone, Action<string> log)
        {
            if (log == null)
            {
                log = delegate { };
            }

            log("동작 실행 시작: " + zone.Name);
            bool wifiConnectRequested = zone.ConnectWifiEnabled.GetValueOrDefault(false);
            bool wifiConnectSucceeded = !wifiConnectRequested;

            try
            {
                if (wifiConnectRequested)
                {
                    if (string.IsNullOrWhiteSpace(zone.ConnectProfile))
                    {
                        log("Wi-Fi 연결 실패: 연결 프로필이 비어 있습니다.");
                    }
                    else
                    {
                        string ssid = string.IsNullOrWhiteSpace(zone.ConnectSsid) ? zone.ConnectProfile : zone.ConnectSsid;
                        CommandResult connectResult = WifiActions.Connect(zone.ConnectProfile, ssid);
                        wifiConnectSucceeded = connectResult.Succeeded;
                        log((connectResult.Succeeded ? "Wi-Fi 연결 성공: " : "Wi-Fi 연결 실패: ") + ssid + " -> " + connectResult.Summary);
                        if (connectResult.Succeeded)
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                wifiConnectSucceeded = false;
                log("Wi-Fi 연결 실패: " + ex.Message);
            }

            List<string> chromeUrls = zone.ChromeUrls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();
            if (chromeUrls.Count > 0)
            {
                if (wifiConnectRequested && !wifiConnectSucceeded)
                {
                    log("Chrome 탭 실행 건너뜀: Wi-Fi 연결이 성공하지 않았습니다.");
                }
                else
                {
                    try
                    {
                        AppLauncher.OpenChromeUrls(chromeUrls, log);
                    }
                    catch (Exception ex)
                    {
                        log("Chrome 실행 실패: " + ex.Message);
                    }
                }
            }

            try
            {
                if (string.Equals(zone.AudioAction, "Mute", StringComparison.OrdinalIgnoreCase))
                {
                    AudioController.SetMute(true);
                    log("소리 동작 성공: 음소거");
                }
                else if (string.Equals(zone.AudioAction, "Unmute", StringComparison.OrdinalIgnoreCase))
                {
                    AudioController.SetMute(false);
                    log("소리 동작 성공: 음소거 해제");
                }
            }
            catch (Exception ex)
            {
                log("소리 동작 실패: " + ex.Message);
            }

            foreach (string app in zone.AppLaunches.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                try
                {
                    AppLauncher.LaunchAppIfNotRunning(app, log);
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Write("앱 실행 실패: " + app, ex);
                    log("앱 실행 실패: " + app + " -> " + ex.Message);
                }
            }

            foreach (string command in zone.Commands.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                try
                {
                    CommandResult result = CommandRunner.Run("cmd.exe", "/c " + command, 20000);
                    log((result.Succeeded ? "명령어 성공: " : "명령어 실패: ") + command + " -> " + result.Summary);
                }
                catch (Exception ex)
                {
                    log("명령어 실패: " + command + " -> " + ex.Message);
                }
            }

            log("동작 실행 종료: " + zone.Name);
        }
    }

    internal static class WifiActions
    {
        public static CommandResult Connect(string profileName, string ssid)
        {
            string arguments = "wlan connect name=" + Quote(profileName);
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                arguments += " ssid=" + Quote(ssid);
            }

            return CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "netsh.exe"), arguments, 15000);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }

        public bool Succeeded
        {
            get { return !TimedOut && ExitCode == 0; }
        }

        public string Summary
        {
            get
            {
                if (TimedOut)
                {
                    return "시간 초과";
                }

                string text = FirstMeaningfulLine(Output);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = FirstMeaningfulLine(Error);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    text = "종료 코드 " + ExitCode;
                }

                return "종료 코드 " + ExitCode + " / " + text;
            }
        }

        private static string FirstMeaningfulLine(string text)
        {
            return CommandOutputFormatter.FirstMeaningfulLine(text, 160);
        }
    }

    internal static class CommandOutputFormatter
    {
        public static string FirstMeaningfulLine(string text, int maxLength)
        {
            string summary = SummarizeForUser(text);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "";
            }

            string[] lines = summary.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return TrimToLength(trimmed, maxLength);
                }
            }

            return "";
        }

        public static string SummarizeForUser(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            string normalized = text.Trim();
            if (normalized.StartsWith("#< CLIXML", StringComparison.OrdinalIgnoreCase))
            {
                string parsed = TryParseCliXml(normalized);
                if (!string.IsNullOrWhiteSpace(parsed))
                {
                    return parsed;
                }

                string fallback = FirstNonCliXmlLine(normalized);
                return string.IsNullOrWhiteSpace(fallback) ? "PowerShell 위치 API 오류" : fallback;
            }

            return DecodePowerShellEscapes(normalized);
        }

        public static string FirstRawLines(string text, int maxLines, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            List<string> lines = text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Take(Math.Max(1, maxLines))
                .ToList();

            return TrimToLength(string.Join(" / ", lines.ToArray()), maxLength);
        }

        private static string TryParseCliXml(string text)
        {
            int xmlStart = text.IndexOf("<Objs", StringComparison.OrdinalIgnoreCase);
            if (xmlStart < 0)
            {
                int firstLineEnd = text.IndexOf('\n');
                xmlStart = firstLineEnd < 0 ? -1 : text.IndexOf('<', firstLineEnd + 1);
            }
            if (xmlStart < 0 || xmlStart >= text.Length)
            {
                return "";
            }

            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(text.Substring(xmlStart));
                XmlNodeList nodes = document.SelectNodes("//*[local-name()='S' and @S='Error']");
                if (nodes == null || nodes.Count == 0)
                {
                    nodes = document.SelectNodes("//*[local-name()='S']");
                }

                List<string> parts = new List<string>();
                if (nodes != null)
                {
                    foreach (XmlNode node in nodes)
                    {
                        string value = CleanCliXmlText(node.InnerText);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            parts.Add(value);
                        }
                    }
                }

                if (parts.Count == 0)
                {
                    return "";
                }

                return CollapseLines(string.Join(Environment.NewLine, parts.ToArray()));
            }
            catch
            {
                return "";
            }
        }

        private static string FirstNonCliXmlLine(string text)
        {
            string[] lines = text.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = CleanCliXmlText(line);
                if (trimmed.Length > 0 && !trimmed.StartsWith("#< CLIXML", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }
            }

            return "";
        }

        private static string CleanCliXmlText(string text)
        {
            return CollapseLines(DecodePowerShellEscapes(text));
        }

        private static string DecodePowerShellEscapes(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return Regex.Replace(text, "_x([0-9A-Fa-f]{4})_", delegate(Match match)
            {
                int value;
                if (int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                {
                    return Convert.ToChar(value).ToString();
                }

                return match.Value;
            });
        }

        private static string CollapseLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return string.Join(Environment.NewLine, text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToArray());
        }

        private static string TrimToLength(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0 || text.Length <= maxLength)
            {
                return text ?? "";
            }

            return text.Substring(0, maxLength);
        }
    }

    internal static class CommandRunner
    {
        public static CommandResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            CommandResult result = new CommandResult { ExitCode = -1, Output = "", Error = "" };

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = fileName;
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;
                startInfo.StandardOutputEncoding = Encoding.Default;
                startInfo.StandardErrorEncoding = Encoding.Default;

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        result.Error = "프로세스를 시작하지 못했습니다.";
                        return result;
                    }

                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        result.TimedOut = true;
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }

                    result.Output = process.StandardOutput.ReadToEnd();
                    result.Error = process.StandardError.ReadToEnd();
                    if (!result.TimedOut)
                    {
                        result.ExitCode = process.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }
    }

    internal static class StartupManager
    {
        private const string ShortcutName = "WinZoneTrigger.lnk";
        private const string TaskName = "WinZoneTrigger";
        private const string RunValueName = "WinZoneTrigger";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled()
        {
            return IsScheduledTaskEnabled() || IsRunKeyEnabled() || File.Exists(GetShortcutPath());
        }

        public static string GetStartupStatusSummary()
        {
            List<string> modes = new List<string>();
            if (IsScheduledTaskEnabled())
            {
                modes.Add("작업 스케줄러");
            }
            if (IsRunKeyEnabled())
            {
                modes.Add("Run 레지스트리");
            }
            if (File.Exists(GetShortcutPath()))
            {
                modes.Add("시작 폴더");
            }

            return modes.Count == 0 ? "미등록" : string.Join(", ", modes.ToArray());
        }

        public static void SetEnabled(bool enabled, bool startMinimized)
        {
            if (!enabled)
            {
                DeleteScheduledTask();
                DeleteRunKey();
                DeleteStartupShortcut();
                DiagnosticsLog.WriteEvent("자동 시작 해제: 미등록");
                return;
            }

            DeleteStartupShortcut();
            DeleteRunKey();

            try
            {
                CreateScheduledTask(startMinimized);
                DiagnosticsLog.WriteEvent("자동 시작 등록: 작업 스케줄러");
                return;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.WriteEvent("작업 스케줄러 자동 시작 등록 실패, Run 레지스트리로 대체: " + ex.Message);
                WriteRunKey(startMinimized);
                DiagnosticsLog.WriteEvent("자동 시작 등록: Run 레지스트리");
            }
        }

        private static bool IsScheduledTaskEnabled()
        {
            CommandResult result = RunSchtasks("/Query /TN " + Quote(TaskName));
            return result.Succeeded;
        }

        private static void CreateScheduledTask(bool startMinimized)
        {
            string taskRun = BuildStartupCommand(startMinimized);
            string schtasksArguments =
                "/Create /F /SC ONLOGON /TN " + Quote(TaskName) +
                " /TR " + Quote(taskRun) +
                " /RL LIMITED";

            CommandResult result = RunSchtasks(schtasksArguments);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("작업 스케줄러 등록 실패: " + result.Summary);
            }
        }

        private static void DeleteScheduledTask()
        {
            RunSchtasks("/Delete /F /TN " + Quote(TaskName));
        }

        private static CommandResult RunSchtasks(string arguments)
        {
            return CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments, 15000);
        }

        private static void DeleteStartupShortcut()
        {
            string path = GetShortcutPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool IsRunKeyEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                object value = key == null ? null : key.GetValue(RunValueName);
                return value != null && Convert.ToString(value).IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static void WriteRunKey(bool startMinimized)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("시작 실행 레지스트리를 열 수 없습니다.");
                }

                key.SetValue(RunValueName, BuildStartupCommand(startMinimized), RegistryValueKind.String);
            }
        }

        private static void DeleteRunKey()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static string BuildStartupCommand(bool startMinimized)
        {
            return Quote(Application.ExecutablePath) + (startMinimized ? " --startup --minimized" : " --startup");
        }

        private static string GetShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
