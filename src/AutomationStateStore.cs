using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WinZoneTrigger
{
    internal sealed class AutomationStateSnapshot
    {
        public DateTime UpdatedAtLocal { get; set; }
        public int ProcessId { get; set; }
        public List<string> ActiveZoneIds { get; set; }
        public List<string> ActiveZoneNames { get; set; }
        public LocationInfo CurrentLocation { get; set; }
        public bool LocationWasRequested { get; set; }
        public string LocationError { get; set; }
        public List<string> VisibleSsids { get; set; }
        public string WifiError { get; set; }
    }

    internal static class AutomationStateStore
    {
        public static readonly string StatePath =
            Path.Combine(ConfigStore.ConfigDirectory, "automation-state.json");

        public static void Save(AutomationStateSnapshot snapshot)
        {
            try
            {
                if (snapshot == null)
                {
                    return;
                }

                if (!Directory.Exists(ConfigStore.ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigStore.ConfigDirectory);
                }

                string json = new JavaScriptSerializer().Serialize(snapshot);
                File.WriteAllText(StatePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("자동화 상태 저장 실패", ex);
            }
        }

        public static AutomationStateSnapshot Load()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return null;
                }

                string json = File.ReadAllText(StatePath, Encoding.UTF8);
                return new JavaScriptSerializer().Deserialize<AutomationStateSnapshot>(json);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("자동화 상태 읽기 실패", ex);
                return null;
            }
        }
    }
}
