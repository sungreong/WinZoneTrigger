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
