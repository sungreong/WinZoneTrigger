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
    public sealed class AppConfig
    {
        public bool MonitoringEnabled { get; set; }
        public bool? RunOnceAtStartup { get; set; }
        public int ScanIntervalSeconds { get; set; }
        public bool StartMinimized { get; set; }
        public bool PreventSleepWhileAutomationActive { get; set; }
        public bool AppWatchEnabled { get; set; }
        public bool? AppWatchRequireWindow { get; set; }
        public string AppWatchProcessName { get; set; }
        public string AppWatchLaunchTarget { get; set; }
        public int AppWatchIntervalValue { get; set; }
        public string AppWatchIntervalUnit { get; set; }
        public List<ZoneRule> Zones { get; set; }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                MonitoringEnabled = false,
                RunOnceAtStartup = true,
                ScanIntervalSeconds = 30,
                StartMinimized = true,
                PreventSleepWhileAutomationActive = false,
                AppWatchEnabled = false,
                AppWatchRequireWindow = false,
                AppWatchProcessName = "",
                AppWatchLaunchTarget = "",
                AppWatchIntervalValue = 5,
                AppWatchIntervalUnit = "Minutes",
                Zones = new List<ZoneRule>
                {
                    ZoneRule.CreateDefault("내 위치")
                }
            };
        }

        public void Normalize()
        {
            if (!RunOnceAtStartup.HasValue)
            {
                RunOnceAtStartup = true;
                MonitoringEnabled = false;
            }

            if (ScanIntervalSeconds < 5)
            {
                ScanIntervalSeconds = 30;
            }

            if (AppWatchProcessName == null)
            {
                AppWatchProcessName = "";
            }

            if (AppWatchLaunchTarget == null)
            {
                AppWatchLaunchTarget = "";
            }

            if (!string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                AppWatchIntervalUnit = "Minutes";
            }

            int maxAppWatchInterval = string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            if (AppWatchIntervalValue <= 0)
            {
                AppWatchIntervalValue = 5;
            }
            else if (AppWatchIntervalValue > maxAppWatchInterval)
            {
                AppWatchIntervalValue = maxAppWatchInterval;
            }

            if (Zones == null)
            {
                Zones = new List<ZoneRule>();
            }

            foreach (ZoneRule zone in Zones)
            {
                if (!zone.RunOnceAtStartup.HasValue)
                {
                    zone.RunOnceAtStartup = RunOnceAtStartup.GetValueOrDefault(true);
                }

                if (!zone.MonitoringEnabled.HasValue)
                {
                    zone.MonitoringEnabled = MonitoringEnabled;
                }

                if (zone.ScanIntervalSeconds < 5)
                {
                    zone.ScanIntervalSeconds = ScanIntervalSeconds;
                }

                zone.Normalize();
            }

            if (AppWatchEnabled
                && Zones.Count > 0
                && !string.IsNullOrWhiteSpace(AppWatchLaunchTarget)
                && !Zones.Any(z => z.HasAppWatchItems()))
            {
                ZoneRule firstZone = Zones.FirstOrDefault(z => z.Enabled) ?? Zones[0];
                if (firstZone.AppWatchItems == null)
                {
                    firstZone.AppWatchItems = new List<AppWatchItem>();
                }

                firstZone.AppWatchItems.Add(new AppWatchItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Enabled = true,
                    RequireWindow = AppWatchRequireWindow,
                    LaunchTarget = AppWatchLaunchTarget,
                    ProcessName = AppWatchProcessName,
                    IntervalValue = AppWatchIntervalValue,
                    IntervalUnit = AppWatchIntervalUnit
                });
                firstZone.Normalize();
            }
        }
    }

    public sealed class AppWatchItem
    {
        public string Id { get; set; }
        public bool Enabled { get; set; }
        public bool? RequireWindow { get; set; }
        public string ProcessName { get; set; }
        public string LaunchTarget { get; set; }
        public int IntervalValue { get; set; }
        public string IntervalUnit { get; set; }

        public static AppWatchItem CreateDefault()
        {
            return new AppWatchItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Enabled = true,
                RequireWindow = false,
                ProcessName = "",
                LaunchTarget = "",
                IntervalValue = 5,
                IntervalUnit = "Minutes"
            };
        }

        public AppWatchItem Clone()
        {
            return new AppWatchItem
            {
                Id = Id,
                Enabled = Enabled,
                RequireWindow = RequireWindow,
                ProcessName = ProcessName,
                LaunchTarget = LaunchTarget,
                IntervalValue = IntervalValue,
                IntervalUnit = IntervalUnit
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }

            if (ProcessName == null)
            {
                ProcessName = "";
            }

            if (LaunchTarget == null)
            {
                LaunchTarget = "";
            }

            if (!RequireWindow.HasValue)
            {
                RequireWindow = ZoneRule.ShouldRequireWindowByDefault(ProcessName, LaunchTarget);
            }

            if (!string.Equals(IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                IntervalUnit = "Minutes";
            }

            int maxInterval = string.Equals(IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            if (IntervalValue <= 0)
            {
                IntervalValue = 5;
            }
            else if (IntervalValue > maxInterval)
            {
                IntervalValue = maxInterval;
            }
        }
    }

    public sealed class ZoneRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public bool? RunOnceAtStartup { get; set; }
        public bool? MonitoringEnabled { get; set; }
        public int ScanIntervalSeconds { get; set; }
        public bool? AppWatchEnabled { get; set; }
        public bool? AppWatchRequireWindow { get; set; }
        public string AppWatchProcessName { get; set; }
        public string AppWatchLaunchTarget { get; set; }
        public int AppWatchIntervalValue { get; set; }
        public string AppWatchIntervalUnit { get; set; }
        public List<AppWatchItem> AppWatchItems { get; set; }
        public bool UseCoordinates { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public bool RequireAllSsids { get; set; }
        public List<string> NearbySsids { get; set; }
        public bool? ConnectWifiEnabled { get; set; }
        public string ConnectProfile { get; set; }
        public string ConnectSsid { get; set; }
        public string AudioAction { get; set; }
        public List<string> ChromeUrls { get; set; }
        public List<string> AppLaunches { get; set; }
        public List<string> Commands { get; set; }

        public static ZoneRule CreateDefault(string name)
        {
            return new ZoneRule
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Enabled = true,
                RunOnceAtStartup = true,
                MonitoringEnabled = false,
                ScanIntervalSeconds = 30,
                AppWatchEnabled = false,
                AppWatchRequireWindow = false,
                AppWatchProcessName = "",
                AppWatchLaunchTarget = "",
                AppWatchIntervalValue = 5,
                AppWatchIntervalUnit = "Minutes",
                AppWatchItems = new List<AppWatchItem>(),
                UseCoordinates = false,
                Latitude = 0,
                Longitude = 0,
                RadiusMeters = 200,
                RequireAllSsids = false,
                NearbySsids = new List<string>(),
                ConnectWifiEnabled = false,
                ConnectProfile = "",
                ConnectSsid = "",
                AudioAction = "None",
                ChromeUrls = new List<string>(),
                AppLaunches = new List<string>(),
                Commands = new List<string>()
            };
        }

        public ZoneRule Clone()
        {
            return new ZoneRule
            {
                Id = Id,
                Name = Name,
                Enabled = Enabled,
                RunOnceAtStartup = RunOnceAtStartup,
                MonitoringEnabled = MonitoringEnabled,
                ScanIntervalSeconds = ScanIntervalSeconds,
                AppWatchEnabled = AppWatchEnabled,
                AppWatchRequireWindow = AppWatchRequireWindow,
                AppWatchProcessName = AppWatchProcessName,
                AppWatchLaunchTarget = AppWatchLaunchTarget,
                AppWatchIntervalValue = AppWatchIntervalValue,
                AppWatchIntervalUnit = AppWatchIntervalUnit,
                AppWatchItems = AppWatchItems == null ? new List<AppWatchItem>() : AppWatchItems.Select(item => item == null ? null : item.Clone()).Where(item => item != null).ToList(),
                UseCoordinates = UseCoordinates,
                Latitude = Latitude,
                Longitude = Longitude,
                RadiusMeters = RadiusMeters,
                RequireAllSsids = RequireAllSsids,
                NearbySsids = NearbySsids == null ? new List<string>() : new List<string>(NearbySsids),
                ConnectWifiEnabled = ConnectWifiEnabled,
                ConnectProfile = ConnectProfile,
                ConnectSsid = ConnectSsid,
                AudioAction = AudioAction,
                ChromeUrls = ChromeUrls == null ? new List<string>() : new List<string>(ChromeUrls),
                AppLaunches = AppLaunches == null ? new List<string>() : new List<string>(AppLaunches),
                Commands = Commands == null ? new List<string>() : new List<string>(Commands)
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = "이름 없는 위치";
            }
            else if (string.Equals(Name, "My first zone", StringComparison.OrdinalIgnoreCase))
            {
                Name = "내 위치";
            }
            else if (string.Equals(Name, "New zone", StringComparison.OrdinalIgnoreCase))
            {
                Name = "새 위치";
            }

            if (RadiusMeters <= 0)
            {
                RadiusMeters = 200;
            }

            if (!RunOnceAtStartup.HasValue)
            {
                RunOnceAtStartup = true;
            }

            if (!MonitoringEnabled.HasValue)
            {
                MonitoringEnabled = false;
            }

            if (ScanIntervalSeconds < 5)
            {
                ScanIntervalSeconds = 30;
            }

            if (!AppWatchEnabled.HasValue)
            {
                AppWatchEnabled = false;
            }

            if (!AppWatchRequireWindow.HasValue)
            {
                AppWatchRequireWindow = ShouldRequireWindowByDefault(AppWatchProcessName, AppWatchLaunchTarget);
            }

            if (AppWatchProcessName == null)
            {
                AppWatchProcessName = "";
            }

            if (AppWatchLaunchTarget == null)
            {
                AppWatchLaunchTarget = "";
            }

            if (!string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                AppWatchIntervalUnit = "Minutes";
            }

            int maxAppWatchInterval = string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            if (AppWatchIntervalValue <= 0)
            {
                AppWatchIntervalValue = 5;
            }
            else if (AppWatchIntervalValue > maxAppWatchInterval)
            {
                AppWatchIntervalValue = maxAppWatchInterval;
            }

            bool hasLegacyAppWatch = AppWatchEnabled.GetValueOrDefault(false)
                || !string.IsNullOrWhiteSpace(AppWatchLaunchTarget)
                || !string.IsNullOrWhiteSpace(AppWatchProcessName);

            if (AppWatchItems == null)
            {
                AppWatchItems = new List<AppWatchItem>();
            }

            if (AppWatchItems.Count == 0 && hasLegacyAppWatch)
            {
                AppWatchItems.Add(new AppWatchItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Enabled = AppWatchEnabled.GetValueOrDefault(false),
                    RequireWindow = AppWatchRequireWindow,
                    ProcessName = AppWatchProcessName,
                    LaunchTarget = AppWatchLaunchTarget,
                    IntervalValue = AppWatchIntervalValue,
                    IntervalUnit = AppWatchIntervalUnit
                });
            }

            AppWatchItems = AppWatchItems
                .Where(item => item != null)
                .ToList();

            foreach (AppWatchItem item in AppWatchItems)
            {
                item.Normalize();
            }

            SyncLegacyAppWatchFields();

            if (NearbySsids == null)
            {
                NearbySsids = new List<string>();
            }
            else if (NearbySsids.Count == 1 && string.Equals(NearbySsids[0], "ExampleWifiName", StringComparison.OrdinalIgnoreCase))
            {
                NearbySsids.Clear();
            }

            if (Commands == null)
            {
                Commands = new List<string>();
            }

            if (ChromeUrls == null)
            {
                ChromeUrls = new List<string>();
            }
            else
            {
                ChromeUrls = ChromeUrls
                    .Where(url => !ActionValueCleaner.IsAudioStatusValue(url))
                    .ToList();
            }

            if (AppLaunches == null)
            {
                AppLaunches = new List<string>();
            }
            else
            {
                AppLaunches = AppLaunches
                    .Where(app => !ActionValueCleaner.IsAudioStatusValue(app))
                    .ToList();
            }

            if (ConnectProfile == null)
            {
                ConnectProfile = "";
            }

            if (ConnectSsid == null)
            {
                ConnectSsid = "";
            }

            if (!ConnectWifiEnabled.HasValue)
            {
                ConnectWifiEnabled = !string.IsNullOrWhiteSpace(ConnectProfile);
            }

            if (string.IsNullOrWhiteSpace(AudioAction))
            {
                AudioAction = "None";
            }
        }

        public bool HasAppWatchItems()
        {
            return AppWatchItems != null && AppWatchItems.Any(item => item != null);
        }

        public IEnumerable<AppWatchItem> GetEnabledAppWatchItems()
        {
            return (AppWatchItems ?? new List<AppWatchItem>())
                .Where(item => item != null && item.Enabled);
        }

        public void SyncLegacyAppWatchFields()
        {
            if (AppWatchItems == null)
            {
                AppWatchItems = new List<AppWatchItem>();
            }

            AppWatchItem first = AppWatchItems.FirstOrDefault(item => item != null && item.Enabled)
                ?? AppWatchItems.FirstOrDefault(item => item != null);

            AppWatchEnabled = AppWatchItems.Any(item => item != null && item.Enabled);
            if (first == null)
            {
                AppWatchRequireWindow = false;
                AppWatchProcessName = "";
                AppWatchLaunchTarget = "";
                AppWatchIntervalValue = 5;
                AppWatchIntervalUnit = "Minutes";
                return;
            }

            AppWatchRequireWindow = first.RequireWindow.GetValueOrDefault(false);
            AppWatchProcessName = first.ProcessName ?? "";
            AppWatchLaunchTarget = first.LaunchTarget ?? "";
            AppWatchIntervalValue = first.IntervalValue <= 0 ? 5 : first.IntervalValue;
            AppWatchIntervalUnit = string.Equals(first.IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? "Hours" : "Minutes";
        }

        public static bool ShouldRequireWindowByDefault(string processName, string launchTarget)
        {
            return false;
        }
    }

    internal static class ActionValueCleaner
    {
        public static bool IsAudioStatusValue(string value)
        {
            string token = ExtractToken(value);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string compact = token.Replace(" ", "").Replace("-", "").Replace("_", "");
            return string.Equals(compact, "mute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "muted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "unmute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "unmuted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "음소거", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "음소거해제", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "안함", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractToken(string value)
        {
            string text = (value ?? "").Trim().Trim('"', '\'');
            if (text.Length == 0)
            {
                return "";
            }

            try
            {
                Uri uri;
                if (Uri.TryCreate(text, UriKind.Absolute, out uri)
                    && (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                    && string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/'))
                    && string.IsNullOrWhiteSpace(uri.Query)
                    && string.IsNullOrWhiteSpace(uri.Fragment))
                {
                    text = uri.Host;
                }
            }
            catch
            {
            }

            return text.Trim().TrimEnd('.').ToLowerInvariant();
        }
    }
}
