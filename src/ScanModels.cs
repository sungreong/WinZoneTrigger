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
    internal sealed class WifiNetwork
    {
        public string Ssid { get; set; }
        public string ProfileName { get; set; }
        public int SignalQuality { get; set; }
        public bool Connectable { get; set; }
    }

    internal sealed class AppSearchCandidate
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public string AppId { get; set; }
        public string Source { get; set; }
        public string SearchText { get; set; }

        public override string ToString()
        {
            string name = string.IsNullOrWhiteSpace(Name) ? "이름 없는 앱" : Name;
            return string.IsNullOrWhiteSpace(Source) ? name : name + " · " + Source;
        }
    }

    internal sealed class ScanSnapshot
    {
        public List<WifiNetwork> Networks { get; set; }
        public string WifiError { get; set; }
        public LocationReadResult LocationResult { get; set; }
    }

    internal sealed class ScanContext
    {
        public HashSet<string> VisibleSsids { get; set; }
        public LocationInfo CurrentLocation { get; set; }
        public List<WifiNetwork> VisibleNetworks { get; set; }
    }

    internal static class ScanReliability
    {
        public static bool HasTransientDetectionError(ScanSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
            {
                return true;
            }

            LocationReadResult location = snapshot.LocationResult;
            return location != null
                && location.WasRequested
                && !location.HasLocation
                && !string.IsNullOrWhiteSpace(location.Error);
        }
    }

    internal sealed class LocationInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AccuracyMeters { get; set; }
    }

    internal sealed class LocationReadResult
    {
        public bool WasRequested { get; set; }
        public LocationInfo Location { get; set; }
        public string Error { get; set; }

        public bool HasLocation
        {
            get { return Location != null; }
        }

        public static LocationReadResult NotRequested()
        {
            return new LocationReadResult
            {
                WasRequested = false,
                Location = null,
                Error = ""
            };
        }
    }

}
