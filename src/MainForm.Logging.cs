using System;
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
