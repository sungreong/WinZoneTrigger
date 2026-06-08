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
        private void ConfigureTray()
        {
            DiagnosticsLog.WriteEvent("트레이 비활성화: 안정성을 위해 작업표시줄 최소화 방식으로 실행합니다.");
        }

        private void BindConfigToControls()
        {
            _loadingSelection = true;
            _startupCheck.Checked = StartupManager.IsEnabled();
            _startMinimizedCheck.Checked = _config.StartMinimized;
            _loadingSelection = false;

            string selectedId = _config.Zones.Count > 0 ? _config.Zones[0].Id : null;
            BindZoneList(selectedId);
            AppendLog("설정을 불러왔습니다: " + ConfigStore.ConfigPath);
        }

        private void BindZoneList(string selectedId)
        {
            _loadingSelection = true;

            ListBox activeBefore = GetCurrentZoneList();
            _allZoneList.Items.Clear();
            _activeZoneList.Items.Clear();
            _inactiveZoneList.Items.Clear();

            int activeCount = 0;
            int inactiveCount = 0;

            foreach (ZoneRule zone in _config.Zones)
            {
                ZoneListItem item = new ZoneListItem(zone, this);
                _allZoneList.Items.Add(item);

                if (zone.Enabled)
                {
                    _activeZoneList.Items.Add(new ZoneListItem(zone, this));
                    activeCount++;
                }
                else
                {
                    _inactiveZoneList.Items.Add(new ZoneListItem(zone, this));
                    inactiveCount++;
                }
            }

            _allZonesTab.Text = "전체 " + _config.Zones.Count;
            _activeZonesTab.Text = "운영 중 " + activeCount;
            _inactiveZonesTab.Text = "미운영 " + inactiveCount;

            if (!string.IsNullOrEmpty(selectedId))
            {
                SelectZoneInAvailableList(selectedId, activeBefore);
            }

            if (GetSelectedZoneFromLists() == null && _allZoneList.Items.Count > 0)
            {
                _allZoneList.SelectedIndex = 0;
            }

            _loadingSelection = false;
            LoadSelectedZoneToControls();
        }

        private void ZoneListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingSelection)
            {
                return;
            }

            CaptureCurrentZone();
            ListBox source = sender as ListBox;
            ZoneListItem selected = source == null ? null : source.SelectedItem as ZoneListItem;
            if (selected != null)
            {
                _currentZoneId = selected.Zone.Id;
                SyncZoneSelectionAcrossLists(source, _currentZoneId);
            }
            LoadSelectedZoneToControls();
        }

        private ListBox GetCurrentZoneList()
        {
            if (_zoneTabs == null || _zoneTabs.SelectedTab == null)
            {
                return _allZoneList;
            }

            if (_zoneTabs.SelectedTab == _activeZonesTab)
            {
                return _activeZoneList;
            }

            if (_zoneTabs.SelectedTab == _inactiveZonesTab)
            {
                return _inactiveZoneList;
            }

            return _allZoneList;
        }

        private void SelectZoneInAvailableList(string zoneId, ListBox preferredList)
        {
            if (TrySelectZoneInList(preferredList, zoneId))
            {
                return;
            }

            if (TrySelectZoneInList(GetCurrentZoneList(), zoneId))
            {
                return;
            }

            ZoneRule zone = FindZone(zoneId);
            if (zone != null && zone.Enabled && TrySelectZoneInList(_activeZoneList, zoneId))
            {
                _zoneTabs.SelectedTab = _activeZonesTab;
                return;
            }

            if (TrySelectZoneInList(_inactiveZoneList, zoneId))
            {
                _zoneTabs.SelectedTab = _inactiveZonesTab;
                return;
            }

            TrySelectZoneInList(_allZoneList, zoneId);
        }

        private bool TrySelectZoneInList(ListBox list, string zoneId)
        {
            if (list == null || string.IsNullOrEmpty(zoneId))
            {
                return false;
            }

            for (int i = 0; i < list.Items.Count; i++)
            {
                ZoneListItem item = list.Items[i] as ZoneListItem;
                if (item != null && string.Equals(item.Zone.Id, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    list.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void SyncZoneSelectionAcrossLists(ListBox source, string zoneId)
        {
            _loadingSelection = true;
            ClearSelectionUnless(_allZoneList, source);
            ClearSelectionUnless(_activeZoneList, source);
            ClearSelectionUnless(_inactiveZoneList, source);
            TrySelectZoneInList(_allZoneList == source ? null : _allZoneList, zoneId);
            TrySelectZoneInList(_activeZoneList == source ? null : _activeZoneList, zoneId);
            TrySelectZoneInList(_inactiveZoneList == source ? null : _inactiveZoneList, zoneId);
            _loadingSelection = false;
        }

        private void ClearSelectionUnless(ListBox list, ListBox source)
        {
            if (list != null && list != source)
            {
                list.ClearSelected();
            }
        }

        private void SetDetailAreaEnabled(bool enabled)
        {
            SetChildControlsEnabled(_conditionTable, enabled);
            SetChildControlsEnabled(_actionTable, enabled);
            SetChildControlsEnabled(_appWatchTable, enabled);
        }

        private static void SetChildControlsEnabled(Control root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            foreach (Control child in root.Controls)
            {
                child.Enabled = enabled;
            }
        }

        private void LoadSelectedZoneToControls()
        {
            ZoneRule zone = GetSelectedZone();
            _loadingSelection = true;

            if (zone == null)
            {
                _currentZoneId = null;
                SetDetailAreaEnabled(false);
                _zoneEnabledCheck.Checked = false;
                _runOnceStartupCheck.Checked = false;
                _monitoringCheck.Checked = false;
                _intervalInput.Value = 30;
                _zoneNameText.Text = "";
                _useCoordinatesCheck.Checked = false;
                _latitudeText.Text = "";
                _longitudeText.Text = "";
                _radiusInput.Value = 200;
                RenderWifiChoiceButtons(new List<string>(), _lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = false;
                _connectWifiCheck.Checked = false;
                _connectProfileText.Text = "";
                _connectSsidText.Text = "";
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                SetAudioActionSelection("None");
                SetChromeUrls(new List<string>());
                SetAppLaunches(new List<string>());
                _selectedAppWatchItemId = null;
                RenderAppWatchItems();
                _commandsText.Text = "";
            }
            else
            {
                zone.Normalize();
                _currentZoneId = zone.Id;
                SetDetailAreaEnabled(true);
                _zoneEnabledCheck.Checked = zone.Enabled;
                _runOnceStartupCheck.Checked = zone.RunOnceAtStartup.GetValueOrDefault(true);
                _monitoringCheck.Checked = zone.MonitoringEnabled.GetValueOrDefault(false);
                _intervalInput.Value = Math.Max(_intervalInput.Minimum, Math.Min(_intervalInput.Maximum, zone.ScanIntervalSeconds));
                _zoneNameText.Text = zone.Name;
                _useCoordinatesCheck.Checked = zone.UseCoordinates;
                _latitudeText.Text = FormatCoordinate(zone.Latitude);
                _longitudeText.Text = FormatCoordinate(zone.Longitude);
                _radiusInput.Value = Math.Max(_radiusInput.Minimum, Math.Min(_radiusInput.Maximum, zone.RadiusMeters));
                RenderWifiChoiceButtons(zone.NearbySsids, _lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = zone.RequireAllSsids;
                _connectWifiCheck.Checked = zone.ConnectWifiEnabled.GetValueOrDefault(false);
                _connectProfileText.Text = zone.ConnectProfile;
                _connectSsidText.Text = zone.ConnectSsid;
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                SetAudioActionSelection(zone.AudioAction);
                SetChromeUrls(zone.ChromeUrls);
                SetAppLaunches(zone.AppLaunches);
                RenderAppWatchItems();
                _commandsText.Text = JoinLines(zone.Commands);
            }

            _loadingSelection = false;
            SetCoordinateInputsEnabled();
            UpdateSelectedZoneSummary();
        }

        private void CaptureCurrentZone()
        {
            if (_loadingSelection || string.IsNullOrEmpty(_currentZoneId))
            {
                return;
            }

            ZoneRule zone = FindZone(_currentZoneId);
            if (zone == null)
            {
                return;
            }

            zone.Enabled = _zoneEnabledCheck.Checked;
            zone.RunOnceAtStartup = _runOnceStartupCheck.Checked;
            zone.MonitoringEnabled = _monitoringCheck.Checked;
            zone.ScanIntervalSeconds = Convert.ToInt32(_intervalInput.Value);
            zone.Name = string.IsNullOrWhiteSpace(_zoneNameText.Text) ? "이름 없는 위치" : _zoneNameText.Text.Trim();
            zone.UseCoordinates = _useCoordinatesCheck.Checked;
            zone.Latitude = ParseCoordinate(_latitudeText.Text, zone.Latitude);
            zone.Longitude = ParseCoordinate(_longitudeText.Text, zone.Longitude);
            zone.RadiusMeters = Convert.ToInt32(_radiusInput.Value);
            zone.NearbySsids = GetSelectedWifiSsids();
            zone.RequireAllSsids = _requireAllSsidsCheck.Checked;
            zone.ConnectWifiEnabled = _connectWifiCheck.Checked;
            zone.ConnectProfile = _connectProfileText.Text.Trim();
            zone.ConnectSsid = _connectSsidText.Text.Trim();
            zone.AudioAction = ReadAudioActionSelection();
            if (string.IsNullOrWhiteSpace(zone.AudioAction))
            {
                zone.AudioAction = "None";
            }
            zone.ChromeUrls = GetChromeUrls();
            zone.AppLaunches = GetAppLaunches();
            CaptureSelectedAppWatchItemValues(zone);
            zone.Commands = SplitLines(_commandsText.Text);
            UpdateSelectedZoneSummary();
        }

        private void CaptureGlobalSettings()
        {
            if (_loadingSelection)
            {
                return;
            }

            _config.MonitoringEnabled = HasContinuousMonitoringZones();
            _config.RunOnceAtStartup = HasStartupRunOnceZones();
            _config.ScanIntervalSeconds = GetShortestContinuousScanIntervalSeconds();
            _config.StartMinimized = _startMinimizedCheck.Checked;
            _config.AppWatchEnabled = HasAppWatchZones();
            AppWatchItem firstAppWatchItem = null;
            foreach (ZoneRule zone in _config.Zones.Where(z => z.Enabled))
            {
                zone.Normalize();
                firstAppWatchItem = zone.GetEnabledAppWatchItems().FirstOrDefault();
                if (firstAppWatchItem != null)
                {
                    break;
                }
            }
            _config.AppWatchLaunchTarget = firstAppWatchItem == null ? "" : firstAppWatchItem.LaunchTarget;
            _config.AppWatchProcessName = firstAppWatchItem == null ? "" : firstAppWatchItem.ProcessName;
            _config.AppWatchRequireWindow = firstAppWatchItem == null ? false : firstAppWatchItem.RequireWindow;
            _config.AppWatchIntervalValue = firstAppWatchItem == null ? 5 : firstAppWatchItem.IntervalValue;
            _config.AppWatchIntervalUnit = firstAppWatchItem == null ? "Minutes" : firstAppWatchItem.IntervalUnit;
        }

        private static void RestoreDetectionCondition(ZoneRule target, ZoneRule source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.UseCoordinates = source.UseCoordinates;
            target.Latitude = source.Latitude;
            target.Longitude = source.Longitude;
            target.RadiusMeters = source.RadiusMeters;
            target.NearbySsids = source.NearbySsids == null ? new List<string>() : new List<string>(source.NearbySsids);
            target.RequireAllSsids = source.RequireAllSsids;
        }

        private void SetCoordinateInputsEnabled()
        {
            bool enabled = _useCoordinatesCheck != null && _useCoordinatesCheck.Checked;
            if (_latitudeText != null)
            {
                _latitudeText.Enabled = enabled;
                _latitudeText.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _latitudeText.ForeColor = enabled ? UiText : UiTextMuted;
            }
            if (_longitudeText != null)
            {
                _longitudeText.Enabled = enabled;
                _longitudeText.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _longitudeText.ForeColor = enabled ? UiText : UiTextMuted;
            }
            if (_radiusInput != null)
            {
                _radiusInput.Enabled = enabled;
                _radiusInput.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _radiusInput.ForeColor = enabled ? UiText : UiTextMuted;
            }
        }

        private void FillSelectedZoneFromCurrentLocation()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            AppendLog("현재 Windows 위치를 읽는 중입니다...");
            Task.Factory.StartNew(delegate
            {
                return LocationLocator.GetCurrentLocation();
            }).ContinueWith(delegate(Task<LocationReadResult> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 위치 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("위치 읽기 실패: " + message);
                    MessageBox.Show(this, message, "위치 읽기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LocationReadResult result = task.Result;
                if (!result.HasLocation)
                {
                    string message = string.IsNullOrWhiteSpace(result.Error) ? "Windows 위치를 사용할 수 없습니다." : result.Error;
                    AppendLog("위치 읽기 실패: " + message);
                    MessageBox.Show(this, message, "위치 사용 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _useCoordinatesCheck.Checked = true;
                _latitudeText.Text = FormatCoordinate(result.Location.Latitude);
                _longitudeText.Text = FormatCoordinate(result.Location.Longitude);
                if (_radiusInput.Value < 100)
                {
                    _radiusInput.Value = 200;
                }

                CaptureCurrentZone();
                _currentLocationLabel.Text = FormatLocation(result.Location);
                AppendLog("현재 좌표를 위치에 적용했습니다: " + selected.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void CreateZoneFromCurrentLocation()
        {
            CaptureCurrentZone();
            ZoneRule zone = ZoneRule.CreateDefault("현재 위치");
            zone.NearbySsids.Clear();
            _config.Zones.Add(zone);
            BindZoneList(zone.Id);
            AppendLog("현재 좌표로 새 위치를 등록하는 중입니다...");

            Task.Factory.StartNew(delegate
            {
                return LocationLocator.GetCurrentLocation();
            }).ContinueWith(delegate(Task<LocationReadResult> task)
            {
                ZoneRule created = FindZone(zone.Id);
                if (created == null)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 위치 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("현재 위치 등록 실패: " + message);
                    return;
                }

                LocationReadResult result = task.Result;
                if (!result.HasLocation)
                {
                    string message = string.IsNullOrWhiteSpace(result.Error) ? "Windows 위치를 사용할 수 없습니다." : result.Error;
                    AppendLog("현재 위치 등록 실패: " + message);
                    return;
                }

                created.UseCoordinates = true;
                created.Latitude = result.Location.Latitude;
                created.Longitude = result.Location.Longitude;
                created.RadiusMeters = Math.Max(200, created.RadiusMeters);
                BindZoneList(created.Id);
                _currentLocationLabel.Text = FormatLocation(result.Location);
                SaveFromUi();
                AppendLog("현재 위치가 등록되었습니다: " + created.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void FillSelectedZoneFromVisibleNetworks()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            AppendLog("현재 보이는 Wi-Fi를 확인하는 중입니다...");
            Task.Factory.StartNew(delegate
            {
                return WifiLocator.GetVisibleNetworks(true);
            }).ContinueWith(delegate(Task<List<WifiNetwork>> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 Wi-Fi 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("Wi-Fi 확인 실패: " + message);
                    MessageBox.Show(this, message, "Wi-Fi 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<WifiNetwork> networks = task.Result
                    .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                    .OrderByDescending(n => n.SignalQuality)
                    .ThenBy(n => n.Ssid)
                    .Take(5)
                    .ToList();

                if (networks.Count == 0)
                {
                    AppendLog("보이는 Wi-Fi가 없습니다.");
                    MessageBox.Show(this, "현재 보이는 Wi-Fi가 없습니다.", "Wi-Fi 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _useCoordinatesCheck.Checked = false;
                _lastVisibleNetworks = networks;
                RenderWifiChoiceButtons(networks.Select(n => n.Ssid), _lastVisibleNetworks);
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = false;
                if (_connectWifiCheck.Checked && string.IsNullOrWhiteSpace(_connectProfileText.Text))
                {
                    SetConnectWifiTarget(networks[0].Ssid);
                }
                _visibleNetworksLabel.Text = string.Join(", ", networks.Select(n => n.Ssid + " " + n.SignalQuality + "%").ToArray());
                CaptureCurrentZone();
                AppendLog("현재 보이는 Wi-Fi를 위치 감지 조건에 적용했습니다: " + selected.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SaveFromUi()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();

            if (HasStartupRunOnceZones() && !_startupCheck.Checked)
            {
                _startupCheck.Checked = true;
            }

            try
            {
                ConfigStore.Save(_config);
                StartupManager.SetEnabled(_startupCheck.Checked, _config.StartMinimized);
                ApplyPowerSettings();
                ResetScanTimer();
                ResetAppWatchTimer();
                BindZoneList(_currentZoneId);
                AppendLog("저장했습니다.");
            }
            catch (Exception ex)
            {
                AppendLog("저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelectedZone()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "'" + selected.Name + "' 위치를 삭제할까요?", "위치 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            _config.Zones.Remove(selected);
            _insideZones.Remove(selected.Id);
            _currentZoneId = null;
            BindZoneList(_config.Zones.Count > 0 ? _config.Zones[0].Id : null);
            AppendLog("위치를 삭제했습니다: " + selected.Name);
        }

        private void DuplicateSelectedZone()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();

            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                MessageBox.Show(this, "복제할 위치를 먼저 선택하세요.", "위치 복제", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ZoneRule copy = selected.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = BuildDuplicateZoneName(selected.Name);
            copy.Enabled = false;
            if (copy.AppWatchItems != null)
            {
                foreach (AppWatchItem item in copy.AppWatchItems)
                {
                    if (item != null)
                    {
                        item.Enabled = false;
                    }
                }
            }
            copy.Normalize();

            _config.Zones.Add(copy);
            CaptureGlobalSettings();
            ResetScanTimer();
            BindZoneList(copy.Id);
            _zoneTabs.SelectedTab = _inactiveZonesTab;
            TrySelectZoneInList(_inactiveZoneList, copy.Id);
            AppendLog("위치를 복제했습니다: " + selected.Name + " -> " + copy.Name + " (미운영)");
        }

        private string BuildDuplicateZoneName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? "이름 없는 위치" : sourceName.Trim();
            string first = baseName + " 복사본";
            if (!_config.Zones.Any(z => string.Equals(z.Name, first, StringComparison.OrdinalIgnoreCase)))
            {
                return first;
            }

            for (int i = 2; i < 1000; i++)
            {
                string candidate = first + " " + i;
                if (!_config.Zones.Any(z => string.Equals(z.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return first + " " + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private ZoneRule GetSelectedZone()
        {
            ZoneRule selected = GetSelectedZoneFromLists();
            if (selected != null)
            {
                return selected;
            }

            return string.IsNullOrWhiteSpace(_currentZoneId) ? null : FindZone(_currentZoneId);
        }

        private ZoneRule GetSelectedZoneFromLists()
        {
            ZoneListItem currentTabItem = GetCurrentZoneList() == null ? null : GetCurrentZoneList().SelectedItem as ZoneListItem;
            if (currentTabItem != null)
            {
                return currentTabItem.Zone;
            }

            ZoneListItem allItem = _allZoneList == null ? null : _allZoneList.SelectedItem as ZoneListItem;
            if (allItem != null)
            {
                return allItem.Zone;
            }

            ZoneListItem activeItem = _activeZoneList == null ? null : _activeZoneList.SelectedItem as ZoneListItem;
            if (activeItem != null)
            {
                return activeItem.Zone;
            }

            ZoneListItem inactiveItem = _inactiveZoneList == null ? null : _inactiveZoneList.SelectedItem as ZoneListItem;
            return inactiveItem == null ? null : inactiveItem.Zone;
        }

        private ZoneRule FindZone(string zoneId)
        {
            return _config.Zones.FirstOrDefault(z => string.Equals(z.Id, zoneId, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsZoneActive(ZoneRule zone)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
            {
                return false;
            }

            bool active;
            return _insideZones.TryGetValue(zone.Id, out active) && active;
        }

        private bool HasContinuousMonitoringZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.MonitoringEnabled.GetValueOrDefault(false));
        }

        private bool HasStartupRunOnceZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.RunOnceAtStartup.GetValueOrDefault(true));
        }

        private bool HasAppWatchZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.GetEnabledAppWatchItems().Any());
        }

        private bool HasZoneConditionScanZones()
        {
            return _config.Zones.Any(IsZoneConditionScanEnabled);
        }

        private static bool IsZoneConditionScanEnabled(ZoneRule zone)
        {
            return zone != null
                && zone.Enabled
                && (zone.MonitoringEnabled.GetValueOrDefault(false) || zone.GetEnabledAppWatchItems().Any());
        }

        private int GetShortestContinuousScanIntervalSeconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled && z.MonitoringEnabled.GetValueOrDefault(false))
                .Select(z => z.ScanIntervalSeconds < 5 ? 30 : z.ScanIntervalSeconds)
                .ToList();

            return intervals.Count == 0 ? 30 : intervals.Min();
        }

        private int GetShortestConditionScanIntervalSeconds()
        {
            List<int> intervals = _config.Zones
                .Where(IsZoneConditionScanEnabled)
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

        private static bool IsZoneEligibleForScan(ZoneRule zone, bool startupOnly)
        {
            if (zone == null || !zone.Enabled)
            {
                return false;
            }

            return startupOnly
                ? zone.RunOnceAtStartup.GetValueOrDefault(true)
                : zone.MonitoringEnabled.GetValueOrDefault(false);
        }

        private void InvalidateZoneLists()
        {
            bool wasLoading = _loadingSelection;
            _loadingSelection = true;
            try
            {
                RefreshZoneListItems(_allZoneList);
                RefreshZoneListItems(_activeZoneList);
                RefreshZoneListItems(_inactiveZoneList);
            }
            finally
            {
                _loadingSelection = wasLoading;
            }

            if (_allZoneList != null)
            {
                _allZoneList.Invalidate();
            }
            if (_activeZoneList != null)
            {
                _activeZoneList.Invalidate();
            }
            if (_inactiveZoneList != null)
            {
                _inactiveZoneList.Invalidate();
            }
        }

        private void RefreshZoneListItems(ListBox list)
        {
            if (list == null || list.Items.Count == 0)
            {
                return;
            }

            int selectedIndex = list.SelectedIndex;
            list.BeginUpdate();
            try
            {
                for (int i = 0; i < list.Items.Count; i++)
                {
                    ZoneListItem item = list.Items[i] as ZoneListItem;
                    if (item != null)
                    {
                        list.Items[i] = new ZoneListItem(item.Zone, this);
                    }
                }

                if (selectedIndex >= 0 && selectedIndex < list.Items.Count)
                {
                    list.SelectedIndex = selectedIndex;
                }
            }
            finally
            {
                list.EndUpdate();
            }
        }

        private void ResetScanTimer()
        {
            if (IsShuttingDown())
            {
                return;
            }

            _scanTimer.Stop();
            _scanTimer.Tick -= ScanTimerTick;
            if (!_automationEnabled)
            {
                return;
            }

            _scanTimer.Interval = Math.Max(5, GetShortestConditionScanIntervalSeconds()) * 1000;
            _scanTimer.Tick += ScanTimerTick;
            if (HasZoneConditionScanZones())
            {
                _scanTimer.Start();
            }
        }

        private void ResetAppWatchTimer()
        {
            if (IsShuttingDown())
            {
                return;
            }

            _appWatchRunVersion++;
            _appWatchTimer.Stop();
            _appWatchTimerStartedAtLocal = null;
            if (!_automationEnabled)
            {
                RefreshSelectedAppWatchStatusLabel();
                return;
            }

            if (HasAppWatchZones())
            {
                _appWatchTimer.Interval = GetShortestAppWatchIntervalMilliseconds();
                _appWatchTimerStartedAtLocal = DateTime.Now;
                _appWatchTimer.Start();
            }
            RefreshSelectedAppWatchStatusLabel();
        }
    }
}
