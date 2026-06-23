using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Microsoft.Win32;

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
        public int EffectiveStartMinuteOfDay { get; set; }
        public string NightLightAction { get; set; }
        public bool IsDefault { get; set; }
    }

    internal sealed class NightLightStatus
    {
        public bool IsAvailable { get; set; }
        public bool? IsEnabled { get; set; }
        public string Message { get; set; }
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
                    EffectiveStartMinuteOfDay = -1,
                    NightLightAction = "Keep",
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
                    EffectiveStartMinuteOfDay = -1,
                    NightLightAction = "Keep",
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
                    EffectiveStartMinuteOfDay = -1,
                    NightLightAction = "Keep",
                    IsDefault = true
                };
            }

            return new BrightnessScheduleTarget
            {
                Key = "period:" + target.StartMinuteOfDay.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + (target.Id ?? ""),
                BrightnessPercent = ClampBrightness(target.BrightnessPercent),
                EffectiveStartMinuteOfDay = target.StartMinuteOfDay,
                NightLightAction = NormalizeNightLightAction(target.NightLightAction),
                IsDefault = false
            };
        }

        public static string NormalizeNightLightAction(string action)
        {
            if (string.Equals(action, "On", StringComparison.OrdinalIgnoreCase))
            {
                return "On";
            }

            if (string.Equals(action, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return "Off";
            }

            return "Keep";
        }

        public static string FormatNightLightAction(string action)
        {
            string normalized = NormalizeNightLightAction(action);
            if (string.Equals(normalized, "On", StringComparison.OrdinalIgnoreCase))
            {
                return "켜기";
            }

            if (string.Equals(normalized, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return "끄기";
            }

            return "유지";
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

    internal static class NightLightController
    {
        private const string CurrentCloudStorePath =
            @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current";

        public static NightLightStatus GetStatus()
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(CurrentCloudStorePath, false))
                {
                    if (root == null)
                    {
                        return Unavailable("야간모드 상태 저장소를 찾지 못했습니다.");
                    }

                    List<string> names = root.GetSubKeyNames()
                        .Where(name => name.IndexOf("bluelightreduction", StringComparison.OrdinalIgnoreCase) >= 0
                            && name.IndexOf("state", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(name => name.IndexOf("perdevice", StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();

                    foreach (string name in names)
                    {
                        bool? enabled = TryReadState(root, name);
                        if (enabled.HasValue)
                        {
                            return new NightLightStatus
                            {
                                IsAvailable = true,
                                IsEnabled = enabled,
                                Message = enabled.Value ? "켜짐" : "꺼짐"
                            };
                        }
                    }

                    return Unavailable("야간모드 상태를 해석할 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("야간모드 상태 확인 실패", ex);
                return Unavailable("야간모드 상태 확인 실패: " + ex.Message);
            }
        }

        public static string GetStatusSummary()
        {
            NightLightStatus status = GetStatus();
            if (!status.IsAvailable || !status.IsEnabled.HasValue)
            {
                return status.Message ?? "확인 불가";
            }

            return "현재 " + (status.IsEnabled.Value ? "켜짐" : "꺼짐");
        }

        public static string ApplyAction(string action)
        {
            string normalized = BrightnessSchedule.NormalizeNightLightAction(action);
            if (string.Equals(normalized, "Keep", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            NightLightStatus status = GetStatus();
            if (!status.IsAvailable || !status.IsEnabled.HasValue)
            {
                return "야간모드 " + BrightnessSchedule.FormatNightLightAction(normalized) + " 건너뜀: " + (status.Message ?? "상태 확인 불가");
            }

            bool target = string.Equals(normalized, "On", StringComparison.OrdinalIgnoreCase);
            if (status.IsEnabled.Value == target)
            {
                return "야간모드 이미 " + (target ? "켜짐" : "꺼짐");
            }

            return "야간모드 " + (target ? "켜기" : "끄기") + " 요청: Windows 공개 제어 API가 없어 자동 변경하지 않았습니다.";
        }

        private static NightLightStatus Unavailable(string message)
        {
            return new NightLightStatus
            {
                IsAvailable = false,
                IsEnabled = null,
                Message = message
            };
        }

        private static bool? TryReadState(RegistryKey root, string parentName)
        {
            using (RegistryKey parent = root.OpenSubKey(parentName, false))
            {
                if (parent == null)
                {
                    return null;
                }

                foreach (string childName in parent.GetSubKeyNames())
                {
                    using (RegistryKey child = parent.OpenSubKey(childName, false))
                    {
                        byte[] data = child == null ? null : child.GetValue("Data") as byte[];
                        bool? decoded = DecodeNightLightState(data);
                        if (decoded.HasValue)
                        {
                            return decoded;
                        }
                    }
                }
            }

            return null;
        }

        private static bool? DecodeNightLightState(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                return null;
            }

            int trailingZeros = 0;
            for (int i = data.Length - 1; i >= 0 && data[i] == 0; i--)
            {
                trailingZeros++;
            }

            int markerIndex = data.Length - trailingZeros - 1;
            if (markerIndex < 0)
            {
                return null;
            }

            byte marker = data[markerIndex];
            if (marker == 1)
            {
                return true;
            }

            if (marker == 0)
            {
                return false;
            }

            return null;
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
            string nightLightMessage = NightLightController.ApplyAction(target.NightLightAction);
            if (result.Succeeded)
            {
                _lastAppliedScheduleKey = target.Key;
                _lastFailureMessage = "";
                DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + " 진입: " + result.Message);
                if (!string.IsNullOrWhiteSpace(nightLightMessage))
                {
                    DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + " 진입: " + nightLightMessage);
                }
                return;
            }

            if (!string.Equals(_lastFailureMessage, result.Message, StringComparison.OrdinalIgnoreCase))
            {
                _lastFailureMessage = result.Message;
                DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + ": " + result.Message);
            }

            if (!string.IsNullOrWhiteSpace(nightLightMessage))
            {
                DiagnosticsLog.WriteEvent((reason ?? "화면 밝기 일정") + " 진입: " + nightLightMessage);
            }

            _lastAppliedScheduleKey = target.Key;
        }
    }
}
