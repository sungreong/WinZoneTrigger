using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class BackgroundAutomationContext : ApplicationContext
    {
        private readonly Dictionary<string, bool> _insideZones =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _lastAppWatchChecks =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _startupTriggeredZoneIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _automationStateLock = new object();
        private readonly object _actionQueueLock = new object();
        private readonly System.Windows.Forms.Timer _scanTimer;
        private readonly System.Windows.Forms.Timer _appWatchTimer;
        private readonly System.Windows.Forms.Timer _startupRetryTimer;
        private readonly System.Windows.Forms.Timer _brightnessTimer;
        private readonly System.Windows.Forms.Timer _configRefreshTimer;
        private readonly PowerStateMonitor _powerStateMonitor;
        private readonly BrightnessScheduleRunner _brightnessScheduleRunner;
        private readonly SynchronizationContext _uiContext;
        private AppConfig _config;
        private DateTime _configLastWriteUtc;
        private bool _scanInProgress;
        private bool _appWatchInProgress;
        private volatile bool _zoneActionInProgress;
        private bool _startupRetryActive;
        private int _startupRetryAttemptsRemaining;
        private int _startupRetryAttemptsTotal;
        private Task _lastZoneActionTask;
        private List<string> _stateActiveZoneIds = new List<string>();
        private List<string> _stateActiveZoneNames = new List<string>();
        private LocationInfo _stateCurrentLocation;
        private bool _stateLocationWasRequested;
        private string _stateLocationError = "";
        private List<string> _stateVisibleSsids = new List<string>();
        private string _stateWifiError = "";
        private DateTime _stateLastEventAtLocal;
        private string _stateLastEventText = "";
        private string _stateLastActionText = "";
        private string _stateLastAppWatchText = "";
        private string _stateLastAppWatchZoneId = "";
        private string _stateLastAppWatchItemId = "";
        private string _stateLastAppWatchItemText = "";
        private volatile bool _powerResumeDetected;
        private bool _automationWasPaused;
        private DateTime _lastHeartbeatUtc;

        public BackgroundAutomationContext()
        {
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            _uiContext = SynchronizationContext.Current;
            LoadConfigFromDisk("시작", false);
            _scanTimer = new System.Windows.Forms.Timer();
            _appWatchTimer = new System.Windows.Forms.Timer();
            _startupRetryTimer = new System.Windows.Forms.Timer();
            _brightnessTimer = new System.Windows.Forms.Timer();
            _configRefreshTimer = new System.Windows.Forms.Timer();
            _scanTimer.Tick += ScanTimerTick;
            _appWatchTimer.Tick += AppWatchTimerTick;
            _startupRetryTimer.Interval = 30000;
            _startupRetryTimer.Tick += StartupRetryTimerTick;
            _brightnessTimer.Interval = 60000;
            _brightnessTimer.Tick += BrightnessTimerTick;
            _configRefreshTimer.Interval = 5000;
            _configRefreshTimer.Tick += ConfigRefreshTimerTick;
            _configRefreshTimer.Start();
            _brightnessScheduleRunner = new BrightnessScheduleRunner();
            _powerStateMonitor = new PowerStateMonitor(HandlePowerModeChanged);

            DiagnosticsLog.WriteEvent("백그라운드 자동 실행 모드 시작");
            UpdateAutomationEvent("백그라운드 자동 실행 모드 시작", null, null);
            ResetTimers();
            ApplyPowerSettings();
            ApplyBrightnessSchedule("백그라운드 시작 화면 밝기 일정");
            StartInitialScan();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scanTimer.Stop();
                _appWatchTimer.Stop();
                _startupRetryTimer.Stop();
                _brightnessTimer.Stop();
                _configRefreshTimer.Stop();
                _scanTimer.Dispose();
                _appWatchTimer.Dispose();
                _startupRetryTimer.Dispose();
                _brightnessTimer.Dispose();
                _configRefreshTimer.Dispose();
                _powerStateMonitor.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ResetTimers()
        {
            if (_config != null && _config.IsAutomationPaused())
            {
                _scanTimer.Stop();
                _appWatchTimer.Stop();
                _startupRetryTimer.Stop();
                _brightnessTimer.Stop();
                _startupRetryActive = false;
                if (!_automationWasPaused)
                {
                    string until = _config.AutomationPausedUntilUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                    DiagnosticsLog.WriteEvent("자동화 임시 정지 시작: " + until + "까지");
                    UpdateAutomationEvent("자동화 임시 정지 중: " + until + "까지", "임시 정지", "임시 정지");
                }
                _automationWasPaused = true;
                ApplyPowerSettings();
                return;
            }

            if (_automationWasPaused)
            {
                _automationWasPaused = false;
                DiagnosticsLog.WriteEvent("자동화 임시 정지 종료");
                UpdateAutomationEvent("자동화 임시 정지 종료", "자동 실행 재개", "앱 감시 재개");
            }

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

            _brightnessTimer.Stop();
            if (_config.BrightnessScheduleEnabled)
            {
                _brightnessTimer.Start();
            }

            ApplyPowerSettings();
        }

        private void ConfigRefreshTimerTick(object sender, EventArgs e)
        {
            bool wasPaused = _automationWasPaused;
            ReloadConfigIfChanged();
            PollQuickControls();
            ObserveAudioState();
            PollWifiRecovery();
            if (DateTime.UtcNow - _lastHeartbeatUtc > TimeSpan.FromSeconds(60))
            {
                _lastHeartbeatUtc = DateTime.UtcNow;
                lock (_automationStateLock) SaveAutomationStateLocked();
            }
            if (!wasPaused || _config == null || _config.IsAutomationPaused())
            {
                return;
            }

            if (_automationWasPaused)
            {
                ResetTimers();
            }
            ApplyBrightnessSchedule("자동화 임시 정지 해제 화면 밝기 일정");
            if (HasZoneConditionScanZones())
            {
                StartScan(false, false);
            }
        }

        private void BrightnessTimerTick(object sender, EventArgs e)
        {
            if (ReloadConfigIfChanged())
            {
                ApplyBrightnessSchedule("설정 변경 화면 밝기 일정");
                return;
            }

            ApplyBrightnessSchedule("시간대별 화면 밝기 일정");
        }

        private void ApplyBrightnessSchedule(string reason)
        {
            if (_config != null && _config.IsAutomationPaused()) return;
            _brightnessScheduleRunner.Apply(_config, reason);
        }

        private void StartInitialScan()
        {
            if (_config != null && _config.IsAutomationPaused())
            {
                return;
            }

            if (HasStartupRunOnceZones())
            {
                StartStartupRetrySequence();
                return;
            }

            StopStartupRetry("시작 시 1회 실행 대상 위치가 없어 부팅 초기 확인을 종료합니다.");
            if (HasZoneConditionScanZones())
            {
                StartScan(false, false);
            }
        }

        private void StartStartupRetrySequence()
        {
            _startupRetryAttemptsTotal = 12;
            _startupRetryAttemptsRemaining = _startupRetryAttemptsTotal;
            _startupRetryActive = true;
            lock (_startupTriggeredZoneIds)
            {
                _startupTriggeredZoneIds.Clear();
            }
            _startupRetryTimer.Stop();
            DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 확인 시작: 최대 " + _startupRetryAttemptsTotal + "회");
            RunStartupRetryAttempt();
        }

        private void StartupRetryTimerTick(object sender, EventArgs e)
        {
            RunStartupRetryAttempt();
        }

        private void RunStartupRetryAttempt()
        {
            if (!_startupRetryActive)
            {
                _startupRetryTimer.Stop();
                return;
            }

            if (!HasStartupRunOnceZones())
            {
                StopStartupRetry("시작 시 1회 실행 대상 위치가 없어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (AllStartupRunOnceZonesTriggered())
            {
                StopStartupRetry("시작 시 1회 실행 대상 위치 처리가 완료되어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (_scanInProgress || _appWatchInProgress || _zoneActionInProgress || _wifiRecoveryInProgress)
            {
                DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 확인 대기: 다른 자동 작업 진행 중");
                _startupRetryTimer.Start();
                return;
            }

            if (_startupRetryAttemptsRemaining <= 0)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                return;
            }

            int attemptNumber = _startupRetryAttemptsTotal - _startupRetryAttemptsRemaining + 1;
            _startupRetryAttemptsRemaining--;
            DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 확인 " + attemptNumber + "/" + _startupRetryAttemptsTotal);
            StartScan(true, true);
            if (_startupRetryAttemptsRemaining > 0)
            {
                _startupRetryTimer.Start();
            }
        }

        private void StopStartupRetry(string reason)
        {
            if (!_startupRetryActive && !_startupRetryTimer.Enabled)
            {
                return;
            }

            _startupRetryTimer.Stop();
            _startupRetryActive = false;
            DiagnosticsLog.WriteEvent(reason);
            UpdateAutomationEvent(reason, null, null);
        }

        private void ScanTimerTick(object sender, EventArgs e)
        {
            ReloadConfigIfChanged();

            if (_startupRetryActive)
            {
                DiagnosticsLog.WriteEvent("백그라운드 위치 조건 확인 대기: 부팅 초기 확인 진행 중");
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
            ReloadConfigIfChanged();

            if (_startupRetryActive)
            {
                DiagnosticsLog.WriteEvent("백그라운드 앱 감시: 부팅 초기 확인과 병행합니다.");
            }

            bool resumeDetected = ConsumePowerResumeDetected();
            RunDueAppWatchChecks(resumeDetected, resumeDetected ? "절전 복귀 앱 감시" : "백그라운드 앱 감시");
        }

        private void StartScan(bool forceScan, bool startupOnly)
        {
            ReloadConfigIfChanged();
            if (_config != null && _config.IsAutomationPaused())
            {
                return;
            }
            if (_scanInProgress || _appWatchInProgress || _zoneActionInProgress || _wifiRecoveryInProgress)
            {
                DiagnosticsLog.WriteEvent("백그라운드 위치 확인 건너뜀: 다른 자동 작업 진행 중");
                return;
            }

            _scanInProgress = true;
            DiagnosticsLog.WriteThrottled("scan-start:" + startupOnly, startupOnly ? "백그라운드 시작 위치 확인" : "백그라운드 위치 조건 확인");

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(forceScan, _config.Zones.Any(z => z.Enabled && z.UseCoordinates));
            }).ContinueWith(task => _uiContext.Post(delegate
            {
                _scanInProgress = false;
                if (task.IsFaulted)
                {
                    _manualZoneId = null;
                    string message = task.Exception == null ? "알 수 없는 위치 확인 오류" : task.Exception.GetBaseException().Message;
                    DiagnosticsLog.WriteEvent("백그라운드 위치 확인 실패: " + message);
                    UpdateAutomationEvent("백그라운드 위치 확인 실패: " + message, null, null);
                    return;
                }

                ProcessScanResult(task.Result, startupOnly);
            }, null));
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
                StringComparer.Ordinal);

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
            bool preserveActiveZones = ScanReliability.HasTransientDetectionError(snapshot);
            List<ZoneRule> zonesToTrigger = new List<ZoneRule>();
            var decisions = new List<ZoneDecision>();
            string manualZone = _manualZoneId;
            _manualZoneId = null;
            bool startupWifiMatchedZoneExists = startupOnly && HasEligibleStartupWifiMatch(visibleSsids, currentLocation);

            DiagnosticsLog.WriteThrottled("scan", "백그라운드 scan 요약: Wi-Fi="
                + FormatVisibleSsids(visibleSsids)
                + " / 위치=" + FormatLocationForLog(currentLocation)
                + " / startupOnly=" + startupOnly);

            foreach (ZoneRule zone in _config.Zones)
            {
                zone.Normalize();
                bool wasInside = IsZoneActive(zone);
                if (manualZone != null && manualZone != zone.Id)
                {
                    if (wasInside) { activeZoneIds.Add(zone.Id); activeZoneNames.Add(zone.Name); }
                    decisions.Add(new ZoneDecision { ZoneId = zone.Id, Name = zone.Name, CheckedAt = DateTime.Now,
                        Message = "지금 실행에서 선택하지 않은 위치 · 기존 감시 상태 유지" });
                    continue;
                }
                ZoneMatchResult match = AnalyzeZoneMatch(zone, visibleSsids, currentLocation, startupOnly);
                bool timeAllowed = ZoneSchedule.Allows(zone, DateTime.Now);
                bool actualNear = zone.Enabled && timeAllowed && match.Matches;
                bool near = zone.Enabled && timeAllowed && (match.Matches || (preserveActiveZones && wasInside));
                near = StabilizeExit(zone.Id, near, wasInside, !timeAllowed || !zone.Enabled);
                bool eligible = startupOnly
                    ? zone.RunOnceAtStartup.GetValueOrDefault(true)
                    : zone.MonitoringEnabled.GetValueOrDefault(false);
                string reason = timeAllowed ? match.Reason : "요일·시간 조건 밖";

                if (startupOnly && startupWifiMatchedZoneExists && match.StartupWifiConnectionKickoff && !match.WifiMatch)
                {
                    actualNear = false;
                    near = false;
                    reason += "; Wi-Fi 일치 위치 우선으로 좌표 기반 연결 건너뜀";
                }
                bool shouldTrigger = eligible && (startupOnly
                    ? actualNear && !IsStartupZoneCompleted(zone)
                    : near && !wasInside);

                if (manualZone != null) shouldTrigger = manualZone == zone.Id && actualNear;
                decisions.Add(new ZoneDecision { ZoneId = zone.Id, Name = zone.Name, CheckedAt = DateTime.Now,
                    Message = !zone.Enabled ? "위치 비활성" : !timeAllowed ? "요일·시간 조건 밖 · 실행 대기"
                    : shouldTrigger ? reason + " → 실행 요청" : !near ? reason + " → 대기"
                    : !eligible ? "위치 감지됨 · 진입 동작 지속 감시 꺼짐" : "이미 실행한 위치 · 중복 실행 건너뜀" });

                DiagnosticsLog.WriteThrottled("zone:" + zone.Id + ":" + near + ":" + eligible, "백그라운드 위치 판정: " + zone.Name
                    + " / enabled=" + zone.Enabled
                    + " / eligible=" + eligible
                    + " / activeBefore=" + wasInside
                    + " / near=" + near
                    + " / reason=" + reason);

                if (startupOnly && shouldTrigger && wasInside)
                {
                    DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 동작 재시도 대상: " + zone.Name);
                }

                if (near && !wasInside)
                {
                    _insideZones[zone.Id] = true;
                    DiagnosticsLog.WriteEvent("백그라운드 위치 진입: " + zone.Name);
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

                if (shouldTrigger)
                {
                    zonesToTrigger.Add(zone.Clone());
                }
            }

            SaveDecisions(decisions);
            ReconcileAudio(activeZoneIds);
            LogConflictingWifiActions(zonesToTrigger);
            foreach (ZoneRule zone in zonesToTrigger)
            {
                TriggerZone(zone, startupOnly ? "백그라운드 Windows 시작 후 한 번 실행" : "백그라운드 조건 진입 시 실행", startupOnly);
            }

            SaveAutomationState(activeZoneIds, activeZoneNames, snapshot, visibleSsids, currentLocation, "백그라운드 위치 조건 확인 완료");
            if (startupOnly && _startupRetryActive && AllStartupRunOnceZonesTriggered())
            {
                StopStartupRetry("시작 시 1회 실행 대상 위치 처리가 완료되어 부팅 초기 확인을 종료합니다.");
            }
            else if (startupOnly
                && _startupRetryActive
                && _startupRetryAttemptsRemaining <= 0
                && zonesToTrigger.Count == 0
                && !_scanInProgress
                && !_zoneActionInProgress)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
            }
        }

        private void TriggerZone(ZoneRule zone, string reason, bool startupOnly)
        {
            string startMessage = reason + ": " + zone.Name;
            DiagnosticsLog.WriteEvent(startMessage);
            UpdateAutomationEvent(startMessage, "동작 실행 중: " + zone.Name, null);
            EnqueueZoneAction(zone, reason, startupOnly);
        }

        private bool HasEligibleStartupWifiMatch(HashSet<string> visibleSsids, LocationInfo currentLocation)
        {
            foreach (ZoneRule zone in _config.Zones)
            {
                zone.Normalize();
                if (!zone.Enabled || !ZoneSchedule.Allows(zone, DateTime.Now) || !zone.RunOnceAtStartup.GetValueOrDefault(true))
                {
                    continue;
                }

                ZoneMatchResult match = AnalyzeZoneMatch(zone, visibleSsids, currentLocation, true);
                if (match.WifiMatch)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnqueueZoneAction(ZoneRule zone, string reason, bool startupOnly)
        {
            lock (_actionQueueLock)
            {
                Task previous = _lastZoneActionTask;
                _lastZoneActionTask = Task.Factory.StartNew(delegate
                {
                    if (previous != null)
                    {
                        try
                        {
                            previous.Wait();
                        }
                        catch
                        {
                        }
                    }

                    _zoneActionInProgress = true;
                    try
                    {
                        if (IsAutomationPausedInSavedConfig())
                        {
                            DiagnosticsLog.WriteEvent("백그라운드 동작 실행 건너뜀: 자동화 임시 정지 중 - " + zone.Name);
                            return;
                        }

                        AppConfig latest = ConfigStore.Load();
                        ZoneRule current = latest.Zones.FirstOrDefault(z => z.Id == zone.Id);
                        if (current == null || !current.Enabled || !ZoneSchedule.Allows(current, DateTime.Now)) return;
                        ZoneExecutionResult result = ZoneExecutor.Execute(current, DiagnosticsLog.WriteEvent);
                        bool completed = result != null && result.Completed;
                        UpdateAutomationEvent(
                            (completed ? "동작 실행 완료: " : "동작 실행 미완료: ") + zone.Name,
                            (completed ? "완료: " : "미완료: ") + zone.Name,
                            null);
                        if (startupOnly)
                        {
                            if (completed)
                            {
                                MarkStartupZoneCompleted(zone);
                                DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 동작 완료 표시: " + zone.Name);
                            }
                            else
                            {
                                DiagnosticsLog.WriteEvent("백그라운드 부팅 초기 동작 미완료, 다음 확인에서 재시도: " + zone.Name);
                            }
                        }
                        if (result != null && result.WifiConnectionVerified)
                        {
                            QueueFollowUpScan("Wi-Fi 연결 확인 후 follow-up scan: " + result.ConnectedSsid);
                        }
                    }
                    catch (Exception ex)
                    {
                        DiagnosticsLog.Write("백그라운드 동작 실행 실패: " + (zone == null ? "" : zone.Name), ex);
                        UpdateAutomationEvent("백그라운드 동작 실행 실패: " + (zone == null ? "" : zone.Name), "실패: " + (zone == null ? "" : zone.Name), null);
                    }
                    finally
                    {
                        _zoneActionInProgress = false;
                        if (startupOnly)
                        {
                            PostStartupRetryCompletionCheck();
                        }
                    }
                });
            }
        }

        private void PostStartupRetryCompletionCheck()
        {
            SendOrPostCallback callback = delegate
            {
                if (!_startupRetryActive)
                {
                    return;
                }

                if (AllStartupRunOnceZonesTriggered())
                {
                    StopStartupRetry("시작 시 1회 실행 대상 위치 처리가 완료되어 부팅 초기 확인을 종료합니다.");
                }
                else if (_startupRetryAttemptsRemaining <= 0 && !_scanInProgress && !_zoneActionInProgress)
                {
                    StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                }
            };

            if (_uiContext != null)
            {
                _uiContext.Post(callback, null);
                return;
            }

            callback(null);
        }

        private void QueueFollowUpScan(string reason)
        {
            DiagnosticsLog.WriteEvent(reason);
            Task.Factory.StartNew(delegate
            {
                for (int attempt = 1; attempt <= 6; attempt++)
                {
                    Thread.Sleep(3000);
                    if (_scanInProgress || _appWatchInProgress || _zoneActionInProgress || _wifiRecoveryInProgress)
                    {
                        DiagnosticsLog.WriteEvent("follow-up scan 대기: 다른 자동 작업 진행 중 (" + attempt + "/6)");
                        continue;
                    }

                    DiagnosticsLog.WriteEvent("follow-up scan 실행: Wi-Fi 연결 이후 조건 재확인");
                    _uiContext.Post(delegate { StartScan(true, false); }, null);
                    return;
                }

                DiagnosticsLog.WriteEvent("follow-up scan 포기: 자동 작업이 계속 진행 중입니다.");
            });
        }

        private void RunDueAppWatchChecks(bool force, string reason)
        {
            if (_config != null && _config.IsAutomationPaused())
            {
                return;
            }

            if (_appWatchInProgress)
            {
                DiagnosticsLog.WriteEvent(reason + " 건너뜀: 다른 자동 작업 진행 중");
                return;
            }

            DateTime now = DateTime.UtcNow;
            List<Tuple<ZoneRule, AppWatchItem>> dueTargets = new List<Tuple<ZoneRule, AppWatchItem>>();
            foreach (ZoneRule zone in _config.Zones.Where(z => z.Enabled && IsZoneActive(z) && ZoneSchedule.Allows(z, DateTime.Now)))
            {
                foreach (AppWatchItem item in zone.GetEnabledAppWatchItems())
                {
                    string key = zone.Id + ":" + item.Id;
                    DateTime last;
                    bool due = force
                        || !_lastAppWatchChecks.TryGetValue(key, out last)
                        || (now - last).TotalMilliseconds >= AppWatchTiming.GetGuardIntervalMilliseconds(item.IntervalValue, item.IntervalUnit);
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
            DiagnosticsLog.WriteThrottled("watch-start", reason + " 시작: " + dueTargets.Count + "개 항목");
            UpdateAutomationEvent(reason + " 시작: " + dueTargets.Count + "개 항목", null, "확인 중: " + dueTargets.Count + "개 항목");
            Task.Factory.StartNew(delegate
            {
                foreach (Tuple<ZoneRule, AppWatchItem> target in dueTargets)
                {
                    if (IsAutomationPausedInSavedConfig())
                    {
                        DiagnosticsLog.WriteEvent(reason + " 건너뜀: 자동화 임시 정지 중");
                        break;
                    }

                    var saved = ConfigStore.Load();
                    var currentZone = saved.Zones.FirstOrDefault(z => z.Id == target.Item1.Id);
                    if (saved.IsAutomationPaused() || currentZone == null || !currentZone.Enabled
                        || !ZoneSchedule.Allows(currentZone, DateTime.Now)
                        || !currentZone.GetEnabledAppWatchItems().Any(i => i.Id == target.Item2.Id)) continue;
                    AppWatchItem item = target.Item2;
                    string processName = AppWatchdog.NormalizeProcessName(item.ProcessName);
                    if (string.IsNullOrWhiteSpace(processName) || string.IsNullOrWhiteSpace(item.LaunchTarget))
                    {
                        DiagnosticsLog.WriteEvent(reason + " 건너뜀: 앱 감시 설정 부족 - " + target.Item1.Name);
                        continue;
                    }

                    try
                    {
                        DateTime checkedAtLocal = DateTime.Now;
                        DateTime nextCheckAtLocal = checkedAtLocal.AddMilliseconds(
                            AppWatchTiming.GetGuardIntervalMilliseconds(item.IntervalValue, item.IntervalUnit));
                        bool requireWindow = item.RequireWindow.GetValueOrDefault(false);
                        AppWatchCheckResult result = AppWatchdog.EnsureRunning(
                            processName,
                            item.LaunchTarget,
                            requireWindow,
                            DiagnosticsLog.WriteEvent);
                        string itemText = BuildAppWatchStatusText(result.Summary, checkedAtLocal, nextCheckAtLocal);
                        string appWatchText = target.Item1.Name + ": " + itemText;
                        if (result.LaunchAttempted || !result.MeetsRequirement) DiagnosticsLog.WriteEvent(reason + " 결과(" + target.Item1.Name + "): " + itemText);
                        else DiagnosticsLog.WriteThrottled("watch:" + target.Item1.Id + ":" + item.Id, reason + " 결과(" + target.Item1.Name + "): " + itemText);
                        UpdateAutomationEvent(
                            reason + " 결과(" + target.Item1.Name + "): " + itemText,
                            null,
                            appWatchText,
                            target.Item1.Id,
                            item.Id,
                            itemText);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticsLog.Write("백그라운드 앱 감시 실패: " + target.Item1.Name, ex);
                        string itemText = "실패: " + target.Item1.Name;
                        UpdateAutomationEvent(
                            "백그라운드 앱 감시 실패: " + target.Item1.Name,
                            null,
                            itemText,
                            target.Item1.Id,
                            item.Id,
                            itemText);
                    }
                }
            }).ContinueWith(delegate(Task task)
            {
                try
                {
                    if (task != null && task.IsFaulted)
                    {
                        DiagnosticsLog.Write("백그라운드 앱 감시 작업 실패", task.Exception == null ? null : task.Exception.GetBaseException());
                    }
                }
                finally
                {
                    _appWatchInProgress = false;
                }
            });
        }

        private bool HasStartupRunOnceZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.RunOnceAtStartup.GetValueOrDefault(true));
        }

        private bool HasZoneConditionScanZones()
        {
            return _config.Zones.Any(z => z.Enabled && (z.MonitoringEnabled.GetValueOrDefault(false) || z.RestoreAudioOnExit || z.ScheduleEnabled || z.GetEnabledAppWatchItems().Any()));
        }

        private bool HasAppWatchZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.GetEnabledAppWatchItems().Any());
        }

        private void ApplyPowerSettings()
        {
            if (_powerStateMonitor == null || _config == null)
            {
                return;
            }

            bool activeAutomation = HasZoneConditionScanZones() || HasAppWatchZones();
            activeAutomation = activeAutomation && !_config.IsAutomationPaused();
            bool preventSleep = _config.PreventSleepWhileAutomationActive && activeAutomation;
            _powerStateMonitor.SetSleepPrevention(
                preventSleep,
                preventSleep ? "백그라운드 자동 감시 활성" : "자동 감시 비활성 또는 설정 꺼짐");
        }

        private static bool IsAutomationPausedInSavedConfig()
        {
            try
            {
                AppConfig config = ConfigStore.Load();
                return config != null && config.IsAutomationPaused();
            }
            catch
            {
                return false;
            }
        }

        private void HandlePowerModeChanged(PowerModes mode)
        {
            if (mode == PowerModes.Suspend)
            {
                UpdateAutomationEvent("절전 진입 감지", null, null);
                return;
            }

            if (mode == PowerModes.Resume)
            {
                _powerResumeDetected = true;
                _uiContext.Post(delegate { _nextWifiPollUtc = DateTime.MinValue; }, null);
                UpdateAutomationEvent("절전 복귀 감지: 다음 앱 감시는 복귀 후 확인으로 기록됩니다.", null, null);
            }
        }

        private bool ConsumePowerResumeDetected()
        {
            if (!_powerResumeDetected)
            {
                return false;
            }

            _powerResumeDetected = false;
            return true;
        }

        private sealed class ZoneMatchResult
        {
            public bool Matches { get; set; }
            public bool CoordinateMatch { get; set; }
            public bool WifiMatch { get; set; }
            public bool StartupWifiConnectionKickoff { get; set; }
            public string Reason { get; set; }
        }
    }
}
