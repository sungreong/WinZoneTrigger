using System;
using System.Collections.Generic;
using System.Linq;

namespace WinZoneTrigger
{
    // Pure selection policy: never use the sticky 'inside zone' state to reconnect.
    internal static class WifiRecoveryPolicy
    {
        public static ZoneRule Select(IEnumerable<ZoneRule> zones, IList<WifiNetwork> networks, LocationInfo location)
        {
            return zones.Where(z => z.Enabled && z.WifiRecoveryEnabled && ZoneSchedule.Allows(z, DateTime.Now)
                    && !string.IsNullOrWhiteSpace(z.ConnectProfile)
                    && !string.IsNullOrWhiteSpace(z.ConnectSsid)
                    && networks.Any(n => n.Ssid == z.ConnectSsid && n.Connectable)
                    && Matches(z, networks, location))
                .OrderBy(z => z.WifiPriority)
                .ThenByDescending(z => networks.Any(n => n.Ssid == z.ConnectSsid && n.Connected))
                .ThenBy(z => z.Id, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public static bool Matches(ZoneRule zone, IList<WifiNetwork> networks, LocationInfo location)
        {
            bool coordinates = zone.UseCoordinates && location != null && GeoMath.DistanceMeters(
                location.Latitude, location.Longitude, zone.Latitude, zone.Longitude) <= zone.RadiusMeters;
            List<string> wanted = zone.NearbySsids ?? new List<string>();
            bool wifi = zone.UseWifiCondition.GetValueOrDefault(false) && wanted.Count > 0
                && (zone.RequireAllSsids ? wanted.All(s => networks.Any(n => n.Ssid == s))
                    : wanted.Any(s => networks.Any(n => n.Ssid == s)));
            return coordinates || wifi;
        }

        public static int RetrySeconds(int interval, int failures)
        {
            return Math.Min(3600, Math.Max(30, interval) * (1 << Math.Min(4, Math.Max(0, failures))));
        }

        public static bool AlreadyConnected(IEnumerable<WifiNetwork> networks, string target)
        {
            return networks.Any(n => n.Connected && n.Ssid == target);
        }
    }

    internal sealed class WifiRecoveryState
    {
        public DateTime CheckedAt { get; set; }
        public string InternetStatus { get; set; }
        public string InternetMessage { get; set; }
        public DateTime? DisconnectedSince { get; set; }
        public DateTime? LastRecoveredAt { get; set; }
        public DateTime? NextCheckAt { get; set; }
        public string ZoneId { get; set; }
        public string TargetSsid { get; set; }
        public string ConnectedSsid { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}
