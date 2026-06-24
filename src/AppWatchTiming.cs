using System;

namespace WinZoneTrigger
{
    internal static class AppWatchTiming
    {
        private const int DefaultIntervalMilliseconds = 5 * 60 * 1000;
        private const int MaximumGuardGapMilliseconds = 30 * 1000;

        public static int GetIntervalMilliseconds(int value, string unit)
        {
            long multiplier = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 3600000L : 60000L;
            long milliseconds = Math.Max(1, value) * multiplier;
            return milliseconds > int.MaxValue ? int.MaxValue : Convert.ToInt32(milliseconds);
        }

        public static int GetGuardIntervalMilliseconds(int value, string unit)
        {
            return Math.Min(GetIntervalMilliseconds(value, unit), MaximumGuardGapMilliseconds);
        }

        public static int DefaultGuardIntervalMilliseconds
        {
            get { return Math.Min(DefaultIntervalMilliseconds, MaximumGuardGapMilliseconds); }
        }
    }
}
