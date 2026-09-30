export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export const lines = text => String(text).split(/\r?\n/).map(s => s.trim()).filter(Boolean);
export function asDate(value) {
  if (!value) return null;
  const match = String(value).match(/\/Date\((-?\d+)/);
  const date = new Date(match ? Number(match[1]) : value);
  return Number.isNaN(date.getTime()) ? null : date;
}
export function time(value) {
  const date = asDate(value);
  return date ? date.toLocaleTimeString('ko-KR', {hour12:false,hour:'2-digit',minute:'2-digit',second:'2-digit'}) : '아직 확인 전';
}
export function paused(config) { const date = asDate(config?.AutomationPausedUntilUtc); return date && date.getTime() > Date.now(); }
export function applyNetwork(zone, network) {
  if (!network.ProfileName) throw new Error('Windows에서 먼저 한 번 연결해 프로필을 저장하세요.');
  zone.ConnectSsid = network.Ssid;
  zone.ConnectProfile = network.ProfileName;
}

// Enabled is user intent. Availability always comes from a recent, independent observation.
export function zoneAvailability(zone, config, status, now = Date.now()) {
  if (!zone?.Enabled) return {key:'disabled',label:'사용 안 함',tone:'neutral',detail:'이 위치의 자동화 사용 설정이 꺼져 있습니다.'};
  if ((asDate(config?.AutomationPausedUntilUtc)?.getTime()||0)>now) return {key:'paused',label:'일시 정지',tone:'warning',detail:'사용 설정은 켜져 있고 전체 자동화를 잠시 멈췄습니다.'};
  const d=(status?.Decisions||[]).find(x=>x.ZoneId===zone.Id);
  const observed=asDate(d?.CheckedAt)?.getTime(), heartbeat=asDate(status?.Automation?.UpdatedAtLocal)?.getTime();
  const maxAge=Math.max(90000,(zone.ScanIntervalSeconds||30)*2000+60000);
  if(!observed || !heartbeat || now-observed>maxAge || now-heartbeat>180000 || d.Enabled===false)
    return {key:'unknown',label:'위치 확인 필요',tone:'neutral',detail:'사용 설정은 켜져 있지만 최근 위치 판정이 없습니다. 지금 실행으로 확인하세요.'};
  if(d.LocationMatches===false) return {key:'outside',label:'위치 불일치 · 대기',tone:'warning',detail:'사용 설정은 켜져 있지만 현재 위치 조건이 맞지 않아 실행하지 않습니다.'};
  if(d.LocationMatches!==true) return {key:'unknown',label:'감지 확인 필요',tone:'neutral',detail:'위치·Wi-Fi 정보를 확인하지 못해 현재 위치를 확정할 수 없습니다.'};
  if(d.TimeAllowed===false) return {key:'schedule',label:'시간 조건 · 대기',tone:'warning',detail:'현재 위치는 일치하지만 실행 요일·시간 조건 밖입니다.'};
  if(d.TimeAllowed!==true) return {key:'unknown',label:'조건 확인 필요',tone:'neutral',detail:'새 엔진의 조건 확인 결과를 기다리고 있습니다.'};
  return {key:'ready',label:'현재 위치 일치',tone:'',detail:'사용 설정이 켜져 있고 현재 위치·시간 조건이 맞습니다. 등록한 자동화 방식에 따라 실행합니다.'};
}
