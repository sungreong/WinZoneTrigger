using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void EnsureBackgroundAutomationRunningAfterSave()
        {
            if (_automationEnabled || !HasBackgroundAutomationWork())
            {
                return;
            }

            try
            {
                using (Mutex.OpenExisting(@"Local\WinZoneTrigger.BackgroundInstance"))
                {
                    return;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = Application.ExecutablePath;
                startInfo.Arguments = "--minimized";
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                Process.Start(startInfo);
                AppendLog("백그라운드 자동 실행 프로세스를 시작했습니다.");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("저장 후 백그라운드 자동 실행 시작 실패", ex);
            }
        }

        private bool HasBackgroundAutomationWork()
        {
            return _config != null
                && (_config.BrightnessScheduleEnabled
                    || (_config.Zones != null
                        && _config.Zones.Any(z => z != null
                            && z.Enabled
                            && (z.RunOnceAtStartup.GetValueOrDefault(true)
                                || z.MonitoringEnabled.GetValueOrDefault(false)
                                || z.GetEnabledAppWatchItems().Any()))));
        }
    }
}
