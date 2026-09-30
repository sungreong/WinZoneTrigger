using System;
using System.Collections.Generic;

namespace WinZoneTrigger
{
    internal static class AutomationFeatureTests
    {
        private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
        public static void Run()
        {
            var z = ZoneRule.CreateDefault("test");
            z.ScheduleEnabled = true; z.ScheduleDays = 1 << 5;
            z.ScheduleStartMinute = 22 * 60; z.ScheduleEndMinute = 7 * 60;
            var friday = new DateTime(2026, 10, 2, 22, 0, 0);
            Check(ZoneSchedule.Allows(z, friday), "Friday window opens inclusively");
            Check(!ZoneSchedule.Allows(z, friday.AddMinutes(-1)), "before start rejected");
            Check(ZoneSchedule.Allows(z, friday.AddHours(8)), "overnight belongs to previous day");
            Check(!ZoneSchedule.Allows(z, friday.AddHours(9)), "end exclusive");
            Check(!ZoneSchedule.Allows(z, friday.AddDays(1)), "Saturday night not selected");
            z.ScheduleStartMinute = z.ScheduleEndMinute = 0;
            Check(ZoneSchedule.Allows(z, friday), "equal times full selected day");
            Check(!ZoneSchedule.Allows(z, friday.AddDays(1)), "full day obeys weekday");
            z.ScheduleStartMinute = 540; z.ScheduleEndMinute = 1080;
            Check(ZoneSchedule.Allows(z, friday.Date.AddHours(9)), "daytime start");
            Check(!ZoneSchedule.Allows(z, friday.Date.AddHours(18)), "daytime end");
            z.ScheduleEnabled = false;
            Check(ZoneSchedule.Allows(z, friday), "legacy unrestricted schedule");

            var config = AppConfig.CreateDefault(); config.Normalize();
            var original = new AudioSnapshot { DeviceId = "device", Volume = .56f, Muted = false };
            AudioSnapshot current = original;
            int writes = 0;
            Func<AudioSnapshot> read = () => current;
            Action<AudioSnapshot> write = value => { writes++; current = value; };
            Action<string> log = delegate { };
            var state = new AudioAutomationState { Layers = new List<AudioLayer>() };
            z.AudioAction = "Volume"; z.VolumePercent = 20; z.RestoreAudioOnExit = true;
            AudioAutomation.ApplyCore(state, z, config, read, write, friday, log);
            Check(Math.Abs(current.Volume - .2f) < .001 && !current.Muted, "volume preset");
            AudioAutomation.ApplyCore(state, z, config, read, write, friday, log);
            AudioAutomation.ReconcileCore(state, new string[0], config, read, write, friday, log);
            Check(AudioSnapshot.Same(current, original), "repeat run preserves original baseline");
            AudioAutomation.ApplyCore(state, z, config, read, write, friday, log);
            var other = z.Clone(); other.Id = "other"; other.AudioAction = "Mute";
            AudioAutomation.ApplyCore(state, other, config, read, write, friday, log);
            AudioAutomation.ReconcileCore(state, new[] {other.Id}, config, read, write, friday, log);
            Check(current.Muted, "exiting lower overlapping location preserves top");
            AudioAutomation.ReconcileCore(state, new string[0], config, read, write, friday, log);
            Check(AudioSnapshot.Same(current, original), "overlap unwinds to initial state");
            AudioAutomation.ApplyCore(state, z, config, read, write, friday, log);
            current = new AudioSnapshot { DeviceId = "device", Volume = .35f, Muted = true };
            int before = writes;
            AudioAutomation.ReconcileCore(state, new string[0], config, read, write, friday, log);
            Check(writes == before && state.Layers.Count == 0, "manual edit never restored over");
            AudioAutomation.ApplyCore(state, z, config, read, write, friday.AddMinutes(59), log);
            Check(writes == before, "manual hold prevents new audio action");
            AudioAutomation.ApplyCore(state, z, config, read, write, friday.AddMinutes(60), log);
            Check(writes == before + 1, "hold expires at boundary");
            current = new AudioSnapshot { DeviceId = "headphones", Volume = .2f, Muted = false };
            before = writes;
            AudioAutomation.ReconcileCore(state, new string[0], config, read, write, friday.AddMinutes(61), log);
            Check(writes == before, "device switch never restores to other endpoint");
            state.HoldUntilUtc = DateTime.MinValue;
            z.RestoreAudioOnExit = false;
            AudioAutomation.ApplyCore(state, z, config, read, write, friday, log);
            before = writes;
            AudioAutomation.ReconcileCore(state, new string[0], config, read, write, friday, log);
            Check(writes == before, "restore opt out respected");
            Check(!BrightnessOwnership.Same(new Dictionary<string,int>{{"screen",70}}, new Dictionary<string,int>{{"screen",50}}), "brightness manual edit detected");
            Check(!BrightnessOwnership.Same(new Dictionary<string,int>{{"screen",70}}, new Dictionary<string,int>{{"other",70}}), "brightness device identity respected");
        }
    }
}
