using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace WinZoneTrigger
{
    internal sealed class WifiRadio
    {
        public Guid InterfaceId;
        public uint PhyIndex;
        public int SoftwareState;
        public int HardwareState;
    }

    internal interface IWifiRadio : IDisposable
    {
        List<WifiRadio> Read();
        void TurnOn(WifiRadio radio);
    }

    internal sealed class WifiRadioResult
    {
        public string Status = "ready";
        public string Message = "";
        public bool Ready { get { return Status == "ready"; } }
    }

    internal static class WifiRadioRecovery
    {
        public static bool Allowed(AppConfig config)
        {
            return !config.IsAutomationPaused() && config.Zones.Any(z => z.Enabled && z.WifiRecoveryEnabled
                && ZoneSchedule.Allows(z, DateTime.Now) && !string.IsNullOrWhiteSpace(z.ConnectSsid)
                && !string.IsNullOrWhiteSpace(z.ConnectProfile));
        }

        // Radio power must precede location discovery. Connecting still requires a fresh location match.
        public static WifiRadioResult Prepare(IWifiRadio adapter, Func<bool> stillAllowed)
        {
            try
            {
                if (!stillAllowed()) return Result("cancelled", "자동 복구가 꺼졌거나 실행 시간 밖이어서 Wi-Fi 전원을 변경하지 않습니다.");
                List<WifiRadio> radios = adapter.Read();
                if (radios.Any(r => r.SoftwareState == 1 && r.HardwareState == 1)) return new WifiRadioResult();
                if (radios.Count == 0) return Result("radio-unavailable", "Wi-Fi 장치를 찾지 못했습니다. Windows에서 무선 어댑터가 사용 설정되어 있는지 확인하세요.");
                bool changed = false;
                foreach (WifiRadio radio in radios.Where(r => r.SoftwareState == 2 && r.HardwareState == 1))
                {
                    if (!stillAllowed()) return Result("cancelled", "설정이 바뀌어 Wi-Fi 전원 복구를 중단했습니다.");
                    adapter.TurnOn(radio);
                    changed = true;
                }
                if (changed) return Result("radio-starting", "꺼진 Wi-Fi 전원을 켜도록 요청했습니다. 다음 확인에서 위치와 연결 대상을 다시 검색합니다.");
                if (radios.Any(r => r.HardwareState == 2)) return Result("radio-blocked", "Wi-Fi가 하드웨어에서 꺼져 있습니다. 무선 스위치·비행기 모드 상태를 확인하세요.");
                return Result("radio-unavailable", "Wi-Fi 전원 상태를 확인하지 못했습니다. Windows의 무선 어댑터 상태를 확인하세요.");
            }
            catch (Exception ex) { return Result("radio-error", "Wi-Fi 전원 확인·복구 실패: " + ex.Message); }
        }

        private static WifiRadioResult Result(string status, string message)
        {
            return new WifiRadioResult { Status = status, Message = message };
        }
    }

    // Native WLAN software radio control; never changes hardware switches or disables an adapter.
    internal sealed class NativeWifiRadio : IWifiRadio
    {
        private IntPtr _handle;

        private void Open()
        {
            if (_handle != IntPtr.Zero) return;
            uint version;
            Check(NativeMethods.WlanOpenHandle(2, IntPtr.Zero, out version, out _handle));
        }

        public List<WifiRadio> Read()
        {
            Open();
            IntPtr list = IntPtr.Zero;
            var radios = new List<WifiRadio>();
            int firstError = 0;
            try
            {
                Check(NativeMethods.WlanEnumInterfaces(_handle, IntPtr.Zero, out list));
                int count = Marshal.ReadInt32(list);
                int size = Marshal.SizeOf(typeof(NativeMethods.WLAN_INTERFACE_INFO));
                for (int i = 0; i < count; i++)
                {
                    var info = (NativeMethods.WLAN_INTERFACE_INFO)Marshal.PtrToStructure(IntPtr.Add(list, 8 + i * size), typeof(NativeMethods.WLAN_INTERFACE_INFO));
                    IntPtr data = IntPtr.Zero;
                    try
                    {
                        int bytes, valueType;
                        Guid id = info.InterfaceGuid;
                        int error = WlanQueryInterface(_handle, ref id, 4, IntPtr.Zero, out bytes, out data, out valueType);
                        if (error != 0) { firstError = firstError == 0 ? error : firstError; continue; }
                        if (bytes < 4) throw new InvalidOperationException("무선 전원 응답이 올바르지 않습니다.");
                        int phys = Marshal.ReadInt32(data);
                        if (phys < 0 || phys > 64 || bytes < 4 + phys * 12) throw new InvalidOperationException("무선 전원 응답 길이가 올바르지 않습니다.");
                        for (int p = 0; p < phys; p++)
                        {
                            var state = (PhyRadioState)Marshal.PtrToStructure(IntPtr.Add(data, 4 + p * 12), typeof(PhyRadioState));
                            radios.Add(new WifiRadio { InterfaceId = id, PhyIndex = state.Index, SoftwareState = state.Software, HardwareState = state.Hardware });
                        }
                    }
                    finally { if (data != IntPtr.Zero) NativeMethods.WlanFreeMemory(data); }
                }
                if (radios.Count == 0 && firstError != 0) Check(firstError);
                return radios;
            }
            finally { if (list != IntPtr.Zero) NativeMethods.WlanFreeMemory(list); }
        }

        public void TurnOn(WifiRadio radio)
        {
            Open();
            Guid id = radio.InterfaceId;
            var state = new PhyRadioState { Index = radio.PhyIndex, Software = 1 };
            Check(WlanSetInterface(_handle, ref id, 4, (uint)Marshal.SizeOf(typeof(PhyRadioState)), ref state, IntPtr.Zero));
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero) NativeMethods.WlanCloseHandle(_handle, IntPtr.Zero);
            _handle = IntPtr.Zero;
        }

        private static void Check(int error) { if (error != 0) throw new Win32Exception(error); }
        [StructLayout(LayoutKind.Sequential)]
        private struct PhyRadioState { public uint Index; public int Software; public int Hardware; }
        [DllImport("wlanapi.dll")]
        private static extern int WlanQueryInterface(IntPtr handle, ref Guid id, int opcode, IntPtr reserved, out int size, out IntPtr data, out int valueType);
        [DllImport("wlanapi.dll")]
        private static extern int WlanSetInterface(IntPtr handle, ref Guid id, int opcode, uint size, ref PhyRadioState data, IntPtr reserved);
    }
}
