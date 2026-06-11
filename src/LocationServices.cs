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
    internal static class LocationLocator
    {
        public static LocationReadResult GetCurrentLocation()
        {
            LocationReadResult result = new LocationReadResult { WasRequested = true, Error = "" };

            string script = @"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Devices.Geolocation.Geolocator,Windows.Devices.Geolocation,ContentType=WindowsRuntime] | Out-Null

function Await-WinRtOperation($operation, [Type]$resultType) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and
            $_.IsGenericMethodDefinition -and
            $_.GetParameters().Length -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        } |
        Select-Object -First 1

    if ($null -eq $method) {
        throw 'Windows 위치 API helper를 찾을 수 없습니다.'
    }

    $task = $method.MakeGenericMethod($resultType).Invoke($null, @($operation))
    $task.Wait() | Out-Null
    return $task.Result
}

try {
    $accessOperation = [Windows.Devices.Geolocation.Geolocator]::RequestAccessAsync()
    $access = Await-WinRtOperation $accessOperation ([Windows.Devices.Geolocation.GeolocationAccessStatus])
    if ($access -ne [Windows.Devices.Geolocation.GeolocationAccessStatus]::Allowed) {
        throw ('Windows 위치 권한 상태: ' + $access + '. Windows 설정에서 위치 서비스와 데스크톱 앱 위치 접근을 켜세요.')
    }
} catch [System.Management.Automation.MethodException] {
    # Older Windows builds do not expose RequestAccessAsync; GetGeopositionAsync will report availability.
}

$locator = New-Object Windows.Devices.Geolocation.Geolocator
$locator.DesiredAccuracy = [Windows.Devices.Geolocation.PositionAccuracy]::Default
$position = Await-WinRtOperation $locator.GetGeopositionAsync() ([Windows.Devices.Geolocation.Geoposition])
$basic = $position.Coordinate.Point.Position
$accuracy = $position.Coordinate.Accuracy
$culture = [Globalization.CultureInfo]::InvariantCulture
[Console]::WriteLine(('{0}|{1}|{2}' -f $basic.Latitude.ToString($culture), $basic.Longitude.ToString($culture), $accuracy.ToString($culture)))
";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            CommandResult command = CommandRunner.Run(
                powershellPath,
                "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                25000);

            if (command.TimedOut)
            {
                result.Error = "위치 요청 시간이 초과되었습니다.";
                return result;
            }

            if (command.ExitCode != 0)
            {
                result.Error = FirstNonEmpty(command.Error, command.Output, "위치 명령 실행에 실패했습니다.");
                DiagnosticsLog.WriteEvent("위치 명령 실패: " + result.Error
                    + " / raw=" + FirstNonEmptyRaw(command.Error, command.Output));
                return result;
            }

            string line = FirstNonEmpty(command.Output, "", "");
            string[] parts = line.Split('|');
            if (parts.Length < 2)
            {
                result.Error = "위치 명령 결과 형식이 예상과 다릅니다.";
                return result;
            }

            double latitude;
            double longitude;
            double accuracy;
            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                result.Error = "위치 좌표를 해석할 수 없습니다.";
                return result;
            }

            if (parts.Length < 3 || !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out accuracy))
            {
                accuracy = 0;
            }

            result.Location = new LocationInfo
            {
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracy
            };
            return result;
        }

        private static string FirstNonEmpty(string first, string second, string fallback)
        {
            string line = FirstLine(first);
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            line = FirstLine(second);
            return string.IsNullOrWhiteSpace(line) ? fallback : line;
        }

        private static string FirstLine(string text)
        {
            return CommandOutputFormatter.FirstMeaningfulLine(text, 240);
        }

        private static string FirstNonEmptyRaw(string first, string second)
        {
            string line = CommandOutputFormatter.FirstRawLines(first, 2, 260);
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            line = CommandOutputFormatter.FirstRawLines(second, 2, 260);
            return string.IsNullOrWhiteSpace(line) ? "없음" : line;
        }
    }

    internal static class GeoMath
    {
        public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            const double earthRadiusMeters = 6371000.0;
            double lat1 = ToRadians(latitude1);
            double lat2 = ToRadians(latitude2);
            double deltaLat = ToRadians(latitude2 - latitude1);
            double deltaLon = ToRadians(longitude2 - longitude1);

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusMeters * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }

    internal static class WifiLocator
    {
        public static List<WifiNetwork> GetVisibleNetworks(bool forceScan)
        {
            IntPtr handle = IntPtr.Zero;
            IntPtr interfacesPtr = IntPtr.Zero;
            List<WifiNetwork> networks = new List<WifiNetwork>();
            Dictionary<string, WifiNetwork> bestBySsid = new Dictionary<string, WifiNetwork>(StringComparer.OrdinalIgnoreCase);

            try
            {
                uint negotiated;
                int result = NativeMethods.WlanOpenHandle(2, IntPtr.Zero, out negotiated, out handle);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                result = NativeMethods.WlanEnumInterfaces(handle, IntPtr.Zero, out interfacesPtr);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                int interfaceCount = Marshal.ReadInt32(interfacesPtr, 0);
                long listIterator = interfacesPtr.ToInt64() + 8;
                int interfaceSize = Marshal.SizeOf(typeof(NativeMethods.WLAN_INTERFACE_INFO));

                List<NativeMethods.WLAN_INTERFACE_INFO> interfaces = new List<NativeMethods.WLAN_INTERFACE_INFO>();
                for (int i = 0; i < interfaceCount; i++)
                {
                    IntPtr itemPtr = new IntPtr(listIterator + (i * interfaceSize));
                    NativeMethods.WLAN_INTERFACE_INFO wlanInterface =
                        (NativeMethods.WLAN_INTERFACE_INFO)Marshal.PtrToStructure(itemPtr, typeof(NativeMethods.WLAN_INTERFACE_INFO));
                    interfaces.Add(wlanInterface);
                }

                if (forceScan)
                {
                    foreach (NativeMethods.WLAN_INTERFACE_INFO wlanInterface in interfaces)
                    {
                        Guid interfaceGuid = wlanInterface.InterfaceGuid;
                        NativeMethods.WlanScan(handle, ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    }

                    Thread.Sleep(1300);
                }

                foreach (NativeMethods.WLAN_INTERFACE_INFO wlanInterface in interfaces)
                {
                    IntPtr networksPtr = IntPtr.Zero;
                    try
                    {
                        Guid interfaceGuid = wlanInterface.InterfaceGuid;
                        result = NativeMethods.WlanGetAvailableNetworkList(handle, ref interfaceGuid, 0, IntPtr.Zero, out networksPtr);
                        if (result != 0)
                        {
                            continue;
                        }

                        int networkCount = Marshal.ReadInt32(networksPtr, 0);
                        long networkIterator = networksPtr.ToInt64() + 8;
                        int networkSize = Marshal.SizeOf(typeof(NativeMethods.WLAN_AVAILABLE_NETWORK));

                        for (int i = 0; i < networkCount; i++)
                        {
                            IntPtr itemPtr = new IntPtr(networkIterator + (i * networkSize));
                            NativeMethods.WLAN_AVAILABLE_NETWORK network =
                                (NativeMethods.WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(itemPtr, typeof(NativeMethods.WLAN_AVAILABLE_NETWORK));

                            string ssid = NativeMethods.SsidToString(network.dot11Ssid);
                            if (string.IsNullOrWhiteSpace(ssid))
                            {
                                continue;
                            }

                            WifiNetwork visible = new WifiNetwork
                            {
                                Ssid = ssid,
                                ProfileName = network.strProfileName ?? "",
                                SignalQuality = Convert.ToInt32(Math.Min(100, network.wlanSignalQuality)),
                                Connectable = network.bNetworkConnectable
                            };

                            WifiNetwork existing;
                            if (!bestBySsid.TryGetValue(visible.Ssid, out existing) || existing.SignalQuality < visible.SignalQuality)
                            {
                                bestBySsid[visible.Ssid] = visible;
                            }
                        }
                    }
                    finally
                    {
                        if (networksPtr != IntPtr.Zero)
                        {
                            NativeMethods.WlanFreeMemory(networksPtr);
                        }
                    }
                }
            }
            finally
            {
                if (interfacesPtr != IntPtr.Zero)
                {
                    NativeMethods.WlanFreeMemory(interfacesPtr);
                }

                if (handle != IntPtr.Zero)
                {
                    NativeMethods.WlanCloseHandle(handle, IntPtr.Zero);
                }
            }

            networks.AddRange(bestBySsid.Values);
            return networks;
        }
    }
}
