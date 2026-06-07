using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace WinZoneTrigger
{
    internal static class ScanHelper
    {
        public static int Run(string[] args)
        {
            string outputPath = GetArgumentValue(args, "--out");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return 2;
            }

            bool forceScan = HasArgument(args, "--force");
            bool requestLocation = HasArgument(args, "--location");

            ScanSnapshot snapshot = new ScanSnapshot();
            try
            {
                try
                {
                    snapshot.Networks = WifiLocator.GetVisibleNetworks(forceScan);
                }
                catch (Exception ex)
                {
                    snapshot.Networks = new System.Collections.Generic.List<WifiNetwork>();
                    snapshot.WifiError = ex.Message;
                }

                snapshot.LocationResult = requestLocation
                    ? LocationLocator.GetCurrentLocation()
                    : LocationReadResult.NotRequested();

                string json = new JavaScriptSerializer().Serialize(snapshot);
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(outputPath, json, Encoding.UTF8);
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    snapshot.Networks = snapshot.Networks ?? new System.Collections.Generic.List<WifiNetwork>();
                    snapshot.WifiError = FirstNonEmpty(snapshot.WifiError, ex.Message, "탐지 헬퍼 오류");
                    snapshot.LocationResult = snapshot.LocationResult ?? new LocationReadResult
                    {
                        WasRequested = requestLocation,
                        Error = ex.Message
                    };
                    File.WriteAllText(outputPath, new JavaScriptSerializer().Serialize(snapshot), Encoding.UTF8);
                }
                catch
                {
                }

                return 1;
            }
        }

        private static bool HasArgument(string[] args, string value)
        {
            return args != null && args.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            if (args == null)
            {
                return "";
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1] ?? "";
                }
            }

            return "";
        }

        private static string FirstNonEmpty(string first, string second, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
            if (!string.IsNullOrWhiteSpace(second))
            {
                return second;
            }
            return fallback;
        }
    }
}
