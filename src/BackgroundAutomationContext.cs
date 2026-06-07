using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed class BackgroundAutomationContext : ApplicationContext
    {
        private readonly Dictionary<string, bool> _insideZones =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastAppWatchChecks =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer _scanTimer;
        private readonly System.Windows.Forms.Timer _appWatchTimer;
        private AppConfig _config;
        private DateTime _configLastWriteUtc;
        private bool _scanInProgress;
        private bool _appWatchInProgress;

        public BackgroundAutomationContext()
        {
            LoadConfigFromDisk("시작", false);
            _scanTimer = new System.Windows.Forms.Timer();
            _appWatchTimer = new System.Windows.Forms.Timer();
            _scanTimer.Tick += ScanTimerTick;
            _appWatchTimer.Tick += AppWatchTimerTick;

            DiagnosticsLog.WriteEvent("백그라운드 자동 실행 모드 시작");
            ResetTimers();
            StartInitialScan();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scanTimer.Stop();
                _appWatchTimer.Stop();
                _scanTimer.Dispose();
                _appWatchTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ResetTimers()
        {
            _scanTimer.Stop();
            _scanTimer.Interval = Math.Max(5, GetShortestConditionScanIntervalSeconds()) * 1000;
            if (HasZoneConditionScanZones())
            {
                _scanTimer.Start();
            }

            _appWatchTimer.Stop();
            _appWatchTimer.Interval = GetShortestAppWatchIntervalMilliseconds();
            if (HasAppWatchZones())
            {
                _appWatchTimer.Start();
            }
        }

        private void StartInitialScan()
        {
            if (HasStartupRunOnceZones())
            {
                StartScan(true, true);
                return;
            }

            if (HasZoneConditionScanZones())
            {
                StartScan(false, false);
            }
        }

        private void ScanTimerTick(object sender, EventArgs e)
        {
            if (ReloadConfigIfChanged())
            {
                StartInitialScan();
                return;
            }

            if (_appWatchInProgress)
            {
                DiagnosticsLog.WriteEvent("백그라운드 위치 확인 건너뜀: 앱 감시 진행 중");
                return;
            }

            StartScan(false, false);
        }

        private void AppWatchTimerTick(object sender, EventArgs e)
        {
            if (ReloadConfigIfChanged())
            {
                StartInitialScan();
                return;
            }

            if (_scanInProgress)
            {
                DiagnosticsLog.WriteEvent("백그라운드 앱 감시 건너뜀: 위치 확인 진행 중");
                return;
            }

            RunDueAppWatchChecks(false, "백그라운드 앱 감시");
        }

        private void StartScan(bool forceScan, bool startupOnly)
        {
            ReloadConfigIfChanged();
            if (_scanInProgress || _appWatchInProgress)
            {
                DiagnosticsLog.WriteEvent("백그라운드 위치 확인 건너뜀: 다른 자동 작업 진행 중");
                return;
            }

            _scanInProgress = true;
            DiagnosticsLog.WriteEvent(startupOnly ? "백그라운드 시작 위치 확인" : "백그라운드 위치 조건 확인");

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(forceScan, _config.Zones.Any(z => z.Enabled && z.UseCoordinates));
            }).ContinueWith(delegate(Task<ScanSnapshot> task)
            {
                _scanInProgress = false;
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 위치 확인 오류" : task.Exception.GetBaseException().Message;
                    DiagnosticsLog.WriteEvent("백그라운드 위치 확인 실패: " + message);
                    return;
                }

                ProcessScanResult(task.Result, startupOnly);
            });
        }

        private ScanSnapshot CreateScanSnapshot(bool forceScan, bool requestLocation)
        {
            string outputPath = Path.Combine(Path.GetTempPath(), "WinZoneTrigger.scan." + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string arguments = "--scan-helper --out " + QuoteCommandArgument(outputPath);
                if (forceScan)
                {
                    arguments += " --force";
                }
                if (requestLocation)
                {
                    arguments += " --location";
                }

                CommandResult command = CommandRunner.Run(Application.ExecutablePath, arguments, requestLocation ? 45000 : 20000);
                if (command.TimedOut)
                {
                    DiagnosticsLog.WriteEvent("백그라운드 탐지 헬퍼 시간 초과: location=" + requestLocation);
                    return CreateFailedSnapshot(requestLocation, "탐지 헬퍼 시간 초과");
                }

                if (!File.Exists(outputPath))
                {
                    DiagnosticsLog.WriteEvent("백그라운드 탐지 헬퍼 결과 없음: exit=" + command.ExitCode);
                    return CreateFailedSnapshot(requestLocation, "탐지 헬퍼 결과 없음");
                }

                string json = File.ReadAllText(outputPath, Encoding.UTF8);
                ScanSnapshot snapshot = new JavaScriptSerializer().Deserialize<ScanSnapshot>(json);
                if (snapshot == null)
                {
                    return CreateFailedSnapshot(requestLocation, "탐지 헬퍼 결과 해석 실패");
                }

                if (command.ExitCode != 0)
                {
                    DiagnosticsLog.WriteEvent("백그라운드 탐지 헬퍼 비정상 종료: exit=" + command.ExitCode);
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("백그라운드 탐지 헬퍼 처리 실패", ex);
                return CreateFailedSnapshot(requestLocation, ex.Message);
            }
            finally
            {
                try
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                    }
                }
                catch
                {
                }
            }
        }

        private static ScanSnapshot CreateFailedSnapshot(bool requestLocation, string error)
        {
            return new ScanSnapshot
            {
                Networks = new List<WifiNetwork>(),
                WifiError = error,
                LocationResult = requestLocation
                    ? new LocationReadResult { WasRequested = true, Error = error }
                    : LocationReadResult.NotRequested()
            };
        }

        private void ProcessScanResult(ScanSnapshot snapshot, bool startupOnly)
        {
            HashSet<string> visibleSsids = new HashSet<string>(
                (snapshot.Networks ?? new List<WifiNetwork>())
                    .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                    .Select(n => n.Ssid),
                StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
            {
                DiagnosticsLog.WriteEvent("백그라운드 Wi-Fi 확인 실패: " + snapshot.WifiError);
            }

            LocationInfo currentLocation = snapshot.LocationResult != null && snapshot.LocationResult.HasLocation
                ? snapshot.LocationResult.Location
                : null;
            if (snapshot.LocationResult != null && snapshot.LocationResult.WasRequested && currentLocation == null)
            {
                DiagnosticsLog.WriteEvent("백그라운드 위치 사용 불가: " + snapshot.LocationResult.Error);
            }

            List<string> activeZoneIds = new List<string>();
            List<string> activeZoneNames = new List<string>();

            foreach (ZoneRule zone in _config.Zones)
            {
                zone.Normalize();
                bool near = zone.Enabled && ZoneMatches(zone, visibleSsids, currentLocation);
                bool wasInside = IsZoneActive(zone);
                bool eligible = startupOnly
                    ? zone.RunOnceAtStartup.GetValueOrDefault(true)
                    : zone.MonitoringEnabled.GetValueOrDefault(false);

                if (near && !wasInside)
                {
                    _insideZones[zone.Id] = true;
                    DiagnosticsLog.WriteEvent("백그라운드 위치 진입: " + zone.Name);
                    if (eligible)
                    {
                        TriggerZone(zone.Clone(), startupOnly ? "백그라운드 시작 시 1회 실행" : "백그라운드 위치 진입");
                    }
                    if (zone.GetEnabledAppWatchItems().Any())
                    {
                        RunDueAppWatchChecks(true, "백그라운드 앱 감시 시작 확인");
                    }
                }
                else if (!near && wasInside)
                {
                    _insideZones[zone.Id] = false;
                    DiagnosticsLog.WriteEvent("백그라운드 위치 이탈: " + zone.Name);
                }

                if (IsZoneActive(zone))
                {
                    activeZoneIds.Add(zone.Id);
                    activeZoneNames.Add(zone.Name);
                }
            }

            SaveAutomationState(activeZoneIds, activeZoneNames, snapshot, visibleSsids, currentLocation);
        }

        private void TriggerZone(ZoneRule zone, string reason)
        {
            DiagnosticsLog.WriteEvent(reason + ": " + zone.Name);
            Task.Factory.StartNew(delegate
            {
                try
                {
                    ZoneExecutor.Execute(zone, DiagnosticsLog.WriteEvent);
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Write("백그라운드 동작 실행 실패: " + (zone == null ? "" : zone.Name), ex);
                }
            });
        }

        private void RunDueAppWatchChecks(bool force, string reason)
        {
            if (_appWatchInProgress || _scanInProgress)
            {
                DiagnosticsLog.WriteEvent(reason + " 건너뜀: 다른 자동 작업 진행 중");
                return;
            }

            DateTime now = DateTime.UtcNow;
            List<Tuple<ZoneRule, AppWatchItem>> dueTargets = new List<Tuple<ZoneRule, AppWatchItem>>();
            foreach (ZoneRule zone in _config.Zones.Where(z => z.Enabled && IsZoneActive(z)))
            {
                foreach (AppWatchItem item in zone.GetEnabledAppWatchItems())
                {
                    string key = zone.Id + ":" + item.Id;
                    DateTime last;
                    bool due = force
                        || !_lastAppWatchChecks.TryGetValue(key, out last)
                        || (now - last).TotalMilliseconds >= GetAppWatchIntervalMilliseconds(item.IntervalValue, item.IntervalUnit);
                    if (due)
                    {
                        _lastAppWatchChecks[key] = now;
                        dueTargets.Add(Tuple.Create(zone.Clone(), item.Clone()));
                    }
                }
            }

            if (dueTargets.Count == 0)
            {
                return;
            }

            _appWatchInProgress = true;
            DiagnosticsLog.WriteEvent(reason + " 시작: " + dueTargets.Count + "개 항목");
            Task.Factory.StartNew(delegate
            {
                foreach (Tuple<ZoneRule, AppWatchItem> target in dueTargets)
                {
                    AppWatchItem item = target.Item2;
                    string processName = AppWatchdog.NormalizeProcessName(item.ProcessName);
                    if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(item.LaunchTarget))
                    {
                        DiagnosticsLog.WriteEvent(reason + " 건너뜀: 앱 감시 설정 부족 - " + target.Item1.Name);
                        continue;
                    }

                    try
                    {
                        AppWatchCheckResult result = AppWatchdog.EnsureRunning(
                            processName,
                            item.LaunchTarget,
                            false,
                            DiagnosticsLog.WriteEvent);
                        DiagnosticsLog.WriteEvent(reason + " 결과(" + target.Item1.Name + "): " + result.Summary);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticsLog.Write("백그라운드 앱 감시 실패: " + target.Item1.Name, ex);
                    }
                }
            }).ContinueWith(delegate
            {
                _appWatchInProgress = false;
            });
        }

        private bool ZoneMatches(ZoneRule zone, HashSet<string> visibleSsids, LocationInfo currentLocation)
        {
            if (zone.UseCoordinates)
            {
                if (currentLocation == null)
                {
                    return false;
                }

                double distanceMeters = GeoMath.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    zone.Latitude,
                    zone.Longitude);
                return distanceMeters <= zone.RadiusMeters;
            }

            List<string> wanted = zone.NearbySsids
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (wanted.Count == 0)
            {
                return false;
            }

            return zone.RequireAllSsids ? wanted.All(visibleSsids.Contains) : wanted.Any(visibleSsids.Contains);
        }

        private bool IsZoneActive(ZoneRule zone)
        {
            bool active;
            return zone != null
                && !string.IsNullOrWhiteSpace(zone.Id)
                && _insideZones.TryGetValue(zone.Id, out active)
                && active;
        }

        private bool HasStartupRunOnceZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.RunOnceAtStartup.GetValueOrDefault(true));
        }

        private bool HasZoneConditionScanZones()
        {
            return _config.Zones.Any(z => z.Enabled && (z.MonitoringEnabled.GetValueOrDefault(false) || z.GetEnabledAppWatchItems().Any()));
        }

        private bool HasAppWatchZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.GetEnabledAppWatchItems().Any());
        }

        private int GetShortestConditionScanIntervalSeconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled && (z.MonitoringEnabled.GetValueOrDefault(false) || z.GetEnabledAppWatchItems().Any()))
                .Select(z => z.ScanIntervalSeconds < 5 ? 30 : z.ScanIntervalSeconds)
                .ToList();
            return intervals.Count == 0 ? 30 : intervals.Min();
        }

        private int GetShortestAppWatchIntervalMilliseconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled)
                .SelectMany(z => z.GetEnabledAppWatchItems())
                .Select(item => GetAppWatchIntervalMilliseconds(item.IntervalValue, item.IntervalUnit))
                .ToList();
            return intervals.Count == 0 ? GetAppWatchIntervalMilliseconds(5, "Minutes") : intervals.Min();
        }

        private static int GetAppWatchIntervalMilliseconds(int value, string unit)
        {
            long multiplier = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 3600000L : 60000L;
            long milliseconds = Math.Max(1, value) * multiplier;
            return milliseconds > int.MaxValue ? int.MaxValue : Convert.ToInt32(milliseconds);
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
                _insideZones.Clear();
                _lastAppWatchChecks.Clear();
            }

            DiagnosticsLog.WriteEvent("백그라운드 설정 로드: " + reason + " / zones=" + _config.Zones.Count);
        }

        private static void SaveAutomationState(
            List<string> activeZoneIds,
            List<string> activeZoneNames,
            ScanSnapshot snapshot,
            HashSet<string> visibleSsids,
            LocationInfo currentLocation)
        {
            LocationReadResult locationResult = snapshot == null ? null : snapshot.LocationResult;
            AutomationStateStore.Save(new AutomationStateSnapshot
            {
                UpdatedAtLocal = DateTime.Now,
                ProcessId = Process.GetCurrentProcess().Id,
                ActiveZoneIds = activeZoneIds ?? new List<string>(),
                ActiveZoneNames = activeZoneNames ?? new List<string>(),
                CurrentLocation = currentLocation,
                LocationWasRequested = locationResult != null && locationResult.WasRequested,
                LocationError = locationResult == null ? "" : locationResult.Error,
                VisibleSsids = visibleSsids == null
                    ? new List<string>()
                    : visibleSsids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                WifiError = snapshot == null ? "" : snapshot.WifiError
            });
        }
    }
}
