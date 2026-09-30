using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WinZoneTrigger
{
    internal sealed partial class BackgroundAutomationContext
    {
        private void SaveAutomationState(
            List<string> activeZoneIds,
            List<string> activeZoneNames,
            ScanSnapshot snapshot,
            HashSet<string> visibleSsids,
            LocationInfo currentLocation,
            string eventText)
        {
            LocationReadResult locationResult = snapshot == null ? null : snapshot.LocationResult;
            lock (_automationStateLock)
            {
                _stateActiveZoneIds = activeZoneIds ?? new List<string>();
                _stateActiveZoneNames = activeZoneNames ?? new List<string>();
                _stateCurrentLocation = currentLocation;
                _stateLocationWasRequested = locationResult != null && locationResult.WasRequested;
                _stateLocationError = locationResult == null ? "" : locationResult.Error;
                _stateVisibleSsids = visibleSsids == null
                    ? new List<string>()
                    : visibleSsids.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
                _stateWifiError = snapshot == null ? "" : snapshot.WifiError;
                SetLastEventLocked(eventText);
                SaveAutomationStateLocked();
            }
        }

        private void UpdateAutomationEvent(string eventText, string actionText, string appWatchText)
        {
            UpdateAutomationEvent(eventText, actionText, appWatchText, "", "", "");
        }

        private void UpdateAutomationEvent(
            string eventText,
            string actionText,
            string appWatchText,
            string appWatchZoneId,
            string appWatchItemId,
            string appWatchItemText)
        {
            lock (_automationStateLock)
            {
                SetLastEventLocked(eventText);
                if (!string.IsNullOrWhiteSpace(actionText))
                {
                    _stateLastActionText = actionText;
                }

                if (!string.IsNullOrWhiteSpace(appWatchText))
                {
                    _stateLastAppWatchText = appWatchText;
                }

                if (!string.IsNullOrWhiteSpace(appWatchZoneId) && !string.IsNullOrWhiteSpace(appWatchItemId))
                {
                    _stateLastAppWatchZoneId = appWatchZoneId;
                    _stateLastAppWatchItemId = appWatchItemId;
                    _stateLastAppWatchItemText = appWatchItemText ?? "";
                }

                SaveAutomationStateLocked();
            }
        }

        private void SetLastEventLocked(string eventText)
        {
            if (string.IsNullOrWhiteSpace(eventText))
            {
                return;
            }

            _stateLastEventAtLocal = DateTime.Now;
            _stateLastEventText = eventText;
        }

        private void SaveAutomationStateLocked()
        {
            AutomationStateStore.Save(new AutomationStateSnapshot
            {
                UpdatedAtLocal = DateTime.Now,
                ProcessId = Process.GetCurrentProcess().Id,
                ActiveZoneIds = new List<string>(_stateActiveZoneIds),
                ActiveZoneNames = new List<string>(_stateActiveZoneNames),
                CurrentLocation = _stateCurrentLocation,
                LocationWasRequested = _stateLocationWasRequested,
                LocationError = _stateLocationError,
                VisibleSsids = new List<string>(_stateVisibleSsids),
                WifiError = _stateWifiError,
                LastEventAtLocal = _stateLastEventAtLocal,
                LastEventText = _stateLastEventText,
                LastActionText = _stateLastActionText,
                LastAppWatchText = _stateLastAppWatchText,
                LastAppWatchZoneId = _stateLastAppWatchZoneId,
                LastAppWatchItemId = _stateLastAppWatchItemId,
                LastAppWatchItemText = _stateLastAppWatchItemText
            });
        }

        private static string BuildAppWatchStatusText(string summary, DateTime checkedAtLocal, DateTime nextCheckAtLocal)
        {
            return "확인 " + checkedAtLocal.ToString("yyyy-MM-dd HH:mm:ss")
                + " · 다음 앱 확인 " + nextCheckAtLocal.ToString("yyyy-MM-dd HH:mm:ss")
                + " · " + (summary ?? "");
        }

    }
}
