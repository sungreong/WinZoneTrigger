using System;
using System.Collections.Generic;

namespace WinZoneTrigger
{
    internal static class WifiRadioRecoveryTests
    {
        private sealed class FakeRadio : IWifiRadio
        {
            public List<WifiRadio> Radios = new List<WifiRadio>();
            public int Writes;
            public bool Fail;
            public List<WifiRadio> Read() { return Radios; }
            public void TurnOn(WifiRadio radio)
            {
                Writes++;
                if (Fail) throw new InvalidOperationException("access denied");
                radio.SoftwareState = 1;
            }
            public void Dispose() { }
        }

        public static void Run()
        {
            var adapter = new FakeRadio();
            adapter.Radios.Add(new WifiRadio { SoftwareState = 2, HardwareState = 1 });
            Check(WifiRadioRecovery.Prepare(adapter, () => false).Status == "cancelled" && adapter.Writes == 0, "disabled recovery never powers on");
            int checks = 0;
            Check(WifiRadioRecovery.Prepare(adapter, () => ++checks == 1).Status == "cancelled" && adapter.Writes == 0, "pause before radio write cancels");
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Status == "radio-starting" && adapter.Writes == 1, "software off requests power once");
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Ready && adapter.Writes == 1, "next batch on skips power command");
            var zone = ZoneRule.CreateDefault("home");
            zone.Enabled = true; zone.WifiRecoveryEnabled = true; zone.ConnectSsid = "Home";
            zone.ConnectProfile = "Home"; zone.UseWifiCondition = true; zone.NearbySsids = new List<string> { "Home" };
            Check(WifiRecoveryPolicy.Select(new[] { zone }, new List<WifiNetwork>(), null) == null, "power on alone cannot select an unknown location");
            var visible = new List<WifiNetwork> { new WifiNetwork { Ssid = "Home", Connectable = true } };
            Check(WifiRecoveryPolicy.Select(new[] { zone }, visible, null) == zone, "fresh location after power on permits connection");
            visible[0].Connected = true;
            Check(WifiRecoveryPolicy.AlreadyConnected(visible, "Home"), "Windows auto connection skips app reconnect");
            adapter.Radios[0].SoftwareState = 2;
            adapter.Radios[0].HardwareState = 2;
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Status == "radio-blocked" && adapter.Writes == 1, "hardware off is not changed");
            adapter.Radios[0].HardwareState = 1; adapter.Fail = true;
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Status == "radio-error", "native set failure is visible");
            adapter.Radios.Clear();
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Status == "radio-unavailable", "missing adapter is distinct");
            adapter.Radios.Add(new WifiRadio { SoftwareState = 1, HardwareState = 1 });
            adapter.Radios.Add(new WifiRadio { SoftwareState = 2, HardwareState = 1 });
            int writes = adapter.Writes;
            Check(WifiRadioRecovery.Prepare(adapter, () => true).Ready && adapter.Writes == writes, "usable adapter avoids enabling another adapter");
            var config = AppConfig.CreateDefault(); config.Zones.Clear(); config.Zones.Add(zone);
            Check(WifiRadioRecovery.Allowed(config), "configured recovery permits power discovery");
            config.AutomationPausedUntilUtc = DateTime.UtcNow.AddMinutes(1);
            Check(!WifiRadioRecovery.Allowed(config), "global pause blocks power discovery");
            config.AutomationPausedUntilUtc = null; zone.Enabled = false;
            Check(!WifiRadioRecovery.Allowed(config), "disabled zone blocks power discovery");
            zone.Enabled = true; zone.ConnectProfile = "";
            Check(!WifiRadioRecovery.Allowed(config), "missing profile blocks power discovery");
            zone.ConnectProfile = "Home"; zone.ScheduleEnabled = true;
            zone.ScheduleDays = 1 << (((int)DateTime.Now.DayOfWeek + 2) % 7);
            zone.ScheduleStartMinute = 0; zone.ScheduleEndMinute = 0;
            Check(!WifiRadioRecovery.Allowed(config), "outside schedule blocks power discovery");
        }
        private static void Check(bool value, string description) { if (!value) throw new Exception(description); }
    }
}
