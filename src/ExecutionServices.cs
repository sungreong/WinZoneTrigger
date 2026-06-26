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
using System.Security;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace WinZoneTrigger
{
    internal static class ZoneExecutor
    {
        public static ZoneExecutionResult Execute(ZoneRule zone, Action<string> log)
        {
            if (log == null)
            {
                log = delegate { };
            }

            ZoneExecutionResult executionResult = new ZoneExecutionResult
            {
                ZoneId = zone == null ? "" : zone.Id,
                ZoneName = zone == null ? "" : zone.Name
            };

            log("동작 실행 시작: " + zone.Name);
            bool wifiConnectRequested = zone.ConnectWifiEnabled.GetValueOrDefault(false);
            bool wifiConnectSucceeded = !wifiConnectRequested;
            executionResult.WifiConnectionRequested = wifiConnectRequested;

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
                        WifiConnectionResult connectResult = WifiActions.Connect(zone.ConnectProfile, ssid);
                        wifiConnectSucceeded = connectResult.Succeeded;
                        executionResult.WifiConnectionVerified = connectResult.Verified;
                        executionResult.ConnectedSsid = connectResult.ConnectedSsid;
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
            return executionResult;
        }
    }

    internal sealed class ZoneExecutionResult
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public bool WifiConnectionRequested { get; set; }
        public bool WifiConnectionVerified { get; set; }
        public string ConnectedSsid { get; set; }
    }

    internal static class WifiActions
    {
        public static WifiConnectionResult Connect(string profileName, string ssid)
        {
            string arguments = "wlan connect name=" + Quote(profileName);
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                arguments += " ssid=" + Quote(ssid);
            }

            CommandResult request = CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "netsh.exe"), arguments, 15000);
            WifiConnectionResult result = new WifiConnectionResult
            {
                RequestResult = request,
                TargetSsid = ssid ?? ""
            };

            if (!request.Succeeded)
            {
                result.VerificationSummary = "연결 요청 실패";
                return result;
            }

            if (string.IsNullOrWhiteSpace(ssid))
            {
                result.Verified = true;
                result.VerificationSummary = "대상 SSID가 없어 연결 요청 성공만 확인했습니다.";
                return result;
            }

            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                string connectedSsid = GetConnectedSsid();
                if (!string.IsNullOrWhiteSpace(connectedSsid))
                {
                    result.ConnectedSsid = connectedSsid;
                    if (string.Equals(connectedSsid, ssid, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Verified = true;
                        result.VerificationSummary = "현재 연결 SSID 확인: " + connectedSsid;
                        return result;
                    }
                }

                Thread.Sleep(1000);
            }

            result.VerificationSummary = string.IsNullOrWhiteSpace(result.ConnectedSsid)
                ? "30초 안에 현재 연결 SSID를 확인하지 못했습니다."
                : "30초 뒤 현재 연결 SSID가 다릅니다: " + result.ConnectedSsid;
            return result;
        }

        private static string GetConnectedSsid()
        {
            CommandResult show = CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "netsh.exe"), "wlan show interfaces", 10000);
            if (!show.Succeeded)
            {
                return "";
            }

            string text = (show.Output ?? "") + Environment.NewLine + (show.Error ?? "");
            string[] lines = text.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Match match = Regex.Match(trimmed, @"^SSID\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string value = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return "";
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class WifiConnectionResult
    {
        public CommandResult RequestResult { get; set; }
        public string TargetSsid { get; set; }
        public bool Verified { get; set; }
        public string ConnectedSsid { get; set; }
        public string VerificationSummary { get; set; }

        public bool Succeeded
        {
            get { return RequestResult != null && RequestResult.Succeeded && Verified; }
        }

        public string Summary
        {
            get
            {
                string requestSummary = RequestResult == null ? "연결 요청 결과 없음" : RequestResult.Summary;
                string verifySummary = string.IsNullOrWhiteSpace(VerificationSummary) ? "연결 확인 정보 없음" : VerificationSummary;
                return requestSummary + " / " + verifySummary;
            }
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

        public static void EnsurePreferredRegistration(bool startMinimized)
        {
            if (!IsRunKeyEnabled() || IsScheduledTaskEnabled())
            {
                return;
            }

            try
            {
                CreateScheduledTask(startMinimized);
                DeleteRunKey();
                DeleteStartupShortcut();
                DiagnosticsLog.WriteEvent("자동 시작 자가 복구: Run 레지스트리를 작업 스케줄러로 이전했습니다.");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.WriteEvent("자동 시작 자가 복구 실패: " + ex.Message);
            }
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
            string xmlPath = Path.Combine(Path.GetTempPath(), "WinZoneTrigger.task." + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(xmlPath, BuildScheduledTaskXml(startMinimized), Encoding.Unicode);
                CommandResult result = RunSchtasks("/Create /F /TN " + Quote(TaskName) + " /XML " + Quote(xmlPath));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("작업 스케줄러 등록 실패: " + result.Summary);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(xmlPath))
                    {
                        File.Delete(xmlPath);
                    }
                }
                catch
                {
                }
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

        private static string BuildScheduledTaskXml(bool startMinimized)
        {
            string exePath = Application.ExecutablePath;
            string arguments = startMinimized ? "--startup --minimized" : "--startup";
            string workingDirectory = Path.GetDirectoryName(exePath) ?? "";
            string userId = WindowsIdentity.GetCurrent() == null ? "" : WindowsIdentity.GetCurrent().Name;

            return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n"
                + "<Task version=\"1.4\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n"
                + "  <RegistrationInfo><Author>" + XmlEscape(userId) + "</Author></RegistrationInfo>\r\n"
                + "  <Triggers><LogonTrigger><Enabled>true</Enabled><Delay>PT30S</Delay></LogonTrigger></Triggers>\r\n"
                + "  <Principals><Principal id=\"Author\"><UserId>" + XmlEscape(userId) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>LeastPrivilege</RunLevel></Principal></Principals>\r\n"
                + "  <Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries><AllowHardTerminate>true</AllowHardTerminate><StartWhenAvailable>true</StartWhenAvailable><RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable><IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings><AllowStartOnDemand>true</AllowStartOnDemand><Enabled>true</Enabled><Hidden>false</Hidden><RunOnlyIfIdle>false</RunOnlyIfIdle><WakeToRun>false</WakeToRun><ExecutionTimeLimit>PT0S</ExecutionTimeLimit><Priority>7</Priority><RestartOnFailure><Interval>PT1M</Interval><Count>3</Count></RestartOnFailure></Settings>\r\n"
                + "  <Actions Context=\"Author\"><Exec><Command>" + XmlEscape(exePath) + "</Command><Arguments>" + XmlEscape(arguments) + "</Arguments><WorkingDirectory>" + XmlEscape(workingDirectory) + "</WorkingDirectory></Exec></Actions>\r\n"
                + "</Task>\r\n";
        }

        private static string XmlEscape(string value)
        {
            return SecurityElement.Escape(value ?? "") ?? "";
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
