using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void ScanTimerTick(object sender, EventArgs e)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (_appWatchInProgress)
            {
                AppendLog("위치 조건 확인 건너뜀: 앱 감시 확인이 아직 진행 중입니다.");
                return;
            }

            CaptureCurrentZone();
            CaptureGlobalSettings();
            if (HasZoneConditionScanZones())
            {
                StartScan(false, false);
            }
        }

        private void AppWatchTimerTick(object sender, EventArgs e)
        {
            if (IsShuttingDown())
            {
                return;
            }

            CaptureCurrentZone();
            CaptureGlobalSettings();
            if (HasAppWatchZones())
            {
                RunDueAppWatchChecks(false, "앱 감시");
            }
        }

        private void StartStartupRetrySequence()
        {
            _startupRetryAttemptsTotal = 8;
            _startupRetryAttemptsRemaining = _startupRetryAttemptsTotal;
            _startupRetryActive = true;
            _lastScanHadActiveZone = false;
            _startupRetryTimer.Stop();
            AppendLog("Windows 시작 실행: 부팅 초기 네트워크/위치 준비를 확인합니다.");
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

            if (_scanInProgress)
            {
                return;
            }

            if (!HasStartupRunOnceZones())
            {
                StopStartupRetry("시작 시 1회 실행 대상 위치가 없어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (_lastScanHadActiveZone)
            {
                StopStartupRetry("활성 위치가 인식되어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (_startupRetryAttemptsRemaining <= 0)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                return;
            }

            int attemptNumber = _startupRetryAttemptsTotal - _startupRetryAttemptsRemaining + 1;
            _startupRetryAttemptsRemaining--;
            AppendLog("부팅 초기 확인 " + attemptNumber + "/" + _startupRetryAttemptsTotal);
            StartScan(true, true);

            if (_startupRetryAttemptsRemaining > 0)
            {
                _startupRetryTimer.Start();
            }
        }

        private void StopStartupRetry(string reason)
        {
            _startupRetryTimer.Stop();
            _startupRetryActive = false;
            AppendLog(reason);
        }

        private void StartScan(bool forceScan, bool startupOnly)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (_scanInProgress)
            {
                return;
            }

            if (_appWatchInProgress)
            {
                AppendLog("위치 조건 확인 건너뜀: 앱 감시 확인이 아직 진행 중입니다.");
                return;
            }

            _scanInProgress = true;
            bool updateUi = forceScan;
            AppendLog(startupOnly
                ? "Windows 시작 후 한 번 실행 조건을 확인하는 중입니다..."
                : forceScan ? "Wi-Fi와 위치를 확인하는 중입니다..." : "위치 조건을 확인하는 중입니다...");

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(forceScan, _config.Zones.Any(z => z.Enabled && z.UseCoordinates));
            }).ContinueWith(delegate(Task<ScanSnapshot> task)
            {
                try
                {
                    _scanInProgress = false;
                    if (IsShuttingDown())
                    {
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        string message = task.Exception == null ? "알 수 없는 확인 오류입니다." : task.Exception.GetBaseException().Message;
                        AppendLog("위치 확인 실패: " + message);
                        if (_startupRetryActive && _startupRetryAttemptsRemaining <= 0)
                        {
                            StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                        }
                        return;
                    }

                    ProcessScanResult(task.Result, startupOnly, updateUi);
                }
                catch (Exception ex)
                {
                    _scanInProgress = false;
                    DiagnosticsLog.Write("위치 확인 결과 처리 실패", ex);
                    if (!IsShuttingDown())
                    {
                        AppendLog("위치 확인 결과 처리 실패: " + ex.Message);
                    }
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SetSelectedZoneOperating(bool operating)
        {
            ZoneRule conditionSnapshot = null;
            ZoneRule selectedBeforeCapture = GetSelectedZone();
            if (selectedBeforeCapture != null)
            {
                conditionSnapshot = selectedBeforeCapture.Clone();
            }

            CaptureCurrentZone();
            CaptureGlobalSettings();

            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                MessageBox.Show(this, "운영 상태를 바꿀 위치를 먼저 선택하세요.", operating ? "운영하기" : "운영 안함", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (conditionSnapshot != null && string.Equals(conditionSnapshot.Id, selected.Id, StringComparison.OrdinalIgnoreCase))
            {
                RestoreDetectionCondition(selected, conditionSnapshot);
            }

            selected.Enabled = operating;
            if (_zoneEnabledCheck != null)
            {
                _zoneEnabledCheck.Checked = operating;
            }
            CaptureGlobalSettings();

            if (!operating)
            {
                _insideZones.Remove(selected.Id);
            }

            try
            {
                ConfigStore.Save(_config);
                StartupManager.SetEnabled(_startupCheck.Checked, _config.StartMinimized);
                ApplyPowerSettings();
                ResetScanTimer();
                ResetAppWatchTimer();
                BindZoneList(selected.Id);

                if (operating)
                {
                    _zoneTabs.SelectedTab = _activeZonesTab;
                    TrySelectZoneInList(_activeZoneList, selected.Id);
                    AppendLog("운영을 시작했습니다: " + selected.Name);
                }
                else
                {
                    _zoneTabs.SelectedTab = _inactiveZonesTab;
                    TrySelectZoneInList(_inactiveZoneList, selected.Id);
                    AppendLog("운영을 중지했습니다: " + selected.Name);
                }
            }
            catch (Exception ex)
            {
                AppendLog("운영 상태 저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "운영 상태 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestSelectedZoneCondition()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            ZoneRule snapshotZone = selected.Clone();
            AppendLog("테스트해보기 중입니다: " + snapshotZone.Name);

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(true, snapshotZone.UseCoordinates);
            }).ContinueWith(delegate(Task<ScanSnapshot> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 테스트 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("테스트해보기 실패: " + message);
                    MessageBox.Show(this, message, "테스트해보기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ScanContext context = UpdateScanStatus(task.Result, true);
                bool matches = ZoneMatches(snapshotZone, context.VisibleSsids, context.CurrentLocation);
                _activeZonesLabel.Text = matches ? snapshotZone.Name : "없음";
                UpdateSelectedZoneSummary();

                string resultText = matches
                    ? "'" + snapshotZone.Name + "' 위치 조건이 현재 PC 상태와 일치합니다."
                    : "'" + snapshotZone.Name + "' 위치 조건이 현재 PC 상태와 일치하지 않습니다.";

                AppendLog("테스트해보기 결과: " + resultText);
                MessageBox.Show(this, resultText, "테스트해보기", MessageBoxButtons.OK, matches ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void TestSelectedWifiConnection()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            string profile = selected.ConnectProfile == null ? "" : selected.ConnectProfile.Trim();
            string ssid = string.IsNullOrWhiteSpace(selected.ConnectSsid) ? profile : selected.ConnectSsid.Trim();
            if (string.IsNullOrWhiteSpace(profile))
            {
                MessageBox.Show(this, "연결할 Wi-Fi 프로필을 먼저 선택하거나 입력하세요.", "Wi-Fi 연결 테스트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "'" + ssid + "' Wi-Fi 연결을 지금 시도할까요?",
                "Wi-Fi 연결 테스트",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            AppendLog("Wi-Fi 연결 테스트 중입니다: " + ssid);
            Task.Factory.StartNew(delegate
            {
                return WifiActions.Connect(profile, ssid);
            }).ContinueWith(delegate(Task<WifiConnectionResult> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 Wi-Fi 연결 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("Wi-Fi 연결 테스트 실패: " + message);
                    MessageBox.Show(this, message, "Wi-Fi 연결 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AppendLog("Wi-Fi 연결 테스트 결과: " + task.Result.Summary);
                MessageBox.Show(this, task.Result.Summary, "Wi-Fi 연결 테스트 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private ScanSnapshot CreateScanSnapshot(bool forceScan, bool requestLocation)
        {
            ScanSnapshot helperSnapshot = RunScanHelper(forceScan, requestLocation);
            if (helperSnapshot != null)
            {
                return helperSnapshot;
            }

            return new ScanSnapshot
            {
                Networks = new List<WifiNetwork>(),
                WifiError = "탐지 헬퍼 실행에 실패했습니다.",
                LocationResult = requestLocation
                    ? new LocationReadResult { WasRequested = true, Error = "탐지 헬퍼 실행에 실패했습니다." }
                    : LocationReadResult.NotRequested()
            };
        }

        private ScanSnapshot RunScanHelper(bool forceScan, bool requestLocation)
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
                    DiagnosticsLog.WriteEvent("탐지 헬퍼 시간 초과: location=" + requestLocation);
                    return null;
                }

                if (!File.Exists(outputPath))
                {
                    DiagnosticsLog.WriteEvent("탐지 헬퍼 결과 없음: exit=" + command.ExitCode + " / error=" + FirstLogLine(command.Error));
                    return null;
                }

                string json = File.ReadAllText(outputPath, Encoding.UTF8);
                ScanSnapshot snapshot = new JavaScriptSerializer().Deserialize<ScanSnapshot>(json);
                if (snapshot == null)
                {
                    DiagnosticsLog.WriteEvent("탐지 헬퍼 결과 해석 실패: 빈 결과");
                    return null;
                }

                if (command.ExitCode != 0)
                {
                    DiagnosticsLog.WriteEvent("탐지 헬퍼 비정상 종료: exit=" + command.ExitCode + " / wifiError=" + FirstLogLine(snapshot.WifiError));
                }

                return snapshot;
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("탐지 헬퍼 처리 실패", ex);
                return null;
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

        private ScanContext UpdateScanStatus(ScanSnapshot snapshot, bool updateUi)
        {
            List<WifiNetwork> networks = snapshot.Networks ?? new List<WifiNetwork>();
            List<WifiNetwork> ordered = networks
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .OrderByDescending(n => n.SignalQuality)
                .ThenBy(n => n.Ssid)
                .ToList();

            if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
            {
                AppendLog("Wi-Fi 확인 실패: " + snapshot.WifiError);
            }

            if (updateUi && ordered.Count == 0)
            {
                _visibleNetworksLabel.Text = "보이는 Wi-Fi가 없습니다.";
            }
            else if (updateUi)
            {
                _visibleNetworksLabel.Text = string.Join(", ", ordered.Take(12).Select(n => n.Ssid + " " + n.SignalQuality + "%").ToArray());
            }

            _lastVisibleNetworks = ordered;
            if (updateUi)
            {
                ZoneRule selectedZone = GetSelectedZone();
                IEnumerable<string> selectedWifi = selectedZone == null ? GetSelectedWifiSsids() : selectedZone.NearbySsids;
                RenderWifiChoiceButtons(selectedWifi, _lastVisibleNetworks);
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
            }

            HashSet<string> visibleSsids = new HashSet<string>(ordered.Select(n => n.Ssid), StringComparer.OrdinalIgnoreCase);
            LocationInfo currentLocation = null;
            if (snapshot.LocationResult != null && snapshot.LocationResult.HasLocation)
            {
                currentLocation = snapshot.LocationResult.Location;
                if (updateUi)
                {
                    _currentLocationLabel.Text = FormatLocation(currentLocation);
                }
            }
            else if (snapshot.LocationResult != null && snapshot.LocationResult.WasRequested)
            {
                if (updateUi)
                {
                    _currentLocationLabel.Text = "사용 불가: " + snapshot.LocationResult.Error;
                }
                AppendLog("위치 사용 불가: " + snapshot.LocationResult.Error);
            }
            else if (updateUi)
            {
                _currentLocationLabel.Text = "좌표 감지 위치가 켜져 있지 않습니다.";
            }

            return new ScanContext
            {
                VisibleSsids = visibleSsids,
                CurrentLocation = currentLocation,
                VisibleNetworks = ordered
            };
        }

        private void ProcessScanResult(ScanSnapshot snapshot, bool startupOnly, bool updateUi)
        {
            ScanContext context = UpdateScanStatus(snapshot, updateUi);
            HashSet<string> visibleSsids = context.VisibleSsids;
            LocationInfo currentLocation = context.CurrentLocation;

            List<string> activeZoneNames = new List<string>();
            bool zoneStateChanged = false;
            bool hadEligibleActiveZone = false;
            bool appWatchZoneBecameActive = false;
            bool preserveActiveZones = ScanReliability.HasTransientDetectionError(snapshot);

            foreach (ZoneRule zone in _config.Zones)
            {
                zone.Normalize();
                bool eligible = IsZoneEligibleForScan(zone, startupOnly);
                bool wasInside = _insideZones.ContainsKey(zone.Id) && _insideZones[zone.Id];
                bool near = zone.Enabled && (ZoneMatches(zone, visibleSsids, currentLocation) || (preserveActiveZones && wasInside));
                bool hasEnabledAppWatch = zone.GetEnabledAppWatchItems().Any();

                if (near)
                {
                    activeZoneNames.Add(zone.Name);
                }

                if (near && eligible)
                {
                    hadEligibleActiveZone = true;
                }

                if (near && !wasInside)
                {
                    _insideZones[zone.Id] = true;
                    zoneStateChanged = true;
                    if (eligible)
                    {
                        TriggerZone(zone.Clone(), startupOnly ? "Windows 시작 후 한 번 실행" : "조건 진입 시 실행");
                    }
                    if (hasEnabledAppWatch)
                    {
                        appWatchZoneBecameActive = true;
                    }
                }
                else if (!near && wasInside)
                {
                    _insideZones[zone.Id] = false;
                    zoneStateChanged = true;
                    AppendLog("위치에서 벗어났습니다: " + zone.Name);
                }
            }

            if (updateUi)
            {
                _activeZonesLabel.Text = activeZoneNames.Count == 0
                    ? "없음"
                    : string.Join(", ", activeZoneNames.ToArray());
            }
            if (startupOnly)
            {
                _lastScanHadActiveZone = hadEligibleActiveZone;
            }

            if (updateUi)
            {
                if (zoneStateChanged)
                {
                    BindZoneList(_currentZoneId);
                }
                else
                {
                    InvalidateZoneLists();
                }

                UpdateSelectedZoneSummary();
                RefreshSelectedAppWatchStatusLabel();
            }

            if (appWatchZoneBecameActive)
            {
                RunDueAppWatchChecks(true, "앱 감시 시작 확인");
            }

            if (startupOnly && _startupRetryActive && _lastScanHadActiveZone)
            {
                StopStartupRetry("활성 위치가 인식되어 부팅 초기 확인을 종료합니다.");
            }
            else if (startupOnly && _startupRetryActive && _startupRetryAttemptsRemaining <= 0 && !_scanInProgress)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
            }
        }

        private bool ZoneMatches(ZoneRule zone, HashSet<string> visibleSsids, LocationInfo currentLocation)
        {
            bool coordinateMatch = false;
            if (zone.UseCoordinates)
            {
                coordinateMatch = currentLocation != null
                    && GeoMath.DistanceMeters(
                        currentLocation.Latitude,
                        currentLocation.Longitude,
                        zone.Latitude,
                        zone.Longitude) <= zone.RadiusMeters;
            }

            List<string> wanted = zone.NearbySsids
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool wifiMatch = false;
            if (zone.UseWifiCondition.GetValueOrDefault(false) && wanted.Count > 0)
            {
                wifiMatch = zone.RequireAllSsids
                    ? wanted.All(visibleSsids.Contains)
                    : wanted.Any(visibleSsids.Contains);
            }

            return coordinateMatch || wifiMatch;
        }

        private void TriggerZone(ZoneRule zone, string reason)
        {
            AppendLog(reason + ": " + zone.Name);
            ShowTrayNotification("위치 자동 실행", zone.Name + " 동작을 실행했습니다.");

            Task.Factory.StartNew(delegate
            {
                try
                {
                    ZoneExecutor.Execute(zone, SafeLog);
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Write("동작 실행 실패: " + (zone == null ? "" : zone.Name), ex);
                    SafeLog("동작 실행 실패: " + ex.Message);
                }
            });
        }

        private void SafeLog(string message)
        {
            if (IsShuttingDown())
            {
                DiagnosticsLog.WriteEvent(message);
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                }
                catch
                {
                }
            }
            else
            {
                try
                {
                    AppendLog(message);
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Write("로그 기록 실패", ex);
                }
            }
        }

        private void AppendLog(string message)
        {
            DiagnosticsLog.WriteEvent(message);
            if (IsShuttingDown())
            {
                return;
            }

            UpdateLogDisplayFromMessage(message);
        }

        private static List<string> SplitLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static string JoinLines(IEnumerable<string> values)
        {
            if (values == null)
            {
                return "";
            }

            return string.Join(Environment.NewLine, values.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
        }

        private static string QuoteCommandArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string FirstLogLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            string line = text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => s.Length > 0) ?? "";
            return line.Length > 180 ? line.Substring(0, 180) : line;
        }

        private static double ParseCoordinate(string text, double fallback)
        {
            double value;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        private static string FormatCoordinate(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatLocation(LocationInfo location)
        {
            if (location == null)
            {
                return "Unavailable.";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.######}, {1:0.######} (accuracy {2:0} m)",
                location.Latitude,
                location.Longitude,
                location.AccuracyMeters);
        }

        private static bool IsKnownAudioAction(string value)
        {
            return string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Mute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Unmute", StringComparison.OrdinalIgnoreCase);
        }

        private void SetAudioActionSelection(string value)
        {
            if (string.Equals(value, "Mute", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "음소거", StringComparison.OrdinalIgnoreCase))
            {
                _audioActionCombo.SelectedItem = "음소거";
            }
            else if (string.Equals(value, "Unmute", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "음소거 해제", StringComparison.OrdinalIgnoreCase))
            {
                _audioActionCombo.SelectedItem = "음소거 해제";
            }
            else
            {
                _audioActionCombo.SelectedItem = "안 함";
            }
        }

        private string ReadAudioActionSelection()
        {
            string selected = Convert.ToString(_audioActionCombo.SelectedItem);
            if (string.Equals(selected, "음소거", StringComparison.OrdinalIgnoreCase))
            {
                return "Mute";
            }

            if (string.Equals(selected, "음소거 해제", StringComparison.OrdinalIgnoreCase))
            {
                return "Unmute";
            }

            return "None";
        }

        private void ShowMainWindow()
        {
            if (IsShuttingDown())
            {
                return;
            }

            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (!_automationEnabled)
            {
                AppendLog("설정 화면 모드: 자동 실행은 백그라운드 프로세스에서 처리합니다.");
                return;
            }

            if (_startMinimizedRequested)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = true;
            }

            if (_startedFromWindowsStartup && HasStartupRunOnceZones())
            {
                BeginInvoke(new Action(delegate
                {
                    if (!IsShuttingDown())
                    {
                        StartStartupRetrySequence();
                    }
                }));
            }
            else if (HasZoneConditionScanZones())
            {
                BeginInvoke(new Action(delegate
                {
                    if (!IsShuttingDown())
                    {
                        StartScan(false, false);
                    }
                }));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            DiagnosticsLog.WriteEvent("폼 종료 요청: reason=" + e.CloseReason + " / exiting=" + _isExiting);
            DiagnosticsLog.WriteEvent("폼 종료 진단: " + WindowCloseDiagnostics.Describe(this));
            if (!_automationEnabled
                && !_allowSettingsScreenClose
                && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                DiagnosticsLog.WriteEvent("설정 화면 닫기 요청 전환: 종료하지 않고 트레이로 숨깁니다.");
                EnsureTrayIcon();
                Hide();
                ShowInTaskbar = false;
                return;
            }

            _isExiting = true;
            _appWatchRunVersion++;
            _scanTimer.Stop();
            _startupRetryTimer.Stop();
            _appWatchTimer.Stop();
            _logRefreshTimer.Stop();
            if (_powerStateMonitor != null)
            {
                _powerStateMonitor.Dispose();
            }
            DisposeTrayIcon();

            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            DiagnosticsLog.WriteEvent("폼 종료 완료: reason=" + e.CloseReason);
            base.OnFormClosed(e);
        }

        private bool IsShuttingDown()
        {
            return _isExiting || IsDisposed || Disposing;
        }

        private void HandlePowerModeChanged(PowerModes mode)
        {
            if (mode == PowerModes.Suspend)
            {
                DiagnosticsLog.WriteEvent("절전 진입 감지: 실행 중인 작업은 Windows 복귀 후 재확인됩니다.");
                return;
            }

            if (mode == PowerModes.Resume)
            {
                DiagnosticsLog.WriteEvent("절전 복귀 감지: 감시 상태를 다음 주기에서 재확인합니다.");
            }
        }

        private void ApplyPowerSettings()
        {
            if (_powerStateMonitor == null)
            {
                return;
            }

            bool activeAutomation = HasZoneConditionScanZones() || HasAppWatchZones();
            bool preventSleep = _config.PreventSleepWhileAutomationActive && activeAutomation;
            _powerStateMonitor.SetSleepPrevention(
                preventSleep,
                preventSleep ? "자동 감시 활성" : "자동 감시 비활성 또는 설정 꺼짐");
        }

        private sealed class ZoneListItem
        {
            public ZoneRule Zone { get; private set; }
            private readonly MainForm _owner;

            public ZoneListItem(ZoneRule zone, MainForm owner)
            {
                Zone = zone;
                _owner = owner;
            }

            public string MetadataText
            {
                get
                {
                    string state = Zone.Enabled ? "운영 중" : "미운영";
                    string match = _owner.IsZoneActive(Zone) ? "조건 일치" : "대기";
                    string mode = _owner.BuildZoneModeSummary(Zone);
                    List<string> schedules = new List<string>();
                    if (Zone.RunOnceAtStartup.GetValueOrDefault(true))
                    {
                        schedules.Add("시작 1회");
                    }
                    if (Zone.MonitoringEnabled.GetValueOrDefault(false))
                    {
                        schedules.Add("조건 진입 시 실행 · 조건 확인 " + Math.Max(5, Zone.ScanIntervalSeconds) + "초");
                    }
                    string schedule = schedules.Count == 0 ? "자동 실행 없음" : string.Join("/", schedules.ToArray());
                    return state + " · " + match + " · " + mode + " · " + schedule;
                }
            }

            public override string ToString()
            {
                string state = Zone.Enabled ? "운영 중" : "미운영";
                string match = _owner.IsZoneActive(Zone) ? "조건 일치" : "대기";
                return Zone.Name + " · " + state + " · " + match;
            }
        }
    }
}
