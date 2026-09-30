export const demoConfig = {
  StartMinimized:true, TrayIconEnabled:false, PreventSleepWhileAutomationActive:false,
  BrightnessScheduleEnabled:false, DefaultBrightnessPercent:70, BrightnessPeriods:[],
  Zones:[
    {Id:'demo-home',Name:'우리 집',Enabled:true,UseWifiCondition:true,UseCoordinates:false,NearbySsids:['Home_5G'],RequireAllSsids:false,Latitude:0,Longitude:0,RadiusMeters:200,WifiRecoveryEnabled:true,WifiRecoveryIntervalSeconds:60,WifiPriority:10,ConnectWifiEnabled:true,ConnectSsid:'Home_5G',ConnectProfile:'Home_5G',MonitoringEnabled:true,RunOnceAtStartup:true,ScanIntervalSeconds:30,AudioAction:'Volume',VolumePercent:20,RestoreAudioOnExit:true,ChromeUrls:[],AppLaunches:[],Commands:[],AppWatchItems:[]},
    {Id:'demo-office',Name:'회사',Enabled:true,UseWifiCondition:true,NearbySsids:['Office_5G'],WifiRecoveryEnabled:false,WifiRecoveryIntervalSeconds:120,WifiPriority:20,ConnectSsid:'Office_5G',ConnectProfile:'Office_5G',AppWatchItems:[]}
  ]
};
export const demoNetworks = [
  {Ssid:'Home_5G',ProfileName:'Home_5G',SignalQuality:96,Connected:true,Connectable:true},
  {Ssid:'Home_2.4G',ProfileName:'Home_2.4G',SignalQuality:84,Connected:false,Connectable:true},
  {Ssid:'Studio',ProfileName:'Studio',SignalQuality:61,Connected:false,Connectable:true},
  {Ssid:'Guest network',ProfileName:'',SignalQuality:42,Connected:false,Connectable:true}
];
export function demoStatus(){return {Decisions:[{ZoneId:'demo-home',Name:'우리 집',Enabled:true,LocationMatches:true,TimeAllowed:true,CheckedAt:new Date().toISOString(),Message:'Wi-Fi 일치 · 현재 위치에서 사용 가능'},{ZoneId:'demo-office',Name:'회사',Enabled:true,LocationMatches:false,TimeAllowed:true,CheckedAt:new Date().toISOString(),Message:'Wi-Fi 미감지 · 위치 불일치로 실행 대기'}],Automation:{UpdatedAtLocal:new Date().toISOString(),ActiveZoneIds:['demo-home'],LastAppWatchText:'등록된 앱 감시가 없습니다.'},Wifi:{ZoneId:'demo-home',CheckedAt:new Date().toISOString(),NextCheckAt:new Date(Date.now()+60000).toISOString(),ConnectedSsid:'Home_5G',TargetSsid:'Home_5G',Status:'connected',Message:'원하는 Wi-Fi에 이미 연결되어 있어 건너뛰었습니다.'},Logs:'2026-09-30 21:00:00  Wi-Fi 복구: 이미 연결되어 있어 건너뛰었습니다.\n2026-09-30 20:59:00  백그라운드 위치 조건 확인 완료'};}
