using System;

namespace WinZoneTrigger
{
    internal static class ZoneSchedule
    {
        // Days refer to the day a window STARTS. Equal start/end means a full day.
        public static bool Allows(ZoneRule zone, DateTime local)
        {
            if (!zone.ScheduleEnabled) return true;
            int minute = local.Hour * 60 + local.Minute;
            int start = zone.ScheduleStartMinute, end = zone.ScheduleEndMinute;
            DateTime day = local;
            bool inWindow;
            if (start == end) inWindow = true;
            else if (start < end) inWindow = minute >= start && minute < end;
            else
            {
                inWindow = minute >= start || minute < end;
                if (minute < end) day = day.AddDays(-1);
            }
            return inWindow && (zone.ScheduleDays & (1 << (int)day.DayOfWeek)) != 0;
        }
    }
}
