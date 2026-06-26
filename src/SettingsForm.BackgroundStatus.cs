using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class SettingsForm
    {
        private TabControl CreateSettingsTabs()
        {
            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Margin = new Padding(0);
            tabs.Padding = new Point(18, 6);
            tabs.ItemSize = new Size(150, 36);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.DrawItem += DrawSettingsTab;
            return tabs;
        }

        private void DrawSettingsTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count)
            {
                return;
            }

            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            Color backColor = selected ? Color.FromArgb(31, 122, 92) : Color.FromArgb(228, 236, 226);
            Color textColor = selected ? Color.White : Color.FromArgb(35, 45, 47);
            using (SolidBrush brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }
            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                Font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void BuildBackgroundStatusTab(TabPage page)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            page.Controls.Add(panel);

            TableLayoutPanel content = new TableLayoutPanel();
            content.AutoSize = true;
            content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            content.ColumnCount = 1;
            content.RowCount = 0;
            content.Dock = DockStyle.Top;
            content.Margin = new Padding(0);
            panel.Controls.Add(content);
            panel.Resize += delegate { content.Width = Math.Max(0, panel.ClientSize.Width - 1); };
            content.Width = Math.Max(0, panel.ClientSize.Width - 1);

            Label title = new Label();
            title.Text = "백그라운드 상태";
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(20, 91, 69);
            title.Margin = new Padding(0, 0, 0, 12);
            content.Controls.Add(title, 0, content.RowCount++);

            TableLayoutPanel healthPanel = CreateSection("실행 상태");
            _backgroundHealthValue = CreateStatusValueLabel();
            healthPanel.Controls.Add(CreateStatusValueLine("상태", _backgroundHealthValue), 0, healthPanel.RowCount++);
            _backgroundProcessValue = CreateStatusValueLabel();
            healthPanel.Controls.Add(CreateStatusValueLine("프로세스", _backgroundProcessValue), 0, healthPanel.RowCount++);
            _backgroundUpdatedValue = CreateStatusValueLabel();
            healthPanel.Controls.Add(CreateStatusValueLine("상태 갱신", _backgroundUpdatedValue), 0, healthPanel.RowCount++);

            FlowLayoutPanel actionButtons = new FlowLayoutPanel();
            actionButtons.AutoSize = true;
            actionButtons.WrapContents = true;
            actionButtons.Margin = new Padding(0, 10, 0, 0);
            Button refreshButton = CreateButton("새로고침");
            refreshButton.Click += delegate { RefreshBackgroundStatus(); };
            actionButtons.Controls.Add(refreshButton);
            Button startButton = CreateButton("백그라운드 시작");
            startButton.Click += delegate { StartBackgroundAutomationFromSettings(); };
            actionButtons.Controls.Add(startButton);
            Button logButton = CreateButton("로그 파일");
            logButton.Click += delegate { OpenFile(DiagnosticsLog.ActivityLogPath); };
            actionButtons.Controls.Add(logButton);
            healthPanel.Controls.Add(actionButtons, 0, healthPanel.RowCount++);
            content.Controls.Add(healthPanel, 0, content.RowCount++);

            TableLayoutPanel scanPanel = CreateSection("최근 감지");
            _backgroundActiveZonesValue = CreateStatusValueLabel();
            scanPanel.Controls.Add(CreateStatusValueLine("활성 위치", _backgroundActiveZonesValue), 0, scanPanel.RowCount++);
            _backgroundWifiValue = CreateStatusValueLabel();
            scanPanel.Controls.Add(CreateStatusValueLine("Wi-Fi", _backgroundWifiValue), 0, scanPanel.RowCount++);
            _backgroundLocationValue = CreateStatusValueLabel();
            scanPanel.Controls.Add(CreateStatusValueLine("좌표", _backgroundLocationValue), 0, scanPanel.RowCount++);
            content.Controls.Add(scanPanel, 0, content.RowCount++);

            TableLayoutPanel eventPanel = CreateSection("최근 작업");
            _backgroundLastEventValue = CreateStatusValueLabel();
            eventPanel.Controls.Add(CreateStatusValueLine("마지막 이벤트", _backgroundLastEventValue), 0, eventPanel.RowCount++);
            _backgroundLastActionValue = CreateStatusValueLabel();
            eventPanel.Controls.Add(CreateStatusValueLine("마지막 동작", _backgroundLastActionValue), 0, eventPanel.RowCount++);
            _backgroundLastAppWatchValue = CreateStatusValueLabel();
            eventPanel.Controls.Add(CreateStatusValueLine("앱 감시", _backgroundLastAppWatchValue), 0, eventPanel.RowCount++);
            content.Controls.Add(eventPanel, 0, content.RowCount++);
        }

        private void RefreshBackgroundStatus()
        {
            AutomationStateSnapshot snapshot = AutomationStateStore.Load();
            bool mutexAlive = IsBackgroundMutexAlive();
            bool processAlive = snapshot != null && IsProcessAlive(snapshot.ProcessId);
            bool hasState = snapshot != null && snapshot.UpdatedAtLocal != DateTime.MinValue;
            TimeSpan age = hasState ? DateTime.Now - snapshot.UpdatedAtLocal : TimeSpan.MaxValue;
            bool fresh = hasState && age.TotalMinutes <= 2;

            if (mutexAlive && fresh)
            {
                SetStatusValue(_backgroundHealthValue, "정상 실행 중", true);
            }
            else if (mutexAlive)
            {
                SetStatusValue(_backgroundHealthValue, "실행 중 · 상태 갱신 지연", false);
            }
            else if (processAlive)
            {
                SetStatusValue(_backgroundHealthValue, "프로세스 있음 · 백그라운드 잠금 확인 필요", false);
            }
            else
            {
                SetStatusValue(_backgroundHealthValue, "꺼짐 또는 아직 시작 전", false);
            }

            string processText = snapshot == null || snapshot.ProcessId <= 0
                ? "상태 파일 없음"
                : "pid " + snapshot.ProcessId + (processAlive ? " · 실행 중" : " · 프로세스 없음");
            SetStatusValue(_backgroundProcessValue, processText, processAlive || mutexAlive);

            SetStatusValue(
                _backgroundUpdatedValue,
                hasState
                    ? snapshot.UpdatedAtLocal.ToString("yyyy-MM-dd HH:mm:ss") + " · " + FormatAge(age)
                    : "아직 상태 파일이 없습니다.",
                fresh);

            List<string> activeZones = snapshot == null || snapshot.ActiveZoneNames == null
                ? new List<string>()
                : snapshot.ActiveZoneNames;
            SetStatusValue(
                _backgroundActiveZonesValue,
                activeZones.Count == 0 ? "없음" : string.Join(", ", activeZones.ToArray()),
                activeZones.Count > 0);

            string wifiText = "상태 없음";
            bool wifiOk = false;
            if (snapshot != null)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
                {
                    wifiText = "오류: " + snapshot.WifiError;
                }
                else if (snapshot.VisibleSsids != null && snapshot.VisibleSsids.Count > 0)
                {
                    wifiText = string.Join(", ", snapshot.VisibleSsids.Take(8).ToArray());
                    wifiOk = true;
                }
                else
                {
                    wifiText = "보이는 Wi-Fi 없음";
                }
            }
            SetStatusValue(_backgroundWifiValue, wifiText, wifiOk);

            string locationText = "상태 없음";
            bool locationOk = false;
            if (snapshot != null)
            {
                if (snapshot.CurrentLocation != null)
                {
                    locationText = snapshot.CurrentLocation.Latitude.ToString("0.######", CultureInfo.InvariantCulture)
                        + ", " + snapshot.CurrentLocation.Longitude.ToString("0.######", CultureInfo.InvariantCulture)
                        + " · accuracy " + snapshot.CurrentLocation.AccuracyMeters.ToString("0", CultureInfo.InvariantCulture) + "m";
                    locationOk = true;
                }
                else if (snapshot.LocationWasRequested)
                {
                    locationText = string.IsNullOrWhiteSpace(snapshot.LocationError)
                        ? "위치 사용 불가"
                        : snapshot.LocationError;
                }
                else
                {
                    locationText = "좌표 감지 요청 없음";
                }
            }
            SetStatusValue(_backgroundLocationValue, locationText, locationOk);

            SetStatusValue(
                _backgroundLastEventValue,
                FormatEvent(snapshot == null ? DateTime.MinValue : snapshot.LastEventAtLocal, snapshot == null ? "" : snapshot.LastEventText),
                snapshot != null && !string.IsNullOrWhiteSpace(snapshot.LastEventText));
            SetStatusValue(
                _backgroundLastActionValue,
                string.IsNullOrWhiteSpace(snapshot == null ? "" : snapshot.LastActionText) ? "아직 실행된 동작 없음" : snapshot.LastActionText,
                snapshot != null && !string.IsNullOrWhiteSpace(snapshot.LastActionText));
            SetStatusValue(
                _backgroundLastAppWatchValue,
                string.IsNullOrWhiteSpace(snapshot == null ? "" : snapshot.LastAppWatchText) ? "아직 앱 감시 결과 없음" : snapshot.LastAppWatchText,
                snapshot != null && !string.IsNullOrWhiteSpace(snapshot.LastAppWatchText));
        }

        private void StartBackgroundAutomationFromSettings()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--minimized";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                Process.Start(startInfo);
                DiagnosticsLog.WriteEvent("설정 화면에서 백그라운드 시작 요청");
                RefreshBackgroundStatus();
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("설정 화면 백그라운드 시작 실패", ex);
                MessageBox.Show(this, ex.Message, "백그라운드 시작 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static bool IsBackgroundMutexAlive()
        {
            try
            {
                using (Mutex.OpenExisting(@"Local\WinZoneTrigger.BackgroundInstance"))
                {
                    return true;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                Process process = Process.GetProcessById(processId);
                using (process)
                {
                    return !process.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalSeconds < 0)
            {
                return "방금 갱신";
            }
            if (age.TotalSeconds < 60)
            {
                return Convert.ToInt32(age.TotalSeconds) + "초 전";
            }
            if (age.TotalMinutes < 60)
            {
                return Convert.ToInt32(age.TotalMinutes) + "분 전";
            }
            return Convert.ToInt32(age.TotalHours) + "시간 전";
        }

        private static string FormatEvent(DateTime time, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "아직 기록된 이벤트 없음";
            }
            if (time == DateTime.MinValue)
            {
                return text;
            }
            return time.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text;
        }
    }
}
