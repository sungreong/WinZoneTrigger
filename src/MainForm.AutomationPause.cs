using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void ShowAutomationPauseMenu()
        {
            if (_config != null && _config.IsAutomationPaused())
            {
                ResumeAutomation();
                return;
            }

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("30분 동안 정지", null, delegate { PauseAutomationFor(TimeSpan.FromMinutes(30)); });
            menu.Items.Add("1시간 동안 정지", null, delegate { PauseAutomationFor(TimeSpan.FromHours(1)); });
            menu.Items.Add("2시간 동안 정지", null, delegate { PauseAutomationFor(TimeSpan.FromHours(2)); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("오늘 자정까지 정지", null, delegate
            {
                TimeSpan remaining = DateTime.Today.AddDays(1) - DateTime.Now;
                PauseAutomationFor(remaining);
            });
            menu.Closed += delegate { menu.Dispose(); };
            menu.Show(_pauseAutomationButton, new Point(0, _pauseAutomationButton.Height));
        }

        private void PauseAutomationFor(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return;
            }

            CaptureCurrentZone();
            CaptureGlobalSettings();
            _config.AutomationPausedUntilUtc = DateTime.UtcNow.Add(duration);
            SaveAutomationPauseSetting("자동화를 " + FormatPauseUntil(_config.AutomationPausedUntilUtc.Value) + "까지 임시 정지했습니다.");
        }

        private void ResumeAutomation()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            _config.AutomationPausedUntilUtc = null;
            SaveAutomationPauseSetting("자동화 임시 정지를 해제했습니다.");
        }

        private void SaveAutomationPauseSetting(string message)
        {
            try
            {
                ConfigStore.Save(_config);
                EnsureBackgroundAutomationRunningAfterSave();
                UpdateAutomationPauseButton();
                AppendLog(message);
            }
            catch (Exception ex)
            {
                AppendLog("자동화 임시 정지 설정 저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "임시 정지 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateAutomationPauseButton()
        {
            if (_pauseAutomationButton == null || _pauseAutomationButton.IsDisposed || _config == null)
            {
                return;
            }

            if (_config.IsAutomationPaused())
            {
                _pauseAutomationButton.Text = "정지 해제 (" + FormatPauseRemaining(_config.AutomationPausedUntilUtc.Value) + ")";
                _pauseAutomationButton.BackColor = UiAmberSoft;
                _pauseAutomationButton.ForeColor = UiText;
                EnsureToolTip().SetToolTip(_pauseAutomationButton, "클릭하면 자동 실행과 앱 감시를 바로 다시 시작합니다.");
                return;
            }

            _pauseAutomationButton.Text = "임시 정지";
            _pauseAutomationButton.UseVisualStyleBackColor = true;
            _pauseAutomationButton.ForeColor = UiText;
            EnsureToolTip().SetToolTip(_pauseAutomationButton, "자동 실행, 앱 감시, 화면 밝기 일정을 잠시 멈춥니다.");
        }

        private static string FormatPauseUntil(DateTime untilUtc)
        {
            return untilUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string FormatPauseRemaining(DateTime untilUtc)
        {
            TimeSpan remaining = untilUtc.ToUniversalTime() - DateTime.UtcNow;
            if (remaining.TotalMinutes < 1)
            {
                return "1분 미만";
            }
            if (remaining.TotalHours < 1)
            {
                return Math.Ceiling(remaining.TotalMinutes) + "분";
            }
            return Math.Ceiling(remaining.TotalHours) + "시간";
        }
    }
}
