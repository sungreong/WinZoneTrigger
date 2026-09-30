using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;

namespace WinZoneTrigger
{
    internal sealed class AudioSnapshot
    {
        public string DeviceId { get; set; }
        public float Volume { get; set; }
        public bool Muted { get; set; }
        public static bool Same(AudioSnapshot a, AudioSnapshot b)
        {
            return a != null && b != null && a.DeviceId == b.DeviceId
                && a.Muted == b.Muted && Math.Abs(a.Volume - b.Volume) < 0.005f;
        }
    }
    internal sealed class AudioLayer
    {
        public string ZoneId { get; set; }
        public bool Restore { get; set; }
        public AudioSnapshot Before { get; set; }
        public AudioSnapshot After { get; set; }
    }
    internal sealed class AudioAutomationState
    {
        public List<AudioLayer> Layers { get; set; }
        public DateTime HoldUntilUtc { get; set; }
        public string Message { get; set; }
    }
    internal static class AudioAutomation
    {
        private static readonly string StatePath = Path.Combine(ConfigStore.ConfigDirectory, "audio-state.json");
        // Persistent state and a named lock also cover the legacy editor's execution helper.
        private static void WithState(Action<AudioAutomationState> action)
        {
            using (var gate = new Mutex(false, @"Local\WinZoneTrigger.AudioAutomation"))
            {
                bool locked = false;
                try
                {
                    try { locked = gate.WaitOne(3000); } catch (AbandonedMutexException) { locked = true; }
                    if (!locked) throw new InvalidOperationException("다른 소리 설정 작업이 진행 중입니다.");
                    var json = new JavaScriptSerializer();
                    AudioAutomationState state = File.Exists(StatePath)
                        ? json.Deserialize<AudioAutomationState>(AtomicFile.Read(StatePath)) : new AudioAutomationState();
                    state.Layers = state.Layers ?? new List<AudioLayer>();
                    string before = json.Serialize(state);
                    action(state);
                    string after = json.Serialize(state);
                    if (before != after) AtomicFile.Write(StatePath, after);
                }
                finally { if (locked) gate.ReleaseMutex(); }
            }
        }
        internal static bool DetectOverride(AudioAutomationState state, AudioSnapshot current, AppConfig config, DateTime utc)
        {
            var top = state.Layers.LastOrDefault();
            if (top == null || AudioSnapshot.Same(top.After, current)) return false;
            // Never restore stale values after a device switch or an external/user adjustment.
            state.Layers.Clear();
            if (config.RespectManualChanges.GetValueOrDefault(true))
                state.HoldUntilUtc = utc.AddMinutes(config.ManualOverrideMinutes <= 0 ? 60 : config.ManualOverrideMinutes);
            state.Message = "수동 소리 변경 감지 · 이전 값 복원을 취소했습니다.";
            return true;
        }
        public static void Apply(ZoneRule zone, Action<string> log)
        {
            if (zone.AudioAction != "Mute" && zone.AudioAction != "Unmute" && zone.AudioAction != "Volume") return;
            WithState(state => ApplyCore(state, zone, ConfigStore.Load(), AudioController.Read, AudioController.Set, DateTime.UtcNow, log));
        }
        internal static void ApplyCore(AudioAutomationState state, ZoneRule zone, AppConfig config,
            Func<AudioSnapshot> read, Action<AudioSnapshot> write, DateTime utc, Action<string> log)
        {
            var before = read();
            DetectOverride(state, before, config, utc);
            if (config.RespectManualChanges.GetValueOrDefault(true) && state.HoldUntilUtc > utc)
            {
                state.Message = "수동 소리 설정 유지: " + state.HoldUntilUtc.ToLocalTime().ToString("HH:mm") + "까지";
                log(state.Message); return;
            }
            var existing = state.Layers.LastOrDefault();
            var desired = new AudioSnapshot { DeviceId = before.DeviceId,
                Volume = zone.AudioAction == "Volume" ? zone.VolumePercent / 100f : before.Volume,
                Muted = zone.AudioAction == "Mute" };
            write(desired);
            var after = read();
            // Retain the original baseline when this location is explicitly run again.
            if (existing != null && existing.ZoneId == zone.Id) state.Layers.Remove(existing);
            state.Layers.Add(new AudioLayer { ZoneId = zone.Id, Restore = zone.RestoreAudioOnExit,
                Before = existing != null && existing.ZoneId == zone.Id ? existing.Before : before, After = after });
            state.Message = zone.Name + ": " + (zone.AudioAction == "Volume" ? "볼륨 " + zone.VolumePercent + "%" : zone.AudioAction == "Mute" ? "음소거" : "음소거 해제");
            log(state.Message);
        }
        public static void Reconcile(IEnumerable<string> activeIds, AppConfig config)
        {
            if (!File.Exists(StatePath)) return;
            WithState(state =>
            {
                foreach (var layer in state.Layers)
                {
                    var zone = config.Zones.FirstOrDefault(z => z.Id == layer.ZoneId);
                    if (zone != null) layer.Restore = zone.RestoreAudioOnExit;
                }
                var soundOwners = config.Zones.Where(z => z.AudioAction == "Mute" || z.AudioAction == "Unmute" || z.AudioAction == "Volume").Select(z => z.Id);
                ReconcileCore(state, activeIds.Intersect(soundOwners), config, AudioController.Read, AudioController.Set, DateTime.UtcNow, DiagnosticsLog.WriteEvent);
            });
        }
        internal static void ReconcileCore(AudioAutomationState state, IEnumerable<string> activeIds, AppConfig config,
            Func<AudioSnapshot> read, Action<AudioSnapshot> write, DateTime utc, Action<string> log)
        {
            if (state.Layers.Count == 0) return;
            var current = read();
            if (DetectOverride(state, current, config, utc)) { log(state.Message); return; }
            var active = new HashSet<string>(activeIds);
            while (state.Layers.Count > 0 && !active.Contains(state.Layers.Last().ZoneId))
            {
                var layer = state.Layers.Last();
                if (!layer.Restore || !AudioSnapshot.Same(current, layer.After)) { state.Layers.Clear(); break; }
                write(layer.Before);
                current = read();
                state.Layers.RemoveAt(state.Layers.Count - 1);
                state.Message = "위치 이탈·조건 종료: 이전 소리 설정을 복원했습니다.";
                log(state.Message);
            }
        }
    }
}
