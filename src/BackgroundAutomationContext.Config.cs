using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinZoneTrigger
{
    internal sealed partial class BackgroundAutomationContext
    {
        private int GetShortestConditionScanIntervalSeconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled && (z.MonitoringEnabled.GetValueOrDefault(false) || z.RestoreAudioOnExit || z.ScheduleEnabled || z.GetEnabledAppWatchItems().Any()))
                .Select(z => z.ScanIntervalSeconds < 5 ? 30 : z.ScanIntervalSeconds)
                .ToList();
            return intervals.Count == 0 ? 30 : intervals.Min();
        }

        private int GetShortestAppWatchIntervalMilliseconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled)
                .SelectMany(z => z.GetEnabledAppWatchItems())
                .Select(item => AppWatchTiming.GetGuardIntervalMilliseconds(item.IntervalValue, item.IntervalUnit))
                .ToList();
            int shortest = intervals.Count == 0 ? AppWatchTiming.DefaultGuardIntervalMilliseconds : intervals.Min();
            return Math.Min(shortest, AppWatchTiming.GuardPollIntervalMilliseconds);
        }

        private static string QuoteCommandArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private bool ReloadConfigIfChanged()
        {
            try
            {
                DateTime lastWriteUtc = File.Exists(ConfigStore.ConfigPath)
                    ? File.GetLastWriteTimeUtc(ConfigStore.ConfigPath)
                    : DateTime.MinValue;
                if (lastWriteUtc <= _configLastWriteUtc)
                {
                    return false;
                }

                LoadConfigFromDisk("변경 감지", true);
                ApplyBrightnessSchedule("설정 변경 화면 밝기 일정");
                ResetTimers();
                return true;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("백그라운드 설정 변경 확인 실패", ex);
                return false;
            }
        }

        private void LoadConfigFromDisk(string reason, bool resetState)
        {
            _config = ConfigStore.Load();
            _config.Normalize();
            _configLastWriteUtc = File.Exists(ConfigStore.ConfigPath)
                ? File.GetLastWriteTimeUtc(ConfigStore.ConfigPath)
                : DateTime.UtcNow;

            if (resetState)
            {
                // Keep occupancy across unrelated edits so saving never replays entry actions.
                foreach (string id in _insideZones.Keys.Where(id => !_config.Zones.Any(z => z.Id == id && z.Enabled)).ToList())
                    _insideZones.Remove(id);
            }

            DiagnosticsLog.WriteEvent("백그라운드 설정 로드: " + reason + " / zones=" + _config.Zones.Count);
        }

    }
}
