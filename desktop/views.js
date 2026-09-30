import {escapeHtml as e, time, paused} from './model.mjs';
export const toggle = (field, value, label = '', scope = 'zone') => `<label class="switch"><input type="checkbox" data-scope="${scope}" data-field="${field}" ${value ? 'checked' : ''} aria-label="${e(label || field)}"><span class="switch-track"></span>${label ? `<span>${e(label)}</span>` : ''}</label>`;
export const field = (label, key, value, options = {}) => `<label class="field ${options.wide ? 'wide' : ''}"><span>${e(label)}</span><input data-scope="${options.scope || 'zone'}" data-field="${key}" type="${options.type || 'text'}" value="${e(value)}" ${options.attrs || ''}>${options.help ? `<small>${e(options.help)}</small>` : ''}</label>`;
const textList = (label, key, value, help) => `<label class="field wide"><span>${label}</span><textarea data-field="${key}" data-list="true">${e((value || []).join('\n'))}</textarea><small>${help}</small></label>`;
const header = (title, description, action = '') => `<div class="panel-header"><div><h2>${title}</h2><p>${description}</p></div>${action}</div>`;
const row = (title, description, control) => `<div class="check-row"><div><h3>${title}</h3><p>${description}</p></div>${control}</div>`;
const select = (label,key,value,choices,scope='zone') => `<label class="field"><span>${label}</span><select data-field="${key}" data-scope="${scope}">${choices.map(([v,l])=>`<option value="${e(v)}" ${value===v?'selected':''}>${l}</option>`).join('')}</select></label>`;

export function networksView(zone, networks, purpose) {
  if (!networks.length) return '<div class="empty">주변 Wi-Fi를 불러와 이 위치에서 사용할 네트워크를 선택하세요.<br>연결할 네트워크는 Windows에 저장된 프로필이 필요합니다.</div>';
  return `<div class="network-list">${networks.map((n,i)=>{
    const chosen=purpose==='target'?zone.ConnectSsid===n.Ssid:(zone.NearbySsids||[]).includes(n.Ssid);
    return `<button class="network-option ${chosen?'chosen':''}" data-network="${i}" data-purpose="${purpose}" aria-pressed="${chosen}" ${purpose==='target'&&!n.ProfileName?'disabled':''}><span class="radio"></span><span class="network-copy"><strong>${e(n.Ssid)}</strong><small>${n.Connected?'현재 연결됨':n.ProfileName?'저장된 네트워크':'Windows에서 먼저 연결 필요'}</small></span><span class="quality">${n.SignalQuality}%</span></button>`;
  }).join('')}</div>`;
}

export function wifiView(z, networks) {
 return `<section class="panel">${header('끊겨도, 다시 연결되도록.','이 위치가 감지되면 원하는 Wi-Fi 연결을 주기적으로 확인합니다.',toggle('WifiRecoveryEnabled',z.WifiRecoveryEnabled,'자동 복구'))}
   <div class="field-grid">${field('확인 주기 (초)','WifiRecoveryIntervalSeconds',z.WifiRecoveryIntervalSeconds||60,{type:'number',attrs:'min="30" max="3600" step="1"',help:'30초~1시간 · 기본 60초'})}${field('위치 우선순위','WifiPriority',z.WifiPriority??10,{type:'number',attrs:'min="0" max="100"',help:'여러 위치가 보이면 작은 숫자부터 적용'})}</div>

 </section><section class="panel">${header('이곳에서 연결할 Wi-Fi','저장된 네트워크 중 하나를 선택하세요. 비밀번호는 Windows가 관리합니다.','<button class="button" data-action="scan">↻ 주변 Wi-Fi 찾기</button>')}
 ${networksView(z,networks,'target')}
 <div class="section-label">선택한 연결 대상</div><div class="field-grid">${field('Wi-Fi 이름 (SSID)','ConnectSsid',z.ConnectSsid||'')}${field('Windows 프로필 이름','ConnectProfile',z.ConnectProfile||'',{help:'보통 Wi-Fi 이름과 같습니다. 저장된 프로필 이름을 입력하세요.'})}</div>
 <div class="note">자동 복구는 이 위치가 감지되고 연결 대상이 주변에 보일 때만 실행됩니다. 이미 연결되어 있으면 연결 명령을 보내지 않습니다. 인터넷 장애 자체를 진단하는 기능은 아닙니다.</div>
 </section><section class="panel">${row('위치에 들어올 때도 연결','앱·링크 실행 전에 선택한 Wi-Fi 연결을 확인합니다.',toggle('ConnectWifiEnabled',z.ConnectWifiEnabled,'진입 시 연결'))}</section>`;
}

export function conditionsView(z, networks) {
 return `<section class="panel">${header('어떤 공간인가요?','이름과 감지 조건으로 위치를 구분합니다.')}<div class="field-grid">${field('위치 이름','Name',z.Name,{wide:true})}</div>
 <div class="inline-actions">${toggle('RunOnceAtStartup',z.RunOnceAtStartup,'앱 시작 시 한 번 실행')}${toggle('MonitoringEnabled',z.MonitoringEnabled,'진입 동작 지속 감시')}</div>
 <div class="section-label">위치 확인 주기</div>${field('확인 주기 (초)','ScanIntervalSeconds',z.ScanIntervalSeconds||30,{type:'number',attrs:'min="5" max="3600"',help:'여러 위치를 감시하면 가장 짧은 주기로 함께 확인합니다.'})}</section>
 <section class="panel">${header('주변 Wi-Fi로 위치 찾기','연결하지 않아도 선택한 Wi-Fi가 보이면 위치를 인식합니다.',toggle('UseWifiCondition',z.UseWifiCondition,'Wi-Fi 감지'))}
 <div class="chips">${(z.NearbySsids||[]).map(s=>`<button class="chip" data-remove-ssid="${e(s)}" title="감지 조건에서 제거">${e(s)} ×</button>`).join('')}</div>
 <div class="inline-actions"><button class="button" data-action="scan">↻ 주변 Wi-Fi 찾기</button>${toggle('RequireAllSsids',z.RequireAllSsids,'선택한 Wi-Fi가 모두 보여야 함')}</div><div class="section-label">감지할 Wi-Fi 선택 · 여러 개 가능</div>${networksView(z,networks,'condition')}
 <div class="section-label">직접 입력</div>${textList('감지 Wi-Fi 이름','NearbySsids',z.NearbySsids,'한 줄에 하나씩 입력하세요. SSID는 대소문자를 구분합니다.')}</section>
 <section class="panel">${header('좌표로 위치 찾기','Windows 위치 서비스로 반경 안에 있는지 확인합니다.',toggle('UseCoordinates',z.UseCoordinates,'좌표 감지'))}
 <div class="field-grid three">${field('위도','Latitude',z.Latitude||0,{type:'number',attrs:'min="-90" max="90" step="any"'})}${field('경도','Longitude',z.Longitude||0,{type:'number',attrs:'min="-180" max="180" step="any"'})}${field('반경 (m)','RadiusMeters',z.RadiusMeters||200,{type:'number',attrs:'min="1" max="100000"'})}</div><div class="inline-actions"><button class="button" data-action="coordinates">현재 좌표 가져오기</button></div><div class="note">좌표와 Wi-Fi 감지를 함께 켜면 둘 중 하나만 일치해도 이 위치로 판단합니다.</div></section>
 <div class="danger-zone"><span>위치와 연결 설정을 함께 복제할 수 있습니다.</span><div class="inline-actions"><button class="button" data-action="duplicate">위치 복제</button><button class="button danger" data-action="delete">위치 삭제</button></div></div>`;
}

export function actionsView(z) {
 return `<section class="panel">${header('이 공간에 들어오면','진입 동작은 위치가 바뀔 때 한 번 실행합니다. Wi-Fi 복구 때는 다시 실행하지 않습니다.')}${select('소리 설정','AudioAction',z.AudioAction||'None',[['None','현재 상태 유지'],['Mute','음소거'],['Unmute','음소거 해제']])}</section>
 <section class="panel">${header('자주 쓰는 앱 열기','실행 파일, 바로가기, 앱 이름 또는 프로토콜을 등록하세요.','<button class="button" data-action="apps">＋ 앱 찾기</button>')}${textList('실행할 앱','AppLaunches',z.AppLaunches,'한 줄에 하나씩 · 예: notepad.exe 또는 obsidian://')}<div class="inline-actions"><button class="button" data-action="file">파일 선택</button></div></section>
 <section class="panel">${header('Chrome 링크 열기','필요한 문서와 페이지를 탭으로 열어둡니다.')}${textList('열 URL','ChromeUrls',z.ChromeUrls,'한 줄에 하나씩 · https:// 주소를 입력하세요.')}</section>
 <details class="panel"><summary>고급 명령어</summary><p class="muted">등록한 명령을 Windows 명령 프롬프트에서 순서대로 실행합니다.</p>${textList('실행할 명령','Commands',z.Commands,'직접 확인한 명령만 등록하세요. 각 줄은 cmd.exe /c로 실행됩니다.')}</details>`;
}

export function watchView(z) {
 return `<section class="panel">${header('필요한 앱을 계속 켜두세요.','이 위치에 있을 때 앱이 꺼지면 다시 엽니다. 항목마다 주기를 정할 수 있습니다.','<button class="button" data-action="new-watch">＋ 새 감시</button>')}
 ${(z.AppWatchItems||[]).length===0?'<div class="empty">감시 중인 앱이 없습니다.<br>계속 켜두고 싶은 앱을 추가해보세요.</div>':''}
 ${(z.AppWatchItems||[]).map((w,i)=>`<article class="watch-item" data-watch="${i}"><div class="watch-header">${toggle('Enabled',w.Enabled,`앱 감시 ${i+1}`,'watch')}<button class="button subtle" data-delete-watch="${i}">삭제</button></div><div class="field-grid">${field('실행 대상','LaunchTarget',w.LaunchTarget,{scope:'watch',wide:true})}${field('프로세스 이름','ProcessName',w.ProcessName,{scope:'watch',help:'예: Codex · .exe 확장자 제외'})}${field('확인 주기','IntervalValue',w.IntervalValue||5,{scope:'watch',type:'number',attrs:'min="1" max="10080"'})}${select('주기 단위','IntervalUnit',w.IntervalUnit||'Minutes',[['Minutes','분'],['Hours','시간']],'watch')}</div><div class="inline-actions">${toggle('RequireWindow',w.RequireWindow,'표시되는 창이 없을 때도 다시 열기','watch')}</div></article>`).join('')}</section>`;
}

export function settingsView(c,startup) {
 return `<section class="panel">${header('내 PC에 맞게','시작 동작과 자동화 운영 방식을 설정합니다.')}
 ${row('Windows 시작 시 실행','로그인하면 자동화와 트레이 아이콘을 실행합니다.',`<button class="button" data-action="startup">${startup?'등록 해제':'자동 시작 등록'}</button>`)}
 ${row('트레이로 시작','시작할 때 설정 창을 띄우지 않습니다.',toggle('StartMinimized',c.StartMinimized,'트레이 시작','config'))}
 ${row('자동화 중 절전 방지','자동화가 동작하는 동안 PC 절전을 방지합니다.',toggle('PreventSleepWhileAutomationActive',c.PreventSleepWhileAutomationActive,'절전 방지','config'))}
 ${row('자동화 임시 정지',paused(c)?`재개 예정: ${e(new Date(Number(String(c.AutomationPausedUntilUtc).match(/\d+/)?.[0])).toLocaleString('ko-KR'))}`:'Wi-Fi 복구·진입 동작·앱 감시·밝기 일정을 함께 멈춥니다.',`<button class="button" data-action="pause">${paused(c)?'지금 재개':'1시간 정지'}</button>`)}
 </section><section class="panel">${header('시간대별 화면 밝기','기존 밝기 일정을 그대로 이어서 사용할 수 있습니다.',toggle('BrightnessScheduleEnabled',c.BrightnessScheduleEnabled,'밝기 일정','config'))}${field('기본 밝기 (%)','DefaultBrightnessPercent',c.DefaultBrightnessPercent||70,{scope:'config',type:'number',attrs:'min="1" max="100"'})}
 <div class="inline-actions"><button class="button" data-action="legacy">밝기 일정 상세 편집</button></div><p class="muted">저장된 일정 ${(c.BrightnessPeriods||[]).length}개 · 상세 편집은 기존 고급 설정 화면에서 열립니다.</p></section>
 <section class="panel">${header('로컬 데이터','모든 설정과 기록은 이 PC의 사용자 폴더에 보관됩니다.')}<div class="inline-actions"><button class="button" data-action="folder">설정 폴더 열기</button><button class="button" data-action="legacy">기존 고급 설정 열기</button></div></section>`;
}

export function logsView(status) {
 const a=status?.Automation||{},w=status?.Wifi||{};
 return `<section class="panel">${header('최근 자동화 상태','마지막 확인 시각과 결과를 확인하세요.')}<div class="activity-line"><time>${time(w.CheckedAt)}</time><span>${e(w.Message||'Wi-Fi 복구가 아직 실행되지 않았습니다.')}</span></div><div class="activity-line"><time>${time(a.UpdatedAtLocal)}</time><span>${e(a.LastActionText||a.LastEventText||'자동화 상태를 기다리고 있습니다.')}</span></div><div class="activity-line"><time>앱 감시</time><span>${e(a.LastAppWatchText||'최근 감시 결과가 없습니다.')}</span></div></section><section class="panel">${header('활동 기록','최근 120줄 · 연결과 실행 이력을 확인할 수 있습니다.','<button class="button" data-action="folder">전체 로그 폴더</button>')}<pre class="log-list" id="log-output">${e(status?.Logs||'아직 기록이 없습니다.')}</pre></section>`;
}
