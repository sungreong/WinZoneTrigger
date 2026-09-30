import {escapeHtml as e, time, asDate, paused, lines, applyNetwork} from './model.mjs';
import {wifiView,conditionsView,actionsView,watchView,settingsView,logsView,toggle} from './views.js';
import {demoConfig,demoNetworks,demoStatus} from './demo.mjs';

const $=id=>document.getElementById(id);
const demo=new URLSearchParams(location.search).has('demo')&&!window.__TAURI__;
let config,revision,selected,tab='wifi',networks=[],status={},dirty=false,startup=false,busy=0,toastTimer,pickerZone,editVersion=0;
const zone=()=>config?.Zones.find(z=>z.Id===selected);
const uid=()=>crypto.randomUUID().replaceAll('-','');
async function request(Operation,extra={}) {
 if(demo){
  if(Operation==='load')return {Config:structuredClone(demoConfig),Revision:'demo',Startup:true};
  if(Operation==='scan')return {Networks:demoNetworks,Location:{HasLocation:false,Error:'미리보기에서는 좌표를 읽지 않습니다.'}};
  if(Operation==='save')return {Config:structuredClone(extra.Config),Revision:'demo'};
  if(Operation==='new-zone')return {...structuredClone(demoConfig.Zones[0]),Id:uid(),Name:'새 위치',Enabled:false,WifiRecoveryEnabled:false,ConnectSsid:'',ConnectProfile:'',NearbySsids:[]};
  if(Operation==='apps')return [{Name:'메모장',Target:'notepad.exe',Source:'Windows'}];
  if(Operation==='startup')return extra.Enabled;
  return '';
 }
 if(!window.__TAURI__)throw new Error('앱에서 열어주세요. 화면 미리보기는 주소에 ?demo를 붙여 실행할 수 있습니다.');
 return window.__TAURI__.core.invoke('engine_request',{request:{Operation,...extra}});
}
function toast(message,error=false){clearTimeout(toastTimer);$('toast').textContent=message;$('toast').className=error?'error':'';$('toast').hidden=false;toastTimer=setTimeout(()=>$('toast').hidden=true,error?12000:4500);}
async function run(task,button){busy++;if(button)button.disabled=true;try{return await task();}catch(err){toast(String(err.message||err),true);}finally{busy--;if(button)button.disabled=false;}}
function setDirty(value=true){dirty=value;if(value)editVersion++;$('dirty-dot').className=`dot ${dirty?'warning':'neutral'}`;$('save-status').textContent=dirty?'저장하지 않은 변경사항이 있어요':demo?'미리보기 · 실제 설정에 저장되지 않습니다':'모든 변경사항이 저장되었습니다';$('save').disabled=!config;$('reload').disabled=!config;}
async function confirm(title,message){$('confirm-title').textContent=title;$('confirm-message').textContent=message;const d=$('confirm');d.showModal();return new Promise(resolve=>d.addEventListener('close',()=>resolve(d.returnValue==='ok'),{once:true}));}

function renderNav(){
 $('zones').innerHTML=config.Zones.map((z,i)=>`<button class="zone-button ${z.Id===selected?'selected':''}" data-zone="${e(z.Id)}" aria-current="${z.Id===selected?'page':'false'}"><span class="zone-icon">${i===0?'⌂':'◇'}</span><span class="zone-copy"><strong>${e(z.Name)}</strong><small>${z.Enabled?(z.WifiRecoveryEnabled?'자동 복구 켜짐':'운영 중'):'미운영'}</small></span><span class="dot ${z.Enabled?'':'neutral'}"></span></button>`).join('');
}
function render(){
 if(!config)return;
 renderNav();const z=zone();const global=tab==='settings'||tab==='logs';
 $('title').textContent=tab==='settings'?'내 PC의 자동화':tab==='logs'?'공간의 활동 기록':z?.Name||'첫 공간을 만들어보세요';
 $('subtitle').textContent=tab==='settings'?'시작부터 잠시 쉬어갈 때까지, 원하는 방식으로.':tab==='logs'?'연결을 확인하고, 실행한 일을 돌아보세요.':z?'이 공간에서의 연결과 하루의 시작을 준비하세요.':'왼쪽 ＋ 버튼으로 위치를 추가하면 자동화를 시작할 수 있습니다.';
 $('breadcrumb').textContent=global?(tab==='settings'?'앱 설정':'활동 기록'):z?.Name||'새 위치';
 $('zone-actions').innerHTML=!global&&z?toggle('Enabled',z.Enabled,z.Enabled?'운영 중':'미운영'):'';
 $('tabs').hidden=global||!z;
 $('tabs').innerHTML=[['wifi','Wi-Fi 복구'],['conditions','위치 감지'],['actions','실행 동작'],['watch','앱 감시']].map(([id,label])=>`<button class="tab ${tab===id?'selected':''}" data-tab="${id}" aria-current="${tab===id?'page':'false'}">${label}${id==='watch'&&z?.AppWatchItems?.length?` · ${z.AppWatchItems.length}`:''}</button>`).join('');
 $('connection').hidden=global;
 updateStatus();
 $('content').innerHTML=tab==='settings'?settingsView(config,startup):tab==='logs'?logsView(status):!z?'<div class="empty">장소마다 다른 Wi-Fi와 실행할 앱을 설정할 수 있어요.<br><button class="button primary" data-action="new-zone">＋ 첫 위치 만들기</button></div>':tab==='wifi'?wifiView(z,networks):tab==='conditions'?conditionsView(z,networks):tab==='actions'?actionsView(z):watchView(z);
}
function updateStatus(){
 const a=status.Automation||{},w=status.Wifi||{},date=asDate(a.UpdatedAtLocal);
 const stale=!date||Date.now()-date.getTime()>180000;
 $('engine-status').textContent=demo?'화면 미리보기':paused(config)?'자동화 정지 중':stale?'상태 확인 필요':'자동화 동작 중';
 $('engine-status').className=`status-pill ${!demo&&(stale||paused(config))?'warning':''}`;
 const connected=w.ConnectedSsid||networks.filter(n=>n.Connected).map(n=>n.Ssid).join(', ');
 $('connection').innerHTML=`<div class="signal-icon" aria-hidden="true"><i></i><i></i><i></i></div><div><div class="small-label">${connected?'최근 확인한 연결':'연결 상태'}</div><strong>${e(connected||'아직 연결을 확인하지 않았어요')}</strong><p>${e(w.Message||'주변 Wi-Fi를 찾아 원하는 연결을 선택해보세요.')}</p></div><div class="connection-meta"><div>최근 확인 <b>${time(w.CheckedAt)}</b></div><div>다음 확인 <b>${w.NextCheckAt?time(w.NextCheckAt):'설정한 주기마다'}</b></div></div>`;
}
async function refreshStatus(){status=demo?demoStatus():await window.__TAURI__.core.invoke('read_status');updateStatus();if(tab==='logs')$('content').innerHTML=logsView(status);}
async function load(){const result=await request('load');config=result.Config;revision=result.Revision;startup=result.Startup;selected=config.Zones.some(z=>z.Id===selected)?selected:config.Zones[0]?.Id;setDirty(false);render();await refreshStatus();}
async function save(){
 const invalid=[...document.querySelectorAll('input,select,textarea')].find(el=>!el.checkValidity());
 if(invalid){invalid.reportValidity();return;}
 const savingVersion=editVersion;
 const result=await request('save',{Config:structuredClone(config),Revision:revision});revision=result.Revision;
 if(editVersion===savingVersion){config=result.Config;setDirty(false);render();}
 toast(demo?'미리보기 설정을 적용했습니다.':editVersion===savingVersion?'저장했습니다. 백그라운드에 곧 반영됩니다.':'저장했습니다. 저장 중에 추가한 변경사항은 한 번 더 저장해주세요.');
}
function edit(event){
 const input=event.target,key=input.dataset.field;if(!key)return;
 const scope=input.dataset.scope;
 let target=scope==='config'?config:scope==='watch'?zone()?.AppWatchItems[Number(input.closest('[data-watch]').dataset.watch)]:zone();
 if(!target)return;
 target[key]=input.type==='checkbox'?input.checked:input.dataset.list?lines(input.value):input.type==='number'?Number(input.value):input.value;
 setDirty();if(key==='Name'||key==='Enabled'||key==='WifiRecoveryEnabled')renderNav();
 if(key==='Enabled'&&scope!=='watch'){$('zone-actions').innerHTML=toggle('Enabled',target.Enabled,target.Enabled?'운영 중':'미운영');}
}
$('content').addEventListener('input',edit);$('zone-actions').addEventListener('change',edit);
$('zones').addEventListener('click',event=>{const b=event.target.closest('[data-zone]');if(b){selected=b.dataset.zone;tab='wifi';render();$('workspace').scrollTop=0;}});
$('tabs').addEventListener('click',event=>{const b=event.target.closest('[data-tab]');if(b){tab=b.dataset.tab;render();}});
$('settings').onclick=()=>{tab='settings';render();};$('logs').onclick=()=>{tab='logs';render();};
$('save').onclick=()=>run(save,$('save'));
$('reload').onclick=()=>run(async()=>{if(!dirty||await confirm('변경사항을 취소할까요?','저장하지 않은 변경사항을 버리고 마지막 저장 상태로 돌아갑니다.'))await load();},$('reload'));
$('refresh').onclick=()=>run(async()=>{await refreshStatus();toast('최신 상태를 확인했습니다. 설정 편집 내용은 유지됩니다.');},$('refresh'));
async function newZone(){const z=await request('new-zone');z.Enabled=false;config.Zones.push(z);selected=z.Id;tab='conditions';setDirty();render();}
$('new-zone').onclick=()=>run(newZone,$('new-zone'));

$('content').addEventListener('click',event=>run(async()=>{
 const button=event.target.closest('button');if(!button)return;
 const z=zone();
 if(button.dataset.network!==undefined){const n=networks[Number(button.dataset.network)];if(button.dataset.purpose==='target')applyNetwork(z,n);else{z.NearbySsids||=[];z.NearbySsids=z.NearbySsids.includes(n.Ssid)?z.NearbySsids.filter(s=>s!==n.Ssid):[...z.NearbySsids,n.Ssid];z.UseWifiCondition=true;}setDirty();render();return;}
 if(button.dataset.removeSsid){z.NearbySsids=z.NearbySsids.filter(s=>s!==button.dataset.removeSsid);setDirty();render();return;}
 if(button.dataset.deleteWatch!==undefined){z.AppWatchItems.splice(Number(button.dataset.deleteWatch),1);setDirty();render();return;}
 switch(button.dataset.action){
 case 'new-zone':await newZone();break;
 case 'scan':case 'coordinates':{
  const coordinates=button.dataset.action==='coordinates';button.disabled=true;button.textContent='찾는 중…';
  try{const result=await request('scan',{Location:coordinates});networks=result.Networks||[];if(coordinates){if(!result.Location?.HasLocation)throw new Error(result.Location?.Error||'Windows 위치 서비스를 확인하세요.');Object.assign(z,result.Location.Location);z.UseCoordinates=true;setDirty();}render();toast(coordinates?'현재 좌표를 가져왔습니다. 저장하면 적용됩니다.':`${networks.length}개의 Wi-Fi를 찾았습니다.`);}finally{button.disabled=false;if(button.isConnected)render();}break;
 }
 case 'duplicate':{const copy=structuredClone(z);copy.Id=uid();copy.Name+=' 복사';copy.Enabled=false;(copy.AppWatchItems||[]).forEach(w=>w.Id=uid());config.Zones.push(copy);selected=copy.Id;setDirty();render();break;}
 case 'delete':if(await confirm('이 위치를 삭제할까요?',`${z.Name}의 감지 조건과 실행 설정을 삭제합니다. 저장하면 적용됩니다.`)){config.Zones=config.Zones.filter(x=>x.Id!==z.Id);selected=config.Zones[0]?.Id;setDirty();render();}break;
 case 'new-watch':z.AppWatchItems||=[];z.AppWatchItems.push({Id:uid(),Enabled:true,RequireWindow:false,LaunchTarget:'',ProcessName:'',IntervalValue:5,IntervalUnit:'Minutes'});setDirty();render();break;
 case 'file':{const path=await request('pick-file');if(path){z.AppLaunches||=[];z.AppLaunches.push(path);setDirty();render();}break;}
 case 'apps':pickerZone=z.Id;$('app-picker').showModal();$('app-results').textContent='앱을 찾는 중입니다…';await findApps();break;
 case 'pause':config.AutomationPausedUntilUtc=paused(config)?null:`/Date(${Date.now()+3600000})/`;setDirty();render();toast('변경사항 저장을 누르면 정지·재개가 적용됩니다.');break;
 case 'startup':startup=await request('startup',{Enabled:!startup});render();toast(startup?'Windows 자동 시작을 등록했습니다.':'Windows 자동 시작을 해제했습니다.');break;
 case 'folder':if(!demo)await window.__TAURI__.core.invoke('open_config_folder');break;
 case 'legacy':if(dirty){toast('먼저 변경사항을 저장한 뒤 고급 설정을 열어주세요.',true);break;}if(!demo)await window.__TAURI__.core.invoke('open_legacy');break;
 }
}));
let searchTimer;
async function findApps(){const items=await request('apps',{Query:$('app-query').value});$('app-results').replaceChildren();for(const item of items){const b=document.createElement('button');b.className='button';b.textContent=item.Name+' · '+item.Source;b.title=item.Target;b.onclick=()=>{const target=config.Zones.find(z=>z.Id===pickerZone);if(!target)return;target.AppLaunches||=[];target.AppLaunches.push(item.Target);setDirty();$('app-picker').close();render();};$('app-results').append(b);}if(!items.length)$('app-results').textContent='검색 결과가 없습니다. 실행 파일을 직접 선택해보세요.';}
$('app-query').oninput=()=>{clearTimeout(searchTimer);searchTimer=setTimeout(()=>run(findApps),350);};$('close-picker').onclick=()=>$('app-picker').close();
window.addEventListener('keydown',event=>{if((event.ctrlKey||event.metaKey)&&event.key==='s'){event.preventDefault();if(config&&!busy)run(save,$('save'));}});
await run(async()=>{await load();if(demo){networks=demoNetworks;render();}});
if(!config){$('content').innerHTML='<div class="error-page">설정을 불러오지 못했습니다. 오류 메시지를 확인한 후 다시 시도하세요.<br><button id="retry-load" class="button">다시 불러오기</button></div>';$('retry-load').onclick=()=>run(load);}
setInterval(()=>{if(config&&!busy&&window.__TAURI__)run(refreshStatus);},10000);
