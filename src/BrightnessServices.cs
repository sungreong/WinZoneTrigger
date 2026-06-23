using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WinZoneTrigger
{
    internal sealed class BrightnessApplyResult
    {
        public bool Succeeded { get; set; }
        public int BrightnessPercent { get; set; }
        public int MonitorCount { get; set; }
        public string Message { get; set; }
    }

    internal sealed class BrightnessScheduleTarget
    {
        public string Key { get; set; }
        public int BrightnessPercent { get; set; }
        public bool IsDefault { get; set; }
    }

    internal static class BrightnessSchedule
    {
        public static int GetTargetBrightnessPercent(AppConfig config, DateTime localTime)
        {
            return GetTarget(config, localTime).BrightnessPercent;
        }

        public static BrightnessScheduleTarget GetTarget(AppConfig config, DateTime localTime)
        {
            if (config == null)
            {
                return new BrightnessScheduleTarget
                {
                    Key = "default",
                    BrightnessPercent = 70,
                    IsDefault = true
                };
            }

            int defaultPercent = ClampBrightness(config.DefaultBrightnessPercent <= 0 ? 70 : config.DefaultBrightnessPercent);
            List<BrightnessPeriod> periods = (config.BrightnessPeriods ?? new List<BrightnessPeriod>())
                .Where(period => period != null && period.Enabled)
                .Select(period => period.Clone())
                .ToList();
            foreach (BrightnessPeriod period in periods)
            {
                period.Normalize();
            }

            if (periods.Count == 0)
            {
                return new BrightnessScheduleTarget
                {
                    Key = "default",
                    BrightnessPercent = defaultPercent,
                    IsDefault = true
                };
            }

            int currentMinute = (localTime.Hour * 60) + localTime.Minute;
            BrightnessPeriod target = periods
                .Where(period => period.StartMinuteOfDay <= currentMinute)
                .OrderBy(period => period.StartMinuteOfDay)
                .LastOrDefault();

            if (target == null)
            {
                target = periods
                    .OrderBy(period => period.StartMinuteOfDay)
                    .LastOrDefault();
            }

            if (target == null)
            {
                return new BrightnessScheduleTarget
                {
                    Key = "default",
                    BrightnessPercent = defaultPercent,
                    IsDefault = true
                };
            }

            return new BrightnessScheduleTarget
            {
                Key = "period:" + target.StartMinuteOfDay.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (target.Id ?? ""),
                BrightnessPercent = ClampBrightness(target.BrightnessPercent),
                IsDefault = false
            };
        }

        public static int ClampBrightness(int percent)
        {
            if (percent < 1)
            {
                return 1;
            }

            if (percent > 100)
            {
                return 100;
            }

            return percent;
        }

        public static string FormatStartTime(int startMinuteOfDay)
        {
            int minute = Math.Max(0, Math.Min((24 * 60) - 1, startMinuteOfDay));
            return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00");
        }

        public static bool TryParseStartTime(string text, out int startMinuteOfDay)
        {
            startMinuteOfDay = 0;
            DateTime parsed;
            if (!DateTime.TryParseExact(
                (text ?? "").Trim(),
                "HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out parsed))
            {
                return false;
            }

            startMinuteOfDay = (parsed.Hour * 60) + parsed.Minute;
            return true;
        }
    }

    internal static class BrightnessController
    {
        public static BrightnessApplyResult SetBrightness(int percent)
        {
            int value = BrightnessSchedule.ClampBrightness(percent);
            BrightnessApplyResult result = new BrightnessApplyResult
            {
                BrightnessPercent = value,
                Message = ""
            };

            try
            {
                int count = 0;
                using (ManagementClass methods = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null))
                {
                    foreach (ManagementObject instance in methods.GetInstances())
                    {
                        using (instance)
                        {
                            instance.InvokeMethod("WmiSetBrightness", new object[] { 1, value });
                            count++;
                        }
                    }
                }

                result.MonitorCount = count;
                result.Succeeded = count > 0;
                result.Message = count > 0
                    ? "화면 밝기 적용: " + value + "% (" + count + "개 화면)"
                    : "화면 밝기를 조정할 수 있는 내장 디스플레이를 찾지 못했습니다.";
                return result;
            }
            catch (Exception ex)
            {
                result.Succeeded = false;
                result.Message = "화면 밝기 적용 실패: " + ex.Message;
                DiagnosticsLog.Write("화면 밝기 적용 실패", ex);
                return result;
            }
        }
    }

    internal sealed class BrightnessScheduleRunner
    {
        private string _lastAppliedScheduleKey = "";
        private string _lastFailureMessage = "";

        public void Reset()
        {
            _lastAppliedScheduleKey = "";
            _lastFailureMessage = "";
        }

        public void Apply(AppConfig config, string reason)
        {
            if (config == null || !config.BrightnessScheduleEnabled)
            {
                _lastAppliedScheduleKey = "";
                _lastFailureMessage = "";
                return;
            }

            BrightnessScheduleTarget target = BrightnessSchedule.GetTarget(config, DateTime.Now);
            if (string.Equals(_lastAppliedScheduleKey, target.Key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            BrightnessApplyResult result = BrightnessController.SetBrightness(target.BrightnessPercent);
            if (result.Succeeded)
            {
                _lastAppliedScheduleKey = target.Key;
                _lastFailureMessage = "";
                DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + " 진입: " + result.Message);
                return;
            }

            if (!string.Equals(_lastFailureMessage, result.Message, StringComparison.OrdinalIgnoreCase))
            {
                _lastFailureMessage = result.Message;
                DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + ": " + result.Message);
            }

            _lastAppliedScheduleKey = target.Key;
        }
    }
}
