using System;
using System.Collections.Generic;
using System.Linq;

namespace WinZoneTrigger
{
    internal sealed partial class BackgroundAutomationContext
    {
        private bool ZoneMatches(ZoneRule zone, HashSet<string> visibleSsids, LocationInfo currentLocation)
        {
            return AnalyzeZoneMatch(zone, visibleSsids, currentLocation, false).Matches;
        }

        private ZoneMatchResult AnalyzeZoneMatch(ZoneRule zone, HashSet<string> visibleSsids, LocationInfo currentLocation, bool startupOnly)
        {
            bool coordinateMatch = false;
            if (zone.UseCoordinates)
            {
                coordinateMatch = currentLocation != null
                    && GeoMath.DistanceMeters(
                        currentLocation.Latitude,
                        currentLocation.Longitude,
                        zone.Latitude,
                        zone.Longitude) <= zone.RadiusMeters;
            }

            List<string> wanted = zone.NearbySsids
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool wifiMatch = false;
            if (zone.UseWifiCondition.GetValueOrDefault(false) && wanted.Count > 0)
            {
                wifiMatch = zone.RequireAllSsids
                    ? wanted.All(visibleSsids.Contains)
                    : wanted.Any(visibleSsids.Contains);
            }

            bool coordinateOnlyStartupWifiAction = startupOnly
                && zone.ConnectWifiEnabled.GetValueOrDefault(false)
                && zone.UseWifiCondition.GetValueOrDefault(false)
                && wanted.Count > 0
                && coordinateMatch
                && !wifiMatch;

            List<string> reasons = new List<string>();
            if (zone.UseCoordinates)
            {
                reasons.Add(coordinateMatch ? "좌표 일치" : "좌표 불일치");
            }
            if (zone.UseWifiCondition.GetValueOrDefault(false))
            {
                List<string> missing = wanted.Where(s => !visibleSsids.Contains(s)).ToList();
                reasons.Add(wifiMatch
                    ? "Wi-Fi 일치(" + string.Join(", ", wanted.ToArray()) + ")"
                    : "Wi-Fi 미감지(" + string.Join(", ", missing.ToArray()) + ")");
            }
            if (coordinateOnlyStartupWifiAction)
            {
                reasons.Add("부팅 초기 Wi-Fi 연결 동작은 Wi-Fi 확인 전까지 대기");
            }
            if (reasons.Count == 0)
            {
                reasons.Add("조건 없음");
            }

            return new ZoneMatchResult
            {
                Matches = (coordinateMatch || wifiMatch) && !coordinateOnlyStartupWifiAction,
                CoordinateMatch = coordinateMatch,
                WifiMatch = wifiMatch,
                DeferredForStartupWifi = coordinateOnlyStartupWifiAction,
                Reason = string.Join("; ", reasons.ToArray())
            };
        }

        private void LogConflictingWifiActions(List<ZoneRule> zones)
        {
            List<string> targets = zones
                .Where(z => z != null && z.ConnectWifiEnabled.GetValueOrDefault(false))
                .Select(z => string.IsNullOrWhiteSpace(z.ConnectSsid) ? z.ConnectProfile : z.ConnectSsid)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (targets.Count > 1)
            {
                DiagnosticsLog.WriteEvent("Wi-Fi 연결 동작 충돌 감지: 같은 scan에서 서로 다른 대상 요청="
                    + string.Join(", ", targets.ToArray())
                    + " / 동작은 큐에서 순차 실행됩니다.");
            }
        }

        private static string FormatVisibleSsids(HashSet<string> visibleSsids)
        {
            if (visibleSsids == null || visibleSsids.Count == 0)
            {
                return "없음";
            }

            return string.Join(", ", visibleSsids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).Take(8).ToArray());
        }

        private static string FormatLocationForLog(LocationInfo location)
        {
            if (location == null)
            {
                return "없음";
            }

            return location.Latitude.ToString("0.######") + ","
                + location.Longitude.ToString("0.######")
                + " accuracy=" + location.AccuracyMeters.ToString("0") + "m";
        }

        private bool IsZoneActive(ZoneRule zone)
        {
            bool active;
            return zone != null
                && !string.IsNullOrWhiteSpace(zone.Id)
                && _insideZones.TryGetValue(zone.Id, out active)
                && active;
        }

        private bool AllStartupRunOnceZonesTriggered()
        {
            List<ZoneRule> startupZones = _config.Zones
                .Where(z => z.Enabled && z.RunOnceAtStartup.GetValueOrDefault(true))
                .ToList();
            return startupZones.Count > 0
                && startupZones.All(z => !string.IsNullOrWhiteSpace(z.Id) && _startupTriggeredZoneIds.Contains(z.Id));
        }
    }
}
