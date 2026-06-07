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
        private sealed class AppWatchCheckTarget
        {
            public string ZoneId { get; set; }
            public string ZoneName { get; set; }
            public AppWatchItem Item { get; set; }
        }

        private void RunAppWatchCheck(ZoneRule zone, AppWatchItem item, bool launchIfMissing, string reason, bool showMessage)
        {
            if (IsShuttingDown())
            {
                return;
            }

            CaptureGlobalSettings();
            if (zone == null)
            {
                string message = "앱 감시를 확인할 위치를 먼저 선택하세요.";
                UpdateAppWatchStatusLabel(message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (item == null)
            {
                string message = "앱 감시 항목을 먼저 선택하세요.";
                UpdateAppWatchStatusLabel(message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            RunAppWatchChecks(new List<AppWatchCheckTarget>
            {
                new AppWatchCheckTarget
                {
                    ZoneId = zone.Id,
                    ZoneName = zone.Name,
                    Item = item.Clone()
                }
            }, launchIfMissing, reason, showMessage);
        }

        private void RunDueAppWatchChecks(bool force, string reason)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (_appWatchInProgress)
            {
                AppendLog(reason + " 건너뜀: 이전 확인이 아직 진행 중입니다.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            List<AppWatchCheckTarget> dueTargets = new List<AppWatchCheckTarget>();
            foreach (ZoneRule zone in _config.Zones.Where(z => z.Enabled && IsZoneActive(z)))
            {
                zone.Normalize();
                foreach (AppWatchItem item in zone.GetEnabledAppWatchItems())
                {
                    item.Normalize();
                    DateTime last;
                    string key = BuildAppWatchStatusKey(zone.Id, item.Id);
                    int interval = GetAppWatchIntervalMilliseconds(item.IntervalValue, item.IntervalUnit);
                    bool due = force
                        || !_lastAppWatchChecks.TryGetValue(key, out last)
                        || (now - last).TotalMilliseconds >= interval;

                    if (due)
                    {
                        _lastAppWatchChecks[key] = now;
                        dueTargets.Add(new AppWatchCheckTarget
                        {
                            ZoneId = zone.Id,
                            ZoneName = zone.Name,
                            Item = item.Clone()
                        });
                    }
                }
            }

            if (dueTargets.Count > 0)
            {
                RunAppWatchChecks(dueTargets, true, reason, false);
            }
        }

        private void RunAppWatchCheckWhenZoneIsActive(ZoneRule zone, AppWatchItem item, string reason)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (zone == null || item == null)
            {
                RunAppWatchCheck(zone, item, true, reason, false);
                return;
            }

            if (IsZoneActive(zone))
            {
                RunAppWatchCheck(zone.Clone(), item.Clone(), true, reason, false);
                return;
            }

            string message = "앱 감시 대기 · 위치 조건 일치 후 시작됩니다.";
            UpdateAppWatchStatusLabel(message);
            AppendLog(reason + " 대기(" + BuildAppWatchLogName(zone, item) + "): 위치 조건이 아직 일치하지 않습니다.");
            if (HasZoneConditionScanZones())
            {
                StartScan(false, false);
            }
        }

        private void RunAppWatchChecks(List<AppWatchCheckTarget> targets, bool launchIfMissing, string reason, bool showMessage)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (targets == null || targets.Count == 0)
            {
                return;
            }

            AppWatchCheckTarget firstTarget = targets[0];
            AppWatchItem firstItem = firstTarget == null ? null : firstTarget.Item;
            string processName = firstItem == null ? "" : AppWatchdog.NormalizeProcessName(firstItem.ProcessName);
            string launchTarget = firstItem == null ? "" : firstItem.LaunchTarget ?? "";
            string firstName = firstTarget == null
                ? "선택 위치"
                : firstTarget.ZoneName + " · " + BuildAppWatchItemDisplayName(firstItem);

            if (targets.Count == 1 && string.IsNullOrWhiteSpace(processName))
            {
                string message = "확인할 프로세스 이름을 입력하세요.";
                UpdateAppWatchStatusLabel(message);
                AppendLog(reason + " 실패(" + firstName + "): " + message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (targets.Count == 1 && launchIfMissing && string.IsNullOrWhiteSpace(launchTarget))
            {
                string message = "다시 실행할 앱 대상을 입력하세요.";
                UpdateAppWatchStatusLabel(message);
                AppendLog(reason + " 실패(" + firstName + "): " + message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (_appWatchInProgress)
            {
                AppendLog(reason + " 건너뜀: 이전 확인이 아직 진행 중입니다.");
                return;
            }

            if (_scanInProgress)
            {
                AppendLog(reason + " 건너뜀: 위치 조건 확인이 아직 진행 중입니다.");
                return;
            }

            int runVersion = _appWatchRunVersion;
            _appWatchInProgress = true;
            bool updateUi = showMessage;
            if (updateUi)
            {
                UpdateAppWatchStatusLabel(reason + " 중입니다... (" + FormatAppWatchTimestamp(DateTime.Now) + ")");
            }
            else
            {
                AppendLog(reason + " 시작: " + targets.Count + "개 항목");
            }

            Task.Factory.StartNew(delegate
            {
                List<AppWatchZoneResult> results = new List<AppWatchZoneResult>();
                foreach (AppWatchCheckTarget target in targets)
                {
                    if (runVersion != _appWatchRunVersion)
                    {
                        break;
                    }

                    if (target == null || target.Item == null)
                    {
                        continue;
                    }

                    AppWatchItem item = target.Item.Clone();
                    item.Normalize();
                    string zoneProcessName = AppWatchdog.NormalizeProcessName(item.ProcessName);
                    string zoneLaunchTarget = item.LaunchTarget ?? "";
                    bool zoneRequireWindow = item.RequireWindow.GetValueOrDefault(false);
                    AppWatchZoneResult zoneResult = new AppWatchZoneResult
                    {
                        ZoneId = target.ZoneId,
                        ZoneName = target.ZoneName,
                        ItemId = item.Id,
                        ItemName = BuildAppWatchItemDisplayName(item),
                        LaunchTarget = zoneLaunchTarget
                    };

                    try
                    {
                        if (string.IsNullOrWhiteSpace(zoneProcessName))
                        {
                            zoneResult.Error = "확인할 프로세스 이름을 입력하세요.";
                        }
                        else if (launchIfMissing && string.IsNullOrWhiteSpace(zoneLaunchTarget))
                        {
                            zoneResult.Error = "다시 실행할 앱 대상을 입력하세요.";
                        }
                        else
                        {
                            zoneResult.Result = launchIfMissing
                                ? AppWatchdog.EnsureRunning(zoneProcessName, zoneLaunchTarget, zoneRequireWindow, SafeLog)
                                : AppWatchdog.Check(zoneProcessName, zoneRequireWindow);
                        }
                    }
                    catch (Exception ex)
                    {
                        zoneResult.Error = ex.Message;
                    }

                    zoneResult.CheckedAtLocal = DateTime.Now;
                    zoneResult.NextCheckAtLocal = CalculateNextAppWatchCheckTime(item, zoneResult.CheckedAtLocal);
                    results.Add(zoneResult);
                }

                return results;
            }).ContinueWith(delegate(Task<List<AppWatchZoneResult>> task)
            {
                try
                {
                    _appWatchInProgress = false;
                    if (IsShuttingDown())
                    {
                        return;
                    }

                    if (runVersion != _appWatchRunVersion)
                    {
                        AppendLog(reason + " 결과 무시: 앱 감시 설정이 변경되었습니다.");
                        if (updateUi)
                        {
                            RefreshSelectedAppWatchStatusLabel();
                        }
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        string message = task.Exception == null ? "알 수 없는 앱 감시 오류입니다." : task.Exception.GetBaseException().Message;
                        if (updateUi)
                        {
                            UpdateAppWatchStatusLabel(message);
                        }
                        AppendLog(reason + " 실패: " + message);
                        if (showMessage)
                        {
                            MessageBox.Show(this, message, reason + " 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        return;
                    }

                    List<AppWatchZoneResult> results = task.Result ?? new List<AppWatchZoneResult>();
                    foreach (AppWatchZoneResult zoneResult in results)
                    {
                        if (!showMessage && !IsAppWatchResultStillRelevant(zoneResult))
                        {
                            continue;
                        }

                        string summary = BuildAppWatchResultSummary(zoneResult);
                        string displaySummary = BuildAppWatchDisplayText(zoneResult, summary);
                        string key = BuildAppWatchStatusKey(zoneResult.ZoneId, zoneResult.ItemId);

                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            _lastAppWatchStatusTexts[key] = displaySummary;
                        }

                        AppendLog(reason + " 결과(" + zoneResult.ZoneName + " · " + zoneResult.ItemName + "): " + displaySummary);
                        if (string.Equals(_currentZoneId, zoneResult.ZoneId, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(_selectedAppWatchItemId, zoneResult.ItemId, StringComparison.OrdinalIgnoreCase))
                        {
                            UpdateAppWatchStatusLabel(displaySummary);
                        }

                        if (zoneResult.Result != null && zoneResult.Result.LaunchAttempted)
                        {
                            ShowTrayNotification("앱 감시 재실행", BuildAppWatchNotificationText(zoneResult));
                        }
                    }

                    if (showMessage)
                    {
                        AppWatchZoneResult firstResult = results.FirstOrDefault();
                        string message = firstResult == null
                            ? "앱 감시 결과가 없습니다."
                            : BuildAppWatchDisplayText(firstResult, BuildAppWatchResultSummary(firstResult));
                        bool ok = firstResult != null && firstResult.Result != null && firstResult.Result.MeetsRequirement;
                        MessageBox.Show(this, message, reason, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    _appWatchInProgress = false;
                    DiagnosticsLog.Write("앱 감시 결과 처리 실패", ex);
                    SafeLog("앱 감시 결과 처리 실패: " + ex.Message);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private bool IsAppWatchResultStillRelevant(AppWatchZoneResult zoneResult)
        {
            if (zoneResult == null)
            {
                return false;
            }

            ZoneRule zone = FindZone(zoneResult.ZoneId);
            AppWatchItem item = FindAppWatchItem(zone, zoneResult.ItemId);
            return zone != null && zone.Enabled && IsZoneActive(zone) && item != null && item.Enabled;
        }

        private static string BuildAppWatchResultSummary(AppWatchZoneResult zoneResult)
        {
            if (zoneResult == null)
            {
                return "앱 감시 결과가 없습니다.";
            }

            if (!string.IsNullOrWhiteSpace(zoneResult.Error))
            {
                return zoneResult.Error;
            }

            if (zoneResult.Result == null || string.IsNullOrWhiteSpace(zoneResult.Result.Summary))
            {
                return "앱 감시 결과를 확인할 수 없습니다.";
            }

            return zoneResult.Result.Summary;
        }

        private string BuildCurrentAppWatchStatusText(ZoneRule zone, AppWatchItem item)
        {
            if (zone == null || item == null)
            {
                return "아직 확인 전입니다.";
            }

            string status;
            string key = BuildAppWatchStatusKey(zone.Id, item.Id);
            if (!string.IsNullOrWhiteSpace(key) && _lastAppWatchStatusTexts.TryGetValue(key, out status))
            {
                return status;
            }

            return BuildPendingAppWatchStatusText(zone, item);
        }

        private string BuildPendingAppWatchStatusText(ZoneRule zone, AppWatchItem item)
        {
            if (zone == null || item == null)
            {
                return "아직 확인 전입니다.";
            }

            if (!zone.Enabled)
            {
                return "앱 감시 대기 · 위치 미운영";
            }

            if (!item.Enabled)
            {
                return "앱 감시 꺼짐";
            }

            if (!IsZoneActive(zone))
            {
                return "앱 감시 대기 · 위치 조건 불일치";
            }

            DateTime? next = EstimateNextAppWatchCheckTime(zone, item);
            return next.HasValue
                ? "아직 확인 전입니다. · 다음 확인 " + FormatAppWatchTimestamp(next.Value)
                : "아직 확인 전입니다.";
        }

        private DateTime? EstimateNextAppWatchCheckTime(ZoneRule zone, AppWatchItem item)
        {
            if (zone == null || item == null || !zone.Enabled || !item.Enabled || !IsZoneActive(zone))
            {
                return null;
            }

            int interval = GetAppWatchIntervalMilliseconds(item.IntervalValue, item.IntervalUnit);
            DateTime lastUtc;
            string key = BuildAppWatchStatusKey(zone.Id, item.Id);
            if (!string.IsNullOrWhiteSpace(key) && _lastAppWatchChecks.TryGetValue(key, out lastUtc))
            {
                DateTime next = lastUtc.ToLocalTime().AddMilliseconds(interval);
                return next < DateTime.Now ? DateTime.Now : next;
            }

            if (_appWatchTimerStartedAtLocal.HasValue && _appWatchTimer.Enabled)
            {
                DateTime next = _appWatchTimerStartedAtLocal.Value.AddMilliseconds(_appWatchTimer.Interval);
                return next < DateTime.Now ? DateTime.Now : next;
            }

            return DateTime.Now.AddMilliseconds(interval);
        }

        private void RefreshSelectedAppWatchStatusLabel()
        {
            if (_loadingSelection || _appWatchStatusLabel == null)
            {
                return;
            }

            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                UpdateAppWatchStatusLabel("아직 확인 전입니다.");
                return;
            }

            AppWatchItem item = GetSelectedAppWatchItem(selected);
            UpdateAppWatchStatusLabel(item == null ? "등록된 앱 감시가 없습니다." : BuildCurrentAppWatchStatusText(selected, item));
        }

        private void ClearSelectedAppWatchStatus()
        {
            if (string.IsNullOrWhiteSpace(_currentZoneId))
            {
                return;
            }

            _lastAppWatchStatusTexts.Remove(BuildAppWatchStatusKey(_currentZoneId, _selectedAppWatchItemId));
        }

        private static DateTime? CalculateNextAppWatchCheckTime(AppWatchItem item, DateTime checkedAtLocal)
        {
            if (item == null || !item.Enabled)
            {
                return null;
            }

            int interval = GetAppWatchIntervalMilliseconds(item.IntervalValue, item.IntervalUnit);
            return checkedAtLocal.AddMilliseconds(interval);
        }

        private static string BuildAppWatchDisplayText(AppWatchZoneResult zoneResult, string summary)
        {
            string timing = BuildAppWatchTimingText(zoneResult, true);
            if (string.IsNullOrWhiteSpace(timing))
            {
                return summary ?? "";
            }

            return timing + " · " + (summary ?? "");
        }

        private static string BuildAppWatchNotificationText(AppWatchZoneResult zoneResult)
        {
            if (zoneResult == null)
            {
                return "앱 감시 재실행";
            }

            string timing = BuildAppWatchTimingText(zoneResult, false);
            string target = BuildLaunchNotificationText(zoneResult.LaunchTarget);
            string itemName = string.IsNullOrWhiteSpace(zoneResult.ItemName) ? target : zoneResult.ItemName;
            string message = zoneResult.ZoneName + " · " + itemName + " · " + target;
            return string.IsNullOrWhiteSpace(timing) ? message : timing + " · " + message;
        }

        private static string BuildAppWatchTimingText(AppWatchZoneResult zoneResult, bool includeNext)
        {
            if (zoneResult == null || zoneResult.CheckedAtLocal == default(DateTime))
            {
                return "";
            }

            string text = "확인 " + FormatAppWatchTimestamp(zoneResult.CheckedAtLocal);
            if (includeNext && zoneResult.NextCheckAtLocal.HasValue)
            {
                text += " · 다음 확인 " + FormatAppWatchTimestamp(zoneResult.NextCheckAtLocal.Value);
            }

            return text;
        }

        private static string FormatAppWatchTimestamp(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void UpdateAppWatchStatusLabel(string text)
        {
            if (IsShuttingDown())
            {
                return;
            }

            if (_appWatchStatusLabel != null && !_appWatchStatusLabel.IsDisposed)
            {
                try
                {
                    _appWatchStatusLabel.Text = text;
                }
                catch (Exception ex)
                {
                    DiagnosticsLog.Write("앱 감시 상태 표시 실패", ex);
                }
            }
        }
    }
}
