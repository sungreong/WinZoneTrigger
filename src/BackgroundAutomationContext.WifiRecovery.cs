using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WinZoneTrigger
{
    internal sealed partial class BackgroundAutomationContext
    {
        private bool _wifiRecoveryInProgress;
        private DateTime _nextWifiPollUtc;
        private readonly Dictionary<string, DateTime> _wifiDue = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _wifiFailures = new Dictionary<string, int>();
        private string _lastWifiStatus = "";

        private void PollWifiRecovery()
        {
            if (_wifiRecoveryInProgress || _zoneActionInProgress || _scanInProgress
                || _config.IsAutomationPaused() || DateTime.UtcNow < _nextWifiPollUtc) return;
            List<ZoneRule> zones = _config.Zones.Where(z => z.Enabled && z.WifiRecoveryEnabled).Select(z => z.Clone()).ToList();
            if (zones.Count == 0) return;
            _wifiRecoveryInProgress = true;
            _nextWifiPollUtc = DateTime.UtcNow.AddSeconds(30);
            Task.Factory.StartNew(delegate
            {
                WifiRecoveryState state = new WifiRecoveryState { CheckedAt = DateTime.Now, Status = "waiting", Message = "감지되는 위치와 연결 대상을 기다립니다." };
                try
                {
                    ScanSnapshot scan = CreateScanSnapshot(true, zones.Any(z => z.UseCoordinates));
                    if (!string.IsNullOrWhiteSpace(scan.WifiError)) throw new InvalidOperationException(scan.WifiError);
                    List<WifiNetwork> networks = scan.Networks ?? new List<WifiNetwork>();
                    state.ConnectedSsid = string.Join(", ", networks.Where(n => n.Connected).Select(n => n.Ssid).ToArray());
                    ZoneRule selected = WifiRecoveryPolicy.Select(zones, networks, scan.LocationResult == null ? null : scan.LocationResult.Location);
                    if (selected != null)
                    {
                        string scheduleKey = new JavaScriptSerializer().Serialize(selected);
                        state.ZoneId = selected.Id;
                        state.TargetSsid = selected.ConnectSsid;
                        DateTime due;
                        if (_wifiDue.TryGetValue(scheduleKey, out due) && due > DateTime.UtcNow)
                        {
                            state.Status = "scheduled";
                            state.Message = "다음 확인 주기를 기다립니다.";
                            state.NextCheckAt = due.ToLocalTime();
                        }
                        else
                        {
                            // Re-read immediately before a side effect; edits/pause invalidate this batch.
                            AppConfig latest = ConfigStore.Load();
                            ZoneRule current = latest.Zones.FirstOrDefault(z => z.Id == selected.Id);
                            if (latest.IsAutomationPaused() || current == null || !current.Enabled || !current.WifiRecoveryEnabled
                                || new JavaScriptSerializer().Serialize(current) != new JavaScriptSerializer().Serialize(selected))
                            {
                                state.Status = "cancelled";
                                state.Message = "설정이 바뀌어 이번 확인을 건너뛰었습니다.";
                            }
                            else if (WifiRecoveryPolicy.AlreadyConnected(networks, selected.ConnectSsid))
                            {
                                _wifiFailures[scheduleKey] = 0;
                                _wifiDue[scheduleKey] = DateTime.UtcNow.AddSeconds(selected.WifiRecoveryIntervalSeconds);
                                state.Status = "connected";
                                state.Message = "원하는 Wi-Fi에 이미 연결되어 있어 건너뛰었습니다.";
                                state.NextCheckAt = _wifiDue[scheduleKey].ToLocalTime();
                            }
                            else
                            {
                                WifiConnectionResult result = WifiActions.Connect(selected.ConnectProfile, selected.ConnectSsid);
                                int failures;
                                _wifiFailures.TryGetValue(scheduleKey, out failures);
                                failures = result.Succeeded ? 0 : failures + 1;
                                _wifiFailures[scheduleKey] = failures;
                                _wifiDue[scheduleKey] = DateTime.UtcNow.AddSeconds(WifiRecoveryPolicy.RetrySeconds(selected.WifiRecoveryIntervalSeconds, failures));
                                state.NextCheckAt = _wifiDue[scheduleKey].ToLocalTime();
                                state.Status = result.Succeeded ? "reconnected" : "failed";
                                state.ConnectedSsid = result.ConnectedSsid;
                                state.Message = result.Succeeded ? "Wi-Fi 연결을 복구했습니다." : result.Summary;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    state.Status = "error";
                    state.Message = "Wi-Fi 확인 실패: " + ex.Message;
                }
                return state;
            }).ContinueWith(task => _uiContext.Post(delegate
            {
                _wifiRecoveryInProgress = false;
                if (task.IsFaulted) { DiagnosticsLog.Write("Wi-Fi 복구 작업 실패", task.Exception); return; }
                WifiRecoveryState state = task.Result;
                try { AtomicFile.Write(Path.Combine(ConfigStore.ConfigDirectory, "wifi-state.json"), new JavaScriptSerializer().Serialize(state)); }
                catch (Exception ex) { DiagnosticsLog.Write("Wi-Fi 상태 저장 실패", ex); }
                string key = state.Status + ":" + state.ZoneId;
                if (state.Status != "scheduled" && (key != _lastWifiStatus || state.Status == "failed" || state.Status == "reconnected"))
                {
                    DiagnosticsLog.WriteEvent("Wi-Fi 복구: " + state.Message);
                    _lastWifiStatus = key;
                }
            }, null));
        }
    }
}
