using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void LogRefreshTimerTick(object sender, EventArgs e)
        {
            RefreshLogDisplayFromFile();
            RefreshAutomationStateFromFile();
        }

        private void RefreshAutomationStateFromFile()
        {
            if (IsShuttingDown())
            {
                return;
            }

            try
            {
                if (!File.Exists(AutomationStateStore.StatePath))
                {
                    return;
                }

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(AutomationStateStore.StatePath);
                if (lastWriteUtc <= _automationStateLastWriteUtc)
                {
                    return;
                }

                AutomationStateSnapshot snapshot = AutomationStateStore.Load();
                _automationStateLastWriteUtc = lastWriteUtc;
                if (snapshot == null)
                {
                    return;
                }

                ApplyAutomationState(snapshot);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("자동화 상태 표시 갱신 실패", ex);
            }
        }

        private void ApplyAutomationState(AutomationStateSnapshot snapshot)
        {
            HashSet<string> activeIds = new HashSet<string>(
                snapshot.ActiveZoneIds ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (ZoneRule zone in _config.Zones)
            {
                if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
                {
                    continue;
                }

                _insideZones[zone.Id] = activeIds.Contains(zone.Id);
            }

            List<string> activeNames = snapshot.ActiveZoneNames ?? new List<string>();
            string activePrefix = activeNames.Count == 0 ? "없음" : string.Join(", ", activeNames.ToArray());
            SetStatusLabel(
                _activeZonesLabel,
                activePrefix + " · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                activeNames.Count > 0);

            if (_currentLocationLabel != null && !_currentLocationLabel.IsDisposed)
            {
                if (snapshot.CurrentLocation != null)
                {
                    SetStatusLabel(
                        _currentLocationLabel,
                        FormatLocation(snapshot.CurrentLocation)
                            + " · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        true);
                }
                else if (snapshot.LocationWasRequested)
                {
                    string error = string.IsNullOrWhiteSpace(snapshot.LocationError)
                        ? "Windows 위치를 사용할 수 없습니다."
                        : snapshot.LocationError;
                    SetStatusLabel(
                        _currentLocationLabel,
                        "사용 불가: " + error + " · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        false);
                }
                else
                {
                    SetStatusLabel(
                        _currentLocationLabel,
                        "백그라운드에서 좌표 감지를 요청하지 않았습니다. · " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        false);
                }
            }

            if (_visibleNetworksLabel != null && !_visibleNetworksLabel.IsDisposed)
            {
                List<string> visibleSsids = snapshot.VisibleSsids ?? new List<string>();
                if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
                {
                    SetStatusLabel(
                        _visibleNetworksLabel,
                        "사용 불가: " + snapshot.WifiError + " · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        false);
                }
                else if (visibleSsids.Count == 0)
                {
                    SetStatusLabel(
                        _visibleNetworksLabel,
                        "보이는 Wi-Fi가 없습니다. · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        false);
                }
                else
                {
                    SetStatusLabel(
                        _visibleNetworksLabel,
                        string.Join(", ", visibleSsids.Take(12).ToArray())
                            + " · 백그라운드 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                        true);
                }
            }

            SetStatusLabel(
                _lastActionLabel,
                string.IsNullOrWhiteSpace(snapshot.LastActionText) ? "아직 실행된 동작이 없습니다." : snapshot.LastActionText,
                !string.IsNullOrWhiteSpace(snapshot.LastActionText));
            SetStatusLabel(
                _lastAppWatchLabel,
                string.IsNullOrWhiteSpace(snapshot.LastAppWatchText) ? "아직 앱 감시 결과가 없습니다." : snapshot.LastAppWatchText,
                !string.IsNullOrWhiteSpace(snapshot.LastAppWatchText));
            SetStatusLabel(
                _backgroundProcessLabel,
                "pid " + snapshot.ProcessId + " · 상태 갱신 " + snapshot.UpdatedAtLocal.ToString("HH:mm:ss"),
                snapshot.ProcessId > 0);

            if (!string.IsNullOrWhiteSpace(snapshot.LastEventText))
            {
                DateTime eventTime = snapshot.LastEventAtLocal == DateTime.MinValue
                    ? snapshot.UpdatedAtLocal
                    : snapshot.LastEventAtLocal;
                SetStatusLabel(
                    _recentLogLabel,
                    eventTime.ToString("yyyy-MM-dd HH:mm:ss") + "  " + snapshot.LastEventText,
                    true);
            }

            InvalidateZoneLists();
            UpdateSelectedZoneSummary();
            RefreshSelectedAppWatchStatusLabel();
        }

        private void SetStatusLabel(Label label, string text, bool emphasized)
        {
            if (label == null || label.IsDisposed)
            {
                return;
            }

            label.Text = text ?? "";
            label.BackColor = emphasized ? UiAccentSoft : UiSurface;
            label.ForeColor = emphasized ? UiAccentDark : UiText;
            label.Padding = new Padding(8, 4, 8, 4);
        }

        private void RefreshLogDisplayFromFile()
        {
            if (IsShuttingDown())
            {
                return;
            }

            try
            {
                if (!File.Exists(DiagnosticsLog.ActivityLogPath))
                {
                    UpdateLogDisplay("아직 기록된 이벤트가 없습니다.");
                    return;
                }

                string[] lines = File.ReadAllLines(DiagnosticsLog.ActivityLogPath);
                string text = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 80)).ToArray());
                UpdateLogDisplay(text);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("로그 표시 갱신 실패", ex);
            }
        }

        private void UpdateLogDisplayFromMessage(string message)
        {
            if (IsShuttingDown())
            {
                return;
            }

            try
            {
                if (_recentLogLabel != null && !_recentLogLabel.IsDisposed)
                {
                    _recentLogLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (message ?? "");
                }

                if (_logText != null && !_logText.IsDisposed)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (message ?? "");
                    _logText.AppendText((_logText.TextLength == 0 ? "" : Environment.NewLine) + line);
                    _logText.SelectionStart = _logText.TextLength;
                    _logText.ScrollToCaret();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("로그 표시 실패", ex);
            }
        }

        private void UpdateLogDisplay(string text)
        {
            if (IsShuttingDown())
            {
                return;
            }

            string value = text ?? "";
            if (_recentLogLabel != null && !_recentLogLabel.IsDisposed)
            {
                string lastLine = value.Replace("\r", "\n")
                    .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault();
                _recentLogLabel.Text = string.IsNullOrWhiteSpace(lastLine) ? "아직 기록된 이벤트가 없습니다." : lastLine;
            }

            if (_logText != null && !_logText.IsDisposed && !string.Equals(_logText.Text, value, StringComparison.Ordinal))
            {
                _logText.Text = value;
                _logText.SelectionStart = _logText.TextLength;
                _logText.ScrollToCaret();
            }
        }
    }
}
