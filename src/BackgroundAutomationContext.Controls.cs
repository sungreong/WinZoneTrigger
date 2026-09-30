using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace WinZoneTrigger
{
    internal sealed class ZoneDecision
    {
        public string ZoneId { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
        public bool Enabled { get; set; }
        public bool? LocationMatches { get; set; }
        public bool TimeAllowed { get; set; }
        public DateTime CheckedAt { get; set; }
    }
    internal sealed class ManualRunRequest
    {
        public string ZoneId { get; set; }
        public DateTime RequestedAtUtc { get; set; }
    }
    internal sealed partial class BackgroundAutomationContext
    {
        private string _manualZoneId;
        private readonly Dictionary<string, DateTime> _exitSince = new Dictionary<string, DateTime>();

        private bool StabilizeExit(string id, bool near, bool wasInside, bool immediate)
        {
            if (near || !wasInside || immediate) { _exitSince.Remove(id); return near; }
            DateTime since;
            if (!_exitSince.TryGetValue(id, out since)) { _exitSince[id] = DateTime.UtcNow; return true; }
            return DateTime.UtcNow - since < TimeSpan.FromSeconds(30);
        }
        private void PollQuickControls()
        {
            if (_scanInProgress || _zoneActionInProgress || _wifiRecoveryInProgress || _appWatchInProgress) return;
            string path = Path.Combine(ConfigStore.ConfigDirectory, "manual-run.json");
            if (!File.Exists(path)) return;
            try
            {
                var request = new JavaScriptSerializer().Deserialize<ManualRunRequest>(AtomicFile.Read(path));
                File.Delete(path);
                if (_config.IsAutomationPaused() || DateTime.UtcNow - request.RequestedAtUtc > TimeSpan.FromMinutes(2))
                { UpdateAutomationEvent("지금 실행 취소: 일시 정지 중이거나 요청이 만료되었습니다.", null, null); return; }
                _manualZoneId = request.ZoneId;
                StartScan(true, false);
            }
            catch (Exception ex) { DiagnosticsLog.Write("지금 실행 요청 실패", ex); }
        }
        private void ObserveAudioState()
        {
            if (_zoneActionInProgress || _scanInProgress) return;
            // Configuration removal/disable and pause end ownership even without another scan.
            ReconcileAudio(_config.IsAutomationPaused() ? new List<string>() : _config.Zones
                .Where(z => z.Enabled && ZoneSchedule.Allows(z, DateTime.Now)).Select(z => z.Id).ToList());
        }
        private void ReconcileAudio(IEnumerable<string> active)
        {
            try { AudioAutomation.Reconcile(active, _config); }
            catch (Exception ex) { DiagnosticsLog.WriteThrottled("audio-observe", "소리 상태 확인 실패: " + ex.Message); }
        }
        private void SaveDecisions(List<ZoneDecision> decisions)
        {
            try { AtomicFile.Write(Path.Combine(ConfigStore.ConfigDirectory, "decisions.json"), new JavaScriptSerializer().Serialize(decisions)); }
            catch (Exception ex) { DiagnosticsLog.Write("실행 이유 저장 실패", ex); }
        }
    }
}
