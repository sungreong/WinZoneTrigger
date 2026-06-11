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
        private void ShowAppWatchPicker()
        {
            using (AppPickerForm picker = new AppPickerForm())
            {
                if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedTargets.Count > 0)
                {
                    if (picker.SelectedTargets.Count == 1)
                    {
                        SetAppWatchTarget(picker.SelectedTargets[0]);
                    }
                    else
                    {
                        AddAppWatchTargets(picker.SelectedTargets);
                    }
                }
            }
        }

        private void BrowseAppWatchFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "감시할 앱 선택";
                dialog.Filter = "실행 파일 또는 바로가기|*.exe;*.lnk;*.bat;*.cmd|모든 파일|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SetAppWatchTarget(dialog.FileName);
                }
            }
        }

        private void AddNewAppWatchItem()
        {
            CaptureCurrentZone();
            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                MessageBox.Show(this, "앱 감시를 추가할 위치를 먼저 선택하세요.", "앱 감시", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (zone.AppWatchItems == null)
            {
                zone.AppWatchItems = new List<AppWatchItem>();
            }

            AppWatchItem item = AppWatchItem.CreateDefault();
            item.Enabled = false;
            zone.AppWatchItems.Add(item);
            _selectedAppWatchItemId = item.Id;
            zone.SyncLegacyAppWatchFields();
            CaptureGlobalSettings();
            RenderAppWatchItems();
            ResetScanTimer();
            ResetAppWatchTimer();
            AppendLog("앱 감시 항목을 추가했습니다: " + zone.Name);
        }

        private void RemoveSelectedAppWatchItem()
        {
            CaptureCurrentZone();
            ZoneRule zone = GetSelectedZone();
            AppWatchItem item = GetSelectedAppWatchItem(zone);
            if (zone == null || item == null)
            {
                MessageBox.Show(this, "삭제할 앱 감시 항목을 먼저 선택하세요.", "앱 감시", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string name = BuildAppWatchLogName(zone, item);
            zone.AppWatchItems.Remove(item);
            ClearAppWatchStatus(zone.Id, item.Id);
            _lastAppWatchChecks.Remove(BuildAppWatchStatusKey(zone.Id, item.Id));
            _selectedAppWatchItemId = zone.AppWatchItems.Count == 0 ? null : zone.AppWatchItems[0].Id;
            zone.SyncLegacyAppWatchFields();
            CaptureGlobalSettings();
            RenderAppWatchItems();
            ResetScanTimer();
            ResetAppWatchTimer();
            AppendLog("앱 감시 항목을 삭제했습니다: " + name);
        }

        private void AddAppWatchTargets(IEnumerable<string> targets)
        {
            CaptureCurrentZone();
            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                MessageBox.Show(this, "앱 감시를 추가할 위치를 먼저 선택하세요.", "앱 감시", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (zone.AppWatchItems == null)
            {
                zone.AppWatchItems = new List<AppWatchItem>();
            }

            int added = 0;
            foreach (string target in (targets ?? new List<string>()).Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                string value = target.Trim();
                string processName = AppWatchdog.InferProcessName(value);
                AppWatchItem existing = FindMatchingAppWatchItem(zone, value, processName);
                if (existing != null)
                {
                    _selectedAppWatchItemId = existing.Id;
                    continue;
                }

                AppWatchItem item = AppWatchItem.CreateDefault();
                item.LaunchTarget = value;
                item.ProcessName = processName;
                item.RequireWindow = ZoneRule.ShouldRequireWindowByDefault(processName, value);
                item.Normalize();
                zone.AppWatchItems.Add(item);
                _selectedAppWatchItemId = item.Id;
                added++;
            }

            zone.SyncLegacyAppWatchFields();
            ClearSelectedAppWatchStatus();
            CaptureGlobalSettings();
            RenderAppWatchItems();
            ResetScanTimer();
            ResetAppWatchTimer();
            if (added > 0)
            {
                AppendLog("앱 감시 항목을 추가했습니다: " + zone.Name + " · " + added + "개");
            }
        }

        private void SetAppWatchTarget(string target)
        {
            CaptureCurrentZone();
            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                MessageBox.Show(this, "앱 감시를 설정할 위치를 먼저 선택하세요.", "앱 감시", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (zone.AppWatchItems == null)
            {
                zone.AppWatchItems = new List<AppWatchItem>();
            }

            AppWatchItem item = GetSelectedAppWatchItem(zone);
            if (item == null)
            {
                item = AppWatchItem.CreateDefault();
                zone.AppWatchItems.Add(item);
                _selectedAppWatchItemId = item.Id;
            }

            bool wasEmpty = string.IsNullOrWhiteSpace(item.LaunchTarget) && string.IsNullOrWhiteSpace(item.ProcessName);
            string value = (target ?? "").Trim();
            item.LaunchTarget = value;
            if (wasEmpty && !string.IsNullOrWhiteSpace(value))
            {
                item.Enabled = true;
            }
            string inferredProcessName = AppWatchdog.InferProcessName(value);
            if (!string.IsNullOrWhiteSpace(inferredProcessName))
            {
                item.ProcessName = inferredProcessName;
            }
            if (ZoneRule.ShouldRequireWindowByDefault(item.ProcessName, value))
            {
                item.RequireWindow = true;
            }
            item.Normalize();
            zone.SyncLegacyAppWatchFields();
            ClearSelectedAppWatchStatus();
            CaptureGlobalSettings();
            RenderAppWatchItems();
            ResetScanTimer();
            ResetAppWatchTimer();
            RefreshSelectedAppWatchStatusLabel();
        }

        private void FillAppWatchProcessNameFromTarget(bool replaceExisting)
        {
            if (_appWatchTargetText == null || _appWatchProcessText == null)
            {
                return;
            }

            if (!replaceExisting && !string.IsNullOrWhiteSpace(_appWatchProcessText.Text))
            {
                return;
            }

            string inferred = AppWatchdog.InferProcessName(_appWatchTargetText.Text);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                _appWatchProcessText.Text = inferred;
            }
        }

        private void TestAppWatchLaunchTarget()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            AppWatchItem item = GetSelectedAppWatchItem(selected);
            string target = item == null ? "" : item.LaunchTarget ?? "";
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "실행할 앱을 먼저 입력하거나 선택하세요.", "실행 테스트", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                AppLauncher.LaunchApp(target, AppendLog);
                ShowTrayNotification("앱 감시 실행 테스트", BuildLaunchNotificationText(target));
            }
            catch (Exception ex)
            {
                AppendLog("앱 감시 실행 테스트 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "실행 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TestAppWatchStatusOnly()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            AppWatchItem item = GetSelectedAppWatchItem(selected);
            RunAppWatchCheck(selected == null ? null : selected.Clone(), item == null ? null : item.Clone(), false, "앱 감시 상태 테스트", true);
        }

        private void TestAppWatchRestart()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            AppWatchItem item = GetSelectedAppWatchItem(selected);
            RunAppWatchCheck(selected == null ? null : selected.Clone(), item == null ? null : item.Clone(), true, "앱 감시 재실행 테스트", true);
        }

        private void UpdateAppWatchIntervalLimits()
        {
            if (_appWatchIntervalInput == null)
            {
                return;
            }

            string unit = ReadAppWatchIntervalUnitSelection();
            decimal max = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            _appWatchIntervalInput.Maximum = max;
            if (_appWatchIntervalInput.Value > max)
            {
                _appWatchIntervalInput.Value = max;
            }
        }

        private void SetAppWatchIntervalUnitSelection(string unit)
        {
            if (_appWatchIntervalUnitCombo == null)
            {
                return;
            }

            _appWatchIntervalUnitCombo.SelectedItem = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? "시간" : "분";
            if (_appWatchIntervalUnitCombo.SelectedIndex < 0)
            {
                _appWatchIntervalUnitCombo.SelectedIndex = 0;
            }
            UpdateAppWatchIntervalLimits();
        }

        private string ReadAppWatchIntervalUnitSelection()
        {
            string selected = _appWatchIntervalUnitCombo == null ? "" : Convert.ToString(_appWatchIntervalUnitCombo.SelectedItem);
            return string.Equals(selected, "시간", StringComparison.OrdinalIgnoreCase) ? "Hours" : "Minutes";
        }

        private static int GetAppWatchIntervalMilliseconds(int value, string unit)
        {
            long multiplier = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 3600000L : 60000L;
            long milliseconds = Math.Max(1, value) * multiplier;
            if (milliseconds > int.MaxValue)
            {
                return int.MaxValue;
            }

            return Convert.ToInt32(milliseconds);
        }

        private void RenderAppWatchItems()
        {
            if (_appWatchItemsPanel == null)
            {
                return;
            }

            ClearChildControls(_appWatchItemsPanel);
            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                _selectedAppWatchItemId = null;
                AddEmptyChipHint(_appWatchItemsPanel, "위치를 먼저 선택하세요");
                LoadSelectedAppWatchItemToEditor(null);
                return;
            }

            zone.Normalize();
            EnsureSelectedAppWatchItem(zone);
            List<AppWatchItem> items = zone.AppWatchItems == null ? new List<AppWatchItem>() : zone.AppWatchItems.Where(item => item != null).ToList();
            if (items.Count == 0)
            {
                AddEmptyChipHint(_appWatchItemsPanel, "등록된 앱 감시 없음");
                LoadSelectedAppWatchItemToEditor(zone);
                return;
            }

            foreach (AppWatchItem item in items)
            {
                _appWatchItemsPanel.Controls.Add(CreateAppWatchItemRow(zone, item));
            }

            LoadSelectedAppWatchItemToEditor(zone);
        }

        private Control CreateAppWatchItemRow(ZoneRule zone, AppWatchItem item)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.AutoSize = false;
            row.Width = 610;
            row.Height = GetTextRowHeight(Font, 16, 42);
            row.WrapContents = false;
            row.Margin = new Padding(2, 2, 8, 2);
            row.Tag = item.Id;

            CheckBox toggle = new CheckBox();
            toggle.Appearance = Appearance.Normal;
            toggle.AutoSize = true;
            toggle.Margin = new Padding(2, 3, 4, 3);
            toggle.Tag = item.Id;
            toggle.Checked = item.Enabled;
            StyleSwitchToggle(toggle, "사용", "미사용");
            toggle.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }

                CaptureCurrentZone();
                ZoneRule selectedZone = GetSelectedZone();
                AppWatchItem selectedItem = FindAppWatchItem(selectedZone, Convert.ToString(toggle.Tag));
                if (selectedZone == null || selectedItem == null)
                {
                    return;
                }

                selectedItem.Enabled = toggle.Checked;
                selectedItem.Normalize();
                selectedZone.SyncLegacyAppWatchFields();
                _selectedAppWatchItemId = selectedItem.Id;
                ClearAppWatchStatus(selectedZone.Id, selectedItem.Id);
                CaptureGlobalSettings();
                ResetScanTimer();
                ResetAppWatchTimer();
                AppendLog((selectedItem.Enabled ? "앱 감시를 시작했습니다: " : "앱 감시를 껐습니다: ") + BuildAppWatchLogName(selectedZone, selectedItem));
                RenderAppWatchItems();
                if (selectedItem.Enabled)
                {
                    RunAppWatchCheckWhenZoneIsActive(selectedZone, selectedItem, "앱 감시 시작 확인");
                }
            };
            row.Controls.Add(toggle);

            Button chip = CreateValueChip(item.Id, BuildAppWatchItemChipText(item), string.Equals(_selectedAppWatchItemId, item.Id, StringComparison.OrdinalIgnoreCase));
            chip.Size = new Size(500, Math.Max(32, row.Height - 8));
            chip.Margin = new Padding(0, 4, 4, 4);
            chip.TextAlign = ContentAlignment.MiddleLeft;
            if (_toolTip != null)
            {
                _toolTip.SetToolTip(chip, BuildAppWatchTooltipText(item));
            }
            chip.Click += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }

                CaptureCurrentZone();
                _selectedAppWatchItemId = Convert.ToString(chip.Tag);
                RenderAppWatchItems();
                RefreshSelectedAppWatchStatusLabel();
            };
            row.Controls.Add(chip);

            return row;
        }

        private void EnsureSelectedAppWatchItem(ZoneRule zone)
        {
            if (zone == null || zone.AppWatchItems == null || zone.AppWatchItems.Count == 0)
            {
                _selectedAppWatchItemId = null;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_selectedAppWatchItemId)
                && zone.AppWatchItems.Any(item => item != null && string.Equals(item.Id, _selectedAppWatchItemId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            AppWatchItem first = zone.AppWatchItems.FirstOrDefault(item => item != null);
            _selectedAppWatchItemId = first == null ? null : first.Id;
        }

        private AppWatchItem GetSelectedAppWatchItem(ZoneRule zone)
        {
            if (zone == null)
            {
                return null;
            }

            zone.Normalize();
            EnsureSelectedAppWatchItem(zone);
            return FindAppWatchItem(zone, _selectedAppWatchItemId);
        }

        private static AppWatchItem FindAppWatchItem(ZoneRule zone, string itemId)
        {
            if (zone == null || zone.AppWatchItems == null || string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            return zone.AppWatchItems.FirstOrDefault(item => item != null && string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));
        }

        private static AppWatchItem FindMatchingAppWatchItem(ZoneRule zone, string launchTarget, string processName)
        {
            if (zone == null || zone.AppWatchItems == null)
            {
                return null;
            }

            string target = (launchTarget ?? "").Trim();
            string process = AppWatchdog.NormalizeProcessName(processName);
            return zone.AppWatchItems.FirstOrDefault(item =>
                item != null
                && ((!string.IsNullOrWhiteSpace(target) && string.Equals((item.LaunchTarget ?? "").Trim(), target, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(process) && string.Equals(AppWatchdog.NormalizeProcessName(item.ProcessName), process, StringComparison.OrdinalIgnoreCase))));
        }

        private void LoadSelectedAppWatchItemToEditor(ZoneRule zone)
        {
            bool previousLoading = _loadingSelection;
            _loadingSelection = true;
            try
            {
                AppWatchItem item = GetSelectedAppWatchItem(zone);
                bool enabled = item != null;
                SetAppWatchEditorEnabled(enabled);

                _appWatchEnabledCheck.Checked = item != null && item.Enabled;
                _appWatchRequireWindowCheck.Checked = item != null && item.RequireWindow.GetValueOrDefault(false);
                _appWatchTargetText.Text = item == null ? "" : item.LaunchTarget ?? "";
                _appWatchProcessText.Text = item == null ? "" : item.ProcessName ?? "";
                SetAppWatchIntervalUnitSelection(item == null ? "Minutes" : item.IntervalUnit);
                _appWatchIntervalInput.Value = item == null
                    ? 5
                    : Math.Max(_appWatchIntervalInput.Minimum, Math.Min(_appWatchIntervalInput.Maximum, item.IntervalValue));
                StyleSwitchToggle(_appWatchEnabledCheck, "앱 감시 사용", "앱 감시 미사용");
                StyleSwitchToggle(_appWatchRequireWindowCheck, "표시 창 필요", "표시 창 무시");
                UpdateAppWatchStatusLabel(item == null ? "등록된 앱 감시가 없습니다." : BuildCurrentAppWatchStatusText(zone, item));
            }
            finally
            {
                _loadingSelection = previousLoading;
            }
        }

        private void SetAppWatchEditorEnabled(bool enabled)
        {
            if (_appWatchEnabledCheck != null)
            {
                _appWatchEnabledCheck.Enabled = enabled;
            }
            if (_appWatchRequireWindowCheck != null)
            {
                _appWatchRequireWindowCheck.Enabled = enabled;
            }
            if (_appWatchTargetText != null)
            {
                _appWatchTargetText.Enabled = enabled;
            }
            if (_appWatchProcessText != null)
            {
                _appWatchProcessText.Enabled = enabled;
            }
            if (_appWatchIntervalInput != null)
            {
                _appWatchIntervalInput.Enabled = enabled;
            }
            if (_appWatchIntervalUnitCombo != null)
            {
                _appWatchIntervalUnitCombo.Enabled = enabled;
            }
        }

        private void CaptureSelectedAppWatchItemValues(ZoneRule zone)
        {
            if (_loadingSelection || zone == null)
            {
                return;
            }

            AppWatchItem item = GetSelectedAppWatchItem(zone);
            if (item == null)
            {
                return;
            }

            item.Enabled = _appWatchEnabledCheck.Checked;
            item.RequireWindow = _appWatchRequireWindowCheck.Checked;
            item.LaunchTarget = _appWatchTargetText.Text.Trim();
            item.ProcessName = _appWatchProcessText.Text.Trim();
            item.IntervalValue = Convert.ToInt32(_appWatchIntervalInput.Value);
            item.IntervalUnit = ReadAppWatchIntervalUnitSelection();
            item.Normalize();
            zone.SyncLegacyAppWatchFields();
        }

        private static string BuildAppWatchStatusKey(string zoneId, string itemId)
        {
            return (zoneId ?? "") + ":" + (itemId ?? "");
        }

        private void ClearAppWatchStatus(string zoneId, string itemId)
        {
            _lastAppWatchStatusTexts.Remove(BuildAppWatchStatusKey(zoneId, itemId));
        }

        private string BuildAppWatchLogName(ZoneRule zone, AppWatchItem item)
        {
            string zoneName = zone == null ? "선택 위치" : zone.Name;
            string itemName = BuildAppWatchItemDisplayName(item);
            return string.IsNullOrWhiteSpace(itemName) ? zoneName : zoneName + " · " + itemName;
        }

        private string BuildAppWatchItemChipText(AppWatchItem item)
        {
            string name = BuildAppWatchItemDisplayName(item);
            string interval = BuildAppWatchIntervalText(item);
            string window = item != null && item.RequireWindow.GetValueOrDefault(false) ? "창 확인" : "프로세스";
            return ShortenText(name, 30) + " · " + interval + " · " + window;
        }

        private string BuildAppWatchTooltipText(AppWatchItem item)
        {
            if (item == null)
            {
                return "";
            }

            return "실행 대상: " + (item.LaunchTarget ?? "") + Environment.NewLine
                + "프로세스: " + (item.ProcessName ?? "") + Environment.NewLine
                + "체크 주기: " + BuildAppWatchIntervalText(item);
        }

        private string BuildAppWatchItemDisplayName(AppWatchItem item)
        {
            if (item == null)
            {
                return "";
            }

            string targetName = ShortenAppTargetForChip(item.LaunchTarget);
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                return targetName;
            }

            string process = AppWatchdog.NormalizeProcessName(item.ProcessName);
            return string.IsNullOrWhiteSpace(process) ? "새 감시" : process;
        }

        private static string BuildAppWatchIntervalText(AppWatchItem item)
        {
            if (item == null)
            {
                return "5분";
            }

            bool hours = string.Equals(item.IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase);
            return Math.Max(1, item.IntervalValue) + (hours ? "시간" : "분");
        }

    }
}
