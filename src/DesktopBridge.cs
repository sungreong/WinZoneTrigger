using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal static class DesktopBridge
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        public static int Run(string[] args)
        {
            int index = Array.IndexOf(args, "--desktop-bridge");
            if (index < 0 || args.Length < index + 3) return 2;
            string output = args[index + 2];
            try
            {
                string input = File.ReadAllText(args[index + 1], Encoding.UTF8);
                // JavaScriptSerializer recognizes legacy dates only with escaped slashes.
                // Rust/JSON.stringify emit ordinary slashes when round-tripping the same value.
                input = Regex.Replace(input, "\"/Date\\((-?\\d+)\\)/\"", "\"\\/Date($1)\\/\"");
                var request = Json.Deserialize<BridgeRequest>(input);
                object result = Execute(request);
                File.WriteAllText(output, Json.Serialize(new { Ok = true, Result = result }), new UTF8Encoding(false));
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(output, Json.Serialize(new { Ok = false, Error = ex.Message }), new UTF8Encoding(false));
                return 1;
            }
        }

        private static object Execute(BridgeRequest request)
        {
            switch (request.Operation)
            {
                case "load":
                    // Never silently replace a malformed existing configuration with defaults.
                    AppConfig config = ReadStrict();
                    return new { Config = config, Revision = Revision(), Startup = StartupManager.IsEnabled() };
                case "save":
                    using (Mutex gate = new Mutex(false, @"Local\WinZoneTrigger.ConfigWrite"))
                    {
                        bool acquired = false;
                        try
                        {
                            try { acquired = gate.WaitOne(10000); } catch (AbandonedMutexException) { acquired = true; }
                            if (!acquired) throw new InvalidOperationException("설정 저장 중입니다. 잠시 후 다시 시도하세요.");
                            if (request.Revision != Revision()) throw new InvalidOperationException("다른 화면에서 설정이 변경되었습니다. 다시 불러온 후 저장하세요.");
                            Validate(request.Config);
                            ConfigStore.Save(request.Config);
                            return new { Config = request.Config, Revision = Revision() };
                        }
                        finally { if (acquired) gate.ReleaseMutex(); }
                    }
                case "scan":
                    var networks = WifiLocator.GetVisibleNetworks(true).OrderByDescending(n => n.SignalQuality).ToList();
                    return new { Networks = networks, Location = request.Location ? LocationLocator.GetCurrentLocation() : LocationReadResult.NotRequested() };
                case "new-zone": return ZoneRule.CreateDefault("새 위치");
                case "apps": return AppLauncher.FindInstalledApps(request.Query ?? "", 200, true);
                case "pick-file":
                    Application.EnableVisualStyles();
                    using (OpenFileDialog picker = new OpenFileDialog { Filter = "앱 및 바로가기|*.exe;*.lnk;*.cmd;*.bat|모든 파일|*.*" })
                        return picker.ShowDialog() == DialogResult.OK ? picker.FileName : "";
                case "startup":
                    StartupManager.SetEnabled(request.Enabled, ReadStrict().StartMinimized);
                    return StartupManager.IsEnabled();
                default: throw new InvalidOperationException("지원하지 않는 작업입니다.");
            }
        }

        private static AppConfig ReadStrict()
        {
            if (!File.Exists(ConfigStore.ConfigPath)) return AppConfig.CreateDefault();
            AppConfig config = Json.Deserialize<AppConfig>(AtomicFile.Read(ConfigStore.ConfigPath));
            if (config == null) throw new InvalidOperationException("설정 파일이 비어 있습니다.");
            config.Normalize();
            return config;
        }

        private static string Revision()
        {
            if (!File.Exists(ConfigStore.ConfigPath)) return "missing";
            using (SHA256 hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(ConfigStore.ConfigPath)));
        }

        internal static void Validate(AppConfig config)
        {
            if (config == null || config.Zones == null || config.Zones.Any(z => z == null)) throw new InvalidOperationException("위치 설정을 확인하세요.");
            if (config.Zones.Select(z => z.Id).Distinct().Count() != config.Zones.Count) throw new InvalidOperationException("위치 ID가 중복되었습니다.");
            foreach (ZoneRule z in config.Zones)
            {
                if (string.IsNullOrWhiteSpace(z.Name)) throw new InvalidOperationException("위치 이름을 입력하세요.");
                if (z.UseCoordinates && (double.IsNaN(z.Latitude) || double.IsNaN(z.Longitude) || Math.Abs(z.Latitude) > 90 || Math.Abs(z.Longitude) > 180 || z.RadiusMeters <= 0))
                    throw new InvalidOperationException(z.Name + ": 좌표와 반경을 확인하세요.");
                if (z.WifiRecoveryEnabled)
                {
                    if (string.IsNullOrWhiteSpace(z.ConnectProfile) || string.IsNullOrWhiteSpace(z.ConnectSsid)) throw new InvalidOperationException(z.Name + ": 복구할 Wi-Fi와 저장된 프로필을 선택하세요.");
                    if (!z.UseCoordinates && !(z.UseWifiCondition.GetValueOrDefault(false) && z.NearbySsids != null && z.NearbySsids.Count > 0)) throw new InvalidOperationException(z.Name + ": 먼저 위치 감지 조건을 설정하세요.");
                    if (z.WifiRecoveryIntervalSeconds < 30 || z.WifiRecoveryIntervalSeconds > 3600) throw new InvalidOperationException("Wi-Fi 확인 주기는 30~3600초로 입력하세요.");
                }
            }
            config.Normalize();
        }

        private sealed class BridgeRequest
        {
            public string Operation { get; set; }
            public AppConfig Config { get; set; }
            public string Revision { get; set; }
            public string Query { get; set; }
            public bool Location { get; set; }
            public bool Enabled { get; set; }
        }
    }
}
