using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace WinZoneTrigger
{
    internal static class BrightnessOwnership
    {
        public static Dictionary<string, int> Read()
        {
            var result = new Dictionary<string, int>();
            try
            {
                using (var search = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True"))
                using (var monitors = search.Get())
                    foreach (ManagementObject monitor in monitors)
                        using (monitor) result[(string)monitor["InstanceName"]] = Convert.ToInt32(monitor["CurrentBrightness"]);
            }
            catch { }
            return result;
        }
        public static bool Same(Dictionary<string, int> expected, Dictionary<string, int> actual)
        {
            return expected != null && expected.Count > 0 && expected.Count == actual.Count
                && expected.All(pair => actual.ContainsKey(pair.Key) && actual[pair.Key] == pair.Value);
        }
        public static void Restore(Dictionary<string, int> values)
        {
            try
            {
                using (var methods = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null))
                using (var monitors = methods.GetInstances())
                    foreach (ManagementObject monitor in monitors)
                        using (monitor)
                        {
                            int value;
                            if (values.TryGetValue((string)monitor["InstanceName"], out value))
                                monitor.InvokeMethod("WmiSetBrightness", new object[] { 1, value });
                        }
                DiagnosticsLog.WriteEvent("밝기 일정 종료: 이전 밝기를 복원했습니다.");
            }
            catch (Exception ex) { DiagnosticsLog.Write("이전 밝기 복원 실패", ex); }
        }
    }
}
