using System;
using System.Runtime.InteropServices;

namespace WinZoneTrigger
{
    internal static class NetworkHealth
    {
        // Read Windows' existing connectivity assessment; do not send probes or change networking.
        public static void Read(WifiRecoveryState state)
        {
            object manager = null;
            try
            {
                Type type = Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
                manager = Activator.CreateInstance(type);
                bool internet = (bool)type.InvokeMember("IsConnectedToInternet",
                    System.Reflection.BindingFlags.GetProperty, null, manager, null);
                state.InternetStatus = internet ? "internet" : "no-internet";
                state.InternetMessage = internet ? "Windows: PC 인터넷 연결 확인됨" : "Windows: PC 인터넷 연결 없음";
                if (string.IsNullOrEmpty(state.ConnectedSsid))
                    state.InternetMessage += " · Wi-Fi 연결 없음";
                else if (!internet) state.InternetMessage += " · Wi-Fi는 연결됨 (공유기·회선·인증 확인)";
            }
            catch (Exception)
            {
                state.InternetStatus = "unknown";
                state.InternetMessage = "인터넷 상태를 확인할 수 없습니다.";
            }
            finally { if (manager != null && Marshal.IsComObject(manager)) Marshal.ReleaseComObject(manager); }
        }
    }
}
