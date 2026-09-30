using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WinZoneTrigger
{
    internal static class AutomationLogicTests
    {
        public static int Run()
        {
            try
            {
                ZoneRule home = ZoneRule.CreateDefault("Home");
                home.Id = "home"; home.WifiRecoveryEnabled = true; home.WifiPriority = 10;
                home.ConnectSsid = "Home"; home.ConnectProfile = "Saved home";
                home.UseWifiCondition = true; home.NearbySsids.Add("Home");
                var networks = new List<WifiNetwork> { new WifiNetwork { Ssid = "Home", Connectable = true } };
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == home, "disconnected but visible home selected");
                networks[0].Connected = true;
                Check(WifiRecoveryPolicy.AlreadyConnected(networks, "Home"), "already connected skips command");
                Check(!WifiRecoveryPolicy.AlreadyConnected(networks, "home"), "different case never treated as connected");
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == home, "already connected remains selected");
                Check(WifiRecoveryPolicy.Select(new[] { home }, new List<WifiNetwork>(), null) == null, "out of range never connects");
                networks[0].Ssid = "home";
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == null, "SSID case sensitive");
                networks[0].Ssid = "Home";
                ZoneRule other = home.Clone(); other.Id = "other"; other.ConnectSsid = "Office"; other.WifiPriority = 20;
                networks.Add(new WifiNetwork { Ssid = "Office", Connected = true, Connectable = true });
                Check(WifiRecoveryPolicy.Select(new[] { other, home }, networks, null) == home, "priority resolves overlapping locations");
                home.Enabled = false;
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == null, "disabled location skipped");
                home.Enabled = true; home.RequireAllSsids = true; home.NearbySsids.Add("Beacon");
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == null, "all SSIDs required");
                home.NearbySsids.Remove("Beacon");
                home.ConnectProfile = "";
                Check(WifiRecoveryPolicy.Select(new[] { home }, networks, null) == null, "missing profile skipped");
                Check(WifiRecoveryPolicy.RetrySeconds(60, 1) == 120, "retry backoff");
                Check(WifiRecoveryPolicy.RetrySeconds(3600, 100) == 3600, "backoff bounded");
                AppConfig invalid = AppConfig.CreateDefault(); invalid.Zones[0].WifiRecoveryEnabled = true;
                bool rejected = false; try { DesktopBridge.Validate(invalid); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "invalid recovery config rejected");
                string directory = Path.Combine(Path.GetTempPath(), "WinZoneAtomicTest-" + Guid.NewGuid().ToString("N"));
                string path = Path.Combine(directory, "state.json");
                Task writer = null;
                try
                {
                    AtomicFile.Write(path, "initial");
                    writer = Task.Factory.StartNew(delegate { for (int i = 0; i < 200; i++) AtomicFile.Write(path, "complete:" + i); });
                    for (int i = 0; i < 500; i++)
                    {
                        string text = AtomicFile.Read(path);
                        Check(text == "initial" || text.StartsWith("complete:"), "concurrent read never sees partial state");
                    }
                    writer.Wait();
                    Check(AtomicFile.Read(path) == "complete:199", "all atomic writes completed");
                }
                finally { if (writer != null) writer.Wait(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logic-test-result.txt"), "PASS: 13 automation policy checks + 200 concurrent writes / 500 reads");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logic-test-result.txt"), ex.ToString());
                return 1;
            }
        }
        private static void Check(bool value, string description) { if (!value) throw new Exception(description); }
    }
}
