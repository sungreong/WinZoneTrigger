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
    internal static class AppLauncher
    {
        private static readonly object AppIndexLock = new object();
        private static List<AppSearchCandidate> _installedAppIndex;

        public static void OpenChromeUrl(string url, Action<string> log)
        {
            OpenChromeUrls(new List<string> { url }, log);
        }

        public static void OpenChromeUrls(IEnumerable<string> urls, Action<string> log)
        {
            List<string> normalizedUrls = (urls ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(NormalizeUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedUrls.Count == 0)
            {
                return;
            }

            string chromePath = FindChromePath();

            if (!string.IsNullOrWhiteSpace(chromePath) && File.Exists(chromePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = chromePath;
                startInfo.Arguments = string.Join(" ", normalizedUrls.Select(QuoteArgument).ToArray());
                startInfo.UseShellExecute = false;
                Process.Start(startInfo);
                log("Chrome 탭 실행 성공: " + normalizedUrls.Count + "개");
                foreach (string normalizedUrl in normalizedUrls)
                {
                    log("Chrome 탭: " + normalizedUrl);
                }
                return;
            }

            foreach (string normalizedUrl in normalizedUrls)
            {
                StartShellTarget(normalizedUrl);
                log("브라우저 탭 실행 성공: " + normalizedUrl);
            }
        }

        public static void LaunchApp(string target, Action<string> log)
        {
            LaunchApp(target, log, false);
        }

        public static void LaunchAppIfNotRunning(string target, Action<string> log)
        {
            LaunchApp(target, log, true);
        }

        private static void LaunchApp(string target, Action<string> log, bool skipIfRunning)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return;
            }
            if (log == null)
            {
                log = delegate { };
            }

            if (skipIfRunning && IsAppAlreadyRunning(value, log))
            {
                return;
            }

            if (IsNotepadTarget(value))
            {
                if (TryLaunchNotepad())
                {
                    log("앱 실행 성공: 메모장");
                }
                else
                {
                    log("앱 실행 실패: 메모장을 찾지 못했습니다.");
                }
                return;
            }

            if (string.Equals(value, "ChatGPT", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("ChatGPT") || TryLaunchCommonPath(GetLocalPath(@"Programs\ChatGPT\ChatGPT.exe")) || TryStartShellTarget("chatgpt://"))
                {
                    log("앱 실행 성공: ChatGPT");
                }
                else
                {
                    log("앱 실행 실패: ChatGPT 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Obsidian", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("Obsidian")
                    || TryLaunchCommonPath(GetLocalPath(@"Programs\Obsidian\Obsidian.exe"))
                    || TryLaunchCommonPath(GetLocalPath(@"Obsidian\Obsidian.exe"))
                    || TryStartShellTarget("obsidian://"))
                {
                    log("앱 실행 성공: Obsidian");
                }
                else
                {
                    log("앱 실행 실패: Obsidian 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Teams", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Microsoft Teams", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchMicrosoftTeams())
                {
                    log("앱 실행 성공: Microsoft Teams");
                }
                else
                {
                    log("앱 실행 실패: Microsoft Teams 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Docker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Docker Desktop", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("Docker Desktop")
                    || TryLaunchCommonPath(GetProgramFilesPath(@"Docker\Docker\Docker Desktop.exe"))
                    || TryLaunchCommonPath(GetProgramFilesX86Path(@"Docker\Docker\Docker Desktop.exe")))
                {
                    log("앱 실행 성공: Docker Desktop");
                }
                else
                {
                    log("앱 실행 실패: Docker Desktop 앱을 찾지 못했습니다. Docker Desktop이 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (File.Exists(value) || Directory.Exists(value) || LooksLikeShellTarget(value))
            {
                if (TryLaunchTargetWithFallback(value))
                {
                    log("앱 실행 성공: " + value);
                }
                else
                {
                    log("앱 실행 실패: 앱을 찾았지만 실행하지 못했습니다: " + value);
                }
                return;
            }

            if (LooksLikeExecutableCommand(value) && TryStartShellTarget(value))
            {
                log("앱 실행 성공: " + value);
                return;
            }

            if (TryLaunchStartMenuApp(value))
            {
                log("앱 실행 성공: " + value);
                return;
            }

            if (TryStartShellTarget(value))
            {
                log("앱 실행 성공: " + value);
                return;
            }

            log("앱 실행 실패: 앱을 찾지 못했습니다: " + value);
        }

        private static bool IsAppAlreadyRunning(string target, Action<string> log)
        {
            List<string> processNames = BuildProcessNameCandidates(target);
            if (processNames.Count == 0)
            {
                log("앱 실행 확인: 프로세스 이름을 추론하지 못해 실행을 시도합니다: " + target);
                return false;
            }

            log("앱 실행 확인: " + target + " -> " + string.Join(", ", processNames.ToArray()));
            foreach (string processName in processNames)
            {
                try
                {
                    AppWatchCheckResult result = AppWatchdog.Check(processName, false);
                    if (result != null && result.IsRunning)
                    {
                        log("앱 실행 건너뜀: 이미 실행 중 - " + target + " / " + result.ProcessName + " (" + result.ProcessCount + "개)");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    log("앱 실행 확인 실패: " + target + " / " + processName + " -> " + ex.Message);
                    DiagnosticsLog.Write("앱 실행 사전 확인 실패: " + target + " / " + processName, ex);
                }
            }

            log("앱 실행 확인: 실행 중인 프로세스 없음 - " + target);
            return false;
        }

        private static List<string> BuildProcessNameCandidates(string target)
        {
            List<string> candidates = new List<string>();
            string value = (target ?? "").Trim().Trim('"');

            AddProcessNameCandidate(candidates, AppWatchdog.NormalizeProcessName(value));

            AppSearchCandidate installedApp = FindInstalledAppByTarget(value);
            if (installedApp != null)
            {
                AddProcessNameCandidate(candidates, installedApp.Name);
            }

            if (IsShortcutPath(value))
            {
                ShortcutInfo shortcut = ReadShortcut(value);
                if (shortcut != null)
                {
                    AddProcessNameCandidate(candidates, AppWatchdog.NormalizeProcessName(shortcut.TargetPath));
                }
            }

            return candidates;
        }

        private static AppSearchCandidate FindInstalledAppByTarget(string target)
        {
            try
            {
                string value = (target ?? "").Trim();
                if (!value.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return GetInstalledAppIndex(false)
                    .FirstOrDefault(candidate => candidate != null
                        && string.Equals(candidate.Target, value, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        private static void AddProcessNameCandidate(List<string> candidates, string processName)
        {
            string value = (processName ?? "").Trim();
            if (value.Length == 0)
            {
                return;
            }
            if (value.IndexOf(':') >= 0 || value.IndexOf('/') >= 0 || value.IndexOf('\\') >= 0)
            {
                return;
            }
            if (!candidates.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)))
            {
                candidates.Add(value);
            }
        }

        public static List<AppSearchCandidate> FindInstalledApps(string query, int limit)
        {
            return FindInstalledApps(query, limit, false);
        }

        public static List<AppSearchCandidate> FindInstalledApps(string query, int limit, bool includeAllWhenEmpty)
        {
            string needle = (query ?? "").Trim();

            return GetInstalledAppIndex(false)
                .Where(c => MatchesAppCandidate(c, needle, includeAllWhenEmpty))
                .OrderBy(c => RankAppCandidate(c, needle))
                .ThenBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, limit))
                .ToList();
        }

        public static void RefreshInstalledAppIndex()
        {
            lock (AppIndexLock)
            {
                _installedAppIndex = BuildInstalledAppIndex();
            }
        }

        private static List<AppSearchCandidate> GetInstalledAppIndex(bool forceRefresh)
        {
            lock (AppIndexLock)
            {
                if (forceRefresh || _installedAppIndex == null)
                {
                    _installedAppIndex = BuildInstalledAppIndex();
                }

                return _installedAppIndex.ToList();
            }
        }

        private static List<AppSearchCandidate> BuildInstalledAppIndex()
        {
            List<AppSearchCandidate> candidates = new List<AppSearchCandidate>();
            AddStartApps(candidates);
            AddStartMenuShortcuts(candidates);

            return candidates
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Target))
                .GroupBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddStartApps(List<AppSearchCandidate> candidates)
        {
            try
            {
                string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
Get-StartApps | ForEach-Object {
    $name = [string]$_.Name
    $appId = [string]$_.AppID
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        [Console]::WriteLine(($name.Replace('|', ' ') + '|' + $appId.Replace('|', ' ')))
    }
}
";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                CommandResult result = CommandRunner.Run(
                    powershellPath,
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    8000);

                if (result.TimedOut || result.ExitCode != 0)
                {
                    return;
                }

                foreach (string line in (result.Output ?? "").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.TrimStart().StartsWith("#<", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] parts = line.Split(new[] { '|' }, 2);
                    string name = parts.Length > 0 ? parts[0].Trim() : "";
                    string appId = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string target = string.IsNullOrWhiteSpace(appId) ? name : @"shell:AppsFolder\" + appId;
                        candidates.Add(new AppSearchCandidate
                        {
                            Name = name,
                            Target = target,
                            AppId = appId,
                            Source = "시작 앱",
                            SearchText = name + " " + appId
                        });
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddStartMenuShortcuts(List<AppSearchCandidate> candidates)
        {
            string[] roots = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };

            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root))
                    {
                        continue;
                    }

                    foreach (string shortcut in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(shortcut);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            candidates.Add(new AppSearchCandidate
                            {
                                Name = name,
                                Target = shortcut,
                                AppId = "",
                                Source = "바로가기",
                                SearchText = name + " " + shortcut
                            });
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static bool MatchesAppCandidate(AppSearchCandidate candidate, string query, bool includeAllWhenEmpty)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Name))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return includeAllWhenEmpty || IsCommonSuggestion(candidate.Name);
            }

            return candidate.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrWhiteSpace(candidate.Target) && candidate.Target.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(candidate.AppId) && candidate.AppId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(candidate.SearchText) && candidate.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsCommonSuggestion(string name)
        {
            string[] preferred = { "ChatGPT", "Claude", "Cursor", "Obsidian", "Docker", "Chrome", "Notepad", "PowerShell", "Visual Studio Code" };
            return preferred.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int RankAppCandidate(AppSearchCandidate candidate, string query)
        {
            string name = candidate == null ? "" : candidate.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return IsCommonSuggestion(name) ? 0 : 10;
            }

            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.AppId) && candidate.AppId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            return 3;
        }

        private static string NormalizeUrl(string value)
        {
            string url = (value ?? "").Trim();
            if (url.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                url = "https://" + url;
            }
            return url;
        }

        private static string FindChromePath()
        {
            string[] candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            return candidates.FirstOrDefault(File.Exists) ?? "";
        }

        private static bool TryLaunchStartMenuApp(string appName)
        {
            try
            {
                string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$name = __APP_NAME__
$app = Get-StartApps |
    Where-Object { $_.Name -eq $name -or $_.Name -like ('*' + $name + '*') } |
    Select-Object -First 1
if ($null -eq $app) { exit 3 }
Start-Process explorer.exe -ArgumentList ('shell:AppsFolder\' + $app.AppID)
";
                script = script.Replace("__APP_NAME__", PowerShellStringLiteral(appName));
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                CommandResult result = CommandRunner.Run(
                    powershellPath,
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    12000);
                return !result.TimedOut && result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLaunchMicrosoftTeams()
        {
            return TryLaunchStartMenuApp("Microsoft Teams")
                || TryLaunchStartMenuApp("Teams")
                || TryStartAppsFolderTarget(@"shell:AppsFolder\MSTeams_8wekyb3d8bbwe!MSTeams")
                || TryStartShellTarget(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Microsoft Teams.lnk"))
                || TryStartShellTarget(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Microsoft Teams.lnk"));
        }

        private static bool TryLaunchNotepad()
        {
            return TryStartAppsFolderTarget(@"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")
                || TryLaunchStartMenuApp("메모장")
                || TryLaunchStartMenuApp("Notepad")
                || TryStartShellTarget(Path.Combine(Environment.SystemDirectory, "notepad.exe"));
        }

        private static bool IsNotepadTarget(string target)
        {
            string value = (target ?? "").Trim().Trim('"');
            if (value.Length == 0)
            {
                return false;
            }

            string name = value;
            try
            {
                string fileName = Path.GetFileName(value);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    name = fileName;
                }
            }
            catch
            {
            }

            string withoutExtension = name;
            try
            {
                string extension = Path.GetExtension(name);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    withoutExtension = Path.GetFileNameWithoutExtension(name);
                }
            }
            catch
            {
            }

            return string.Equals(name, "notepad.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(withoutExtension, "notepad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(withoutExtension, "메모장", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryLaunchCommonPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return TryLaunchTargetWithFallback(path);
        }

        private static string GetLocalPath(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relativePath);
        }

        private static string GetProgramFilesPath(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), relativePath);
        }

        private static string GetProgramFilesX86Path(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), relativePath);
        }

        private static bool TryStartShellTarget(string target)
        {
            try
            {
                StartShellTarget(target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLaunchTargetWithFallback(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return false;
            }

            if (value.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            {
                return TryStartAppsFolderTarget(value) || TryStartShellTarget(value);
            }

            if (TryStartShellTarget(value))
            {
                return true;
            }

            if (IsShortcutPath(value))
            {
                string shortcutName = "";
                try
                {
                    shortcutName = Path.GetFileNameWithoutExtension(value);
                }
                catch
                {
                }

                ShortcutInfo shortcut = ReadShortcut(value);
                if (IsTeamsShortcut(shortcutName, shortcut))
                {
                    return TryLaunchMicrosoftTeams();
                }

                if (!string.IsNullOrWhiteSpace(shortcutName) && TryLaunchStartMenuApp(shortcutName))
                {
                    return true;
                }

                if (shortcut != null && !string.IsNullOrWhiteSpace(shortcut.TargetPath) && File.Exists(shortcut.TargetPath))
                {
                    return TryStartShellTarget(shortcut.TargetPath);
                }
            }

            return false;
        }

        private static bool TryStartAppsFolderTarget(string target)
        {
            return TryStartProcess(Path.Combine(Environment.SystemDirectory, "explorer.exe"), target);
        }

        private static bool TryStartProcess(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = fileName;
                startInfo.Arguments = QuoteArgument(arguments);
                startInfo.UseShellExecute = false;
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StartShellTarget(string target)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = target;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }

        private static bool LooksLikeShellTarget(string target)
        {
            return target.IndexOf("://", StringComparison.Ordinal) > 0
                || target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("ms-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeExecutableCommand(string target)
        {
            string value = (target ?? "").Trim().Trim('"');
            if (value.Length == 0)
            {
                return false;
            }

            if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                return false;
            }

            string extension = "";
            try
            {
                extension = Path.GetExtension(value);
            }
            catch
            {
            }

            return string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShortcutPath(string target)
        {
            try
            {
                return string.Equals(Path.GetExtension(target), ".lnk", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTeamsShortcut(string shortcutName, ShortcutInfo shortcut)
        {
            string name = shortcutName ?? "";
            string target = shortcut == null ? "" : shortcut.TargetPath ?? "";
            return name.IndexOf("Teams", StringComparison.OrdinalIgnoreCase) >= 0
                || target.IndexOf("MSTeams", StringComparison.OrdinalIgnoreCase) >= 0
                || target.IndexOf("ms-teams", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ShortcutInfo ReadShortcut(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return null;
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                if (shortcut == null)
                {
                    return null;
                }

                Type shortcutType = shortcut.GetType();
                return new ShortcutInfo
                {
                    TargetPath = Convert.ToString(shortcutType.InvokeMember(
                        "TargetPath",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        shortcut,
                        null)),
                    Arguments = Convert.ToString(shortcutType.InvokeMember(
                        "Arguments",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        shortcut,
                        null))
                };
            }
            catch
            {
                return null;
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

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string PowerShellStringLiteral(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private sealed class ShortcutInfo
        {
            public string TargetPath { get; set; }
            public string Arguments { get; set; }
        }
    }
}
