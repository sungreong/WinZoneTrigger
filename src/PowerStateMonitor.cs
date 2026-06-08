using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinZoneTrigger
{
    internal sealed class PowerStateMonitor : IDisposable
    {
        private const uint EsContinuous = 0x80000000;
        private const uint EsSystemRequired = 0x00000001;

        private readonly Action<PowerModes> _powerModeChanged;
        private bool _subscribed;
        private bool _sleepPreventionActive;
        private bool _disposed;

        public PowerStateMonitor(Action<PowerModes> powerModeChanged)
        {
            _powerModeChanged = powerModeChanged;
            TrySubscribe();
        }

        public void SetSleepPrevention(bool enabled, string reason)
        {
            if (_disposed)
            {
                return;
            }

            if (enabled)
            {
                EnableSleepPrevention(reason);
            }
            else
            {
                DisableSleepPrevention(reason);
            }
        }

        private void EnableSleepPrevention(string reason)
        {
            if (_sleepPreventionActive)
            {
                return;
            }

            try
            {
                uint result = SetThreadExecutionState(EsContinuous | EsSystemRequired);
                if (result == 0)
                {
                    DiagnosticsLog.WriteEvent("절전 방지 설정 실패: " + (reason ?? ""));
                    return;
                }

                _sleepPreventionActive = true;
                DiagnosticsLog.WriteEvent("절전 방지 활성화: " + (reason ?? ""));
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("절전 방지 활성화 실패", ex);
            }
        }

        private void DisableSleepPrevention(string reason)
        {
            if (!_sleepPreventionActive)
            {
                return;
            }

            try
            {
                SetThreadExecutionState(EsContinuous);
                _sleepPreventionActive = false;
                DiagnosticsLog.WriteEvent("절전 방지 해제: " + (reason ?? ""));
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("절전 방지 해제 실패", ex);
            }
        }

        private void TrySubscribe()
        {
            try
            {
                SystemEvents.PowerModeChanged += SystemEventsPowerModeChanged;
                _subscribed = true;
                DiagnosticsLog.WriteEvent("전원 상태 감지 시작");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("전원 상태 감지 시작 실패", ex);
            }
        }

        private void SystemEventsPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            try
            {
                DiagnosticsLog.WriteEvent("전원 상태 변경: " + FormatPowerMode(e.Mode));
                if (_powerModeChanged != null)
                {
                    _powerModeChanged(e.Mode);
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("전원 상태 변경 처리 실패", ex);
            }
        }

        private static string FormatPowerMode(PowerModes mode)
        {
            switch (mode)
            {
                case PowerModes.Suspend:
                    return "절전 진입";
                case PowerModes.Resume:
                    return "절전 복귀";
                case PowerModes.StatusChange:
                    return "전원 상태 갱신";
                default:
                    return mode.ToString();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisableSleepPrevention("앱 종료");

            if (!_subscribed)
            {
                return;
            }

            try
            {
                SystemEvents.PowerModeChanged -= SystemEventsPowerModeChanged;
                _subscribed = false;
                DiagnosticsLog.WriteEvent("전원 상태 감지 종료");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("전원 상태 감지 종료 실패", ex);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);
    }
}
