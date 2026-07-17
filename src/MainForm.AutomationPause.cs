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

            if (_automationPauseMenu == null || _automationPauseMenu.IsDisposed)
            {
                _automationPauseMenu = CreateAutomationPauseMenu();
            }

            _automationPauseMenu.Show(_pauseAutomationButton, new Point(0, _pauseAutomationButton.Height));
        }

        private ContextMenuStrip CreateAutomationPauseMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            AddPauseMenuItem(menu, "30분 동안 정지", TimeSpan.FromMinutes(30));
            AddPauseMenuItem(menu, "1시간 동안 정지", TimeSpan.FromHours(1));
            AddPauseMenuItem(menu, "2시간 동안 정지", TimeSpan.FromHours(2));
            AddPauseMenuItem(menu, "12시간 동안 정지", TimeSpan.FromHours(12));
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem midnightItem = new ToolStripMenuItem("오늘 자정까지 정지");
            midnightItem.Click += delegate
            {
                PauseAutomationFor(DateTime.Today.AddDays(1) - DateTime.Now);
            };
            menu.Items.Add(midnightItem);
            return menu;
        }

        private void AddPauseMenuItem(ContextMenuStrip menu, string text, TimeSpan duration)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { PauseAutomationFor(duration); };
            menu.Items.Add(item);
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
            DateTime untilUtc = _config.AutomationPausedUntilUtc.Value;
            if (SaveAutomationPauseSetting("자동화를 " + FormatPauseUntil(untilUtc) + "까지 임시 정지했습니다.", true))
            {
                MessageBox.Show(
                    this,
                    "자동화를 " + FormatPauseUntil(untilUtc) + "까지 정지했습니다.\r\n상단 버튼에도 정지 종료 시각이 표시됩니다.",
                    "자동화 임시 정지",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ResumeAutomation()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            _config.AutomationPausedUntilUtc = null;
            SaveAutomationPauseSetting("자동화 임시 정지를 해제했습니다.", false);
        }

        private bool SaveAutomationPauseSetting(string message, bool expectPaused)
        {
            try
            {
                ConfigStore.Save(_config);
                AppConfig saved = ConfigStore.Load();
                if (saved == null || saved.IsAutomationPaused() != expectPaused)
                {
                    throw new InvalidOperationException(expectPaused
                        ? "임시 정지 시간이 설정 파일에 저장되지 않았습니다."
                        : "임시 정지 해제가 설정 파일에 저장되지 않았습니다.");
                }
                EnsureBackgroundAutomationRunningAfterSave();
                UpdateAutomationPauseButton();
                AppendLog(message);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("자동화 임시 정지 설정 저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "임시 정지 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
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
                _pauseAutomationButton.Text = "정지 중 · " + FormatPauseButtonUntil(_config.AutomationPausedUntilUtc.Value);
                _pauseAutomationButton.BackColor = UiAmberSoft;
                _pauseAutomationButton.ForeColor = UiText;
                EnsureToolTip().SetToolTip(_pauseAutomationButton, "클릭하면 자동 실행과 앱 감시를 바로 다시 시작합니다.");
                return;
            }

            _pauseAutomationButton.Text = "자동화 실행 중 · 임시 정지";
            StyleButton(_pauseAutomationButton, ButtonTone.Default);
            EnsureToolTip().SetToolTip(_pauseAutomationButton, "자동 실행, 앱 감시, 화면 밝기 일정을 잠시 멈춥니다.");
        }

        private static string FormatPauseUntil(DateTime untilUtc)
        {
            return untilUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }

        private static string FormatPauseButtonUntil(DateTime untilUtc)
        {
            return untilUtc.ToLocalTime().ToString("MM/dd HH:mm") + "까지";
        }
    }
}
