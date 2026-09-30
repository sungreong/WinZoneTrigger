"""Bridge/config regression checks in disposable data; never runs user automation."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

ENGINE = Path(__file__).resolve().parents[1] / 'bin' / 'WinZoneTrigger.Engine.exe'

with tempfile.TemporaryDirectory(prefix='winzone-tests-') as folder:
    root = Path(folder)
    env = dict(os.environ, WINZONE_TEST_DATA=folder)

    def call(operation, **kwargs):
        request, response = root / 'request.json', root / 'response.json'
        request.write_text(json.dumps(dict(Operation=operation, **kwargs)), encoding='utf-8')
        process = subprocess.run([str(ENGINE), '--desktop-bridge', str(request), str(response)], env=env, timeout=25)
        body = json.loads(response.read_text(encoding='utf-8-sig'))
        assert process.returncode == (0 if body['Ok'] else 1), body
        return body

    loaded = call('load')['Result']
    assert loaded['Revision'] == 'missing'
    config = loaded['Config']
    zone = config['Zones'][0]
    zone.update(Name='Fixture', Enabled=False, UseWifiCondition=True, NearbySsids=['Fixture'],
                ConnectSsid='Fixture', ConnectProfile='Fixture profile', WifiRecoveryEnabled=True,
                WifiRecoveryIntervalSeconds=60, WifiPriority=10)
    config['AutomationPausedUntilUtc'] = '/Date(1999999999000)/'
    saved = call('save', Config=config, Revision=loaded['Revision'])
    assert saved['Ok'], saved
    reload = call('load')['Result']
    assert reload['Config']['Zones'][0]['WifiRecoveryEnabled']
    assert reload['Config']['Zones'][0]['ConnectProfile'] == 'Fixture profile'
    assert '1999999999000' in reload['Config']['AutomationPausedUntilUtc']
    assert not call('save', Config=config, Revision='old-revision')['Ok']
    before = (root / 'config.json').read_bytes()
    zone['WifiRecoveryIntervalSeconds'] = 5
    assert not call('save', Config=config, Revision=reload['Revision'])['Ok']
    assert (root / 'config.json').read_bytes() == before
    zone['WifiRecoveryIntervalSeconds'] = 60
    zone['ConnectSsid'] = 'Updated fixture'
    assert call('save', Config=config, Revision=reload['Revision'])['Ok']
    assert (root / 'config.json.bak').read_bytes() == before
    loaded = call('load')['Result']
    config = loaded['Config']; zone = config['Zones'][0]
    zone.update(AudioAction='Volume', VolumePercent=23, RestoreAudioOnExit=True,
                ScheduleEnabled=True, ScheduleDays=62, ScheduleStartMinute=1320, ScheduleEndMinute=420)
    config.update(RespectManualChanges=True, ManualOverrideMinutes=90, RestoreBrightnessOnDisable=True)
    config['BrightnessPeriods'] = [dict(Id='period',Enabled=True,StartMinuteOfDay=540,BrightnessPercent=65,NightLightAction='Keep')]
    saved = call('save', Config=config, Revision=loaded['Revision'])['Result']
    assert saved['Config']['Zones'][0]['VolumePercent'] == 23
    assert saved['Config']['Zones'][0]['ScheduleDays'] == 62
    assert not call('run-now', ZoneId=zone['Id'])['Ok']  # disabled or paused
    pause = call('pause', Query='30', Revision=saved['Revision'])['Result']
    assert pause['Config']['Zones'][0]['RestoreAudioOnExit']
    assert pause['Config']['BrightnessPeriods'][0]['BrightnessPercent'] == 65
    assert not call('pause', Query='today', Revision='stale')['Ok']
    resumed = call('pause', Query='resume', Revision=pause['Revision'])['Result']
    assert resumed['Config']['AutomationPausedUntilUtc'] is None
    zone['Enabled'] = True; config['AutomationPausedUntilUtc'] = None
    saved = call('save', Config=config, Revision=resumed['Revision'])['Result']
    assert call('run-now', ZoneId=zone['Id'])['Ok']
    queued = json.loads((root / 'manual-run.json').read_text(encoding='utf-8-sig'))
    assert queued['ZoneId'] == zone['Id']  # queued only; no actual automation started
    zone['ScheduleDays'] = 0
    assert not call('save', Config=config, Revision=saved['Revision'])['Ok']
    zone['ScheduleDays'] = 62; zone['VolumePercent'] = 101
    assert not call('save', Config=config, Revision=saved['Revision'])['Ok']
    zone['VolumePercent'] = 23
    config['BrightnessPeriods'].append(dict(config['BrightnessPeriods'][0], Id='duplicate'))
    assert not call('save', Config=config, Revision=saved['Revision'])['Ok']
    (root / 'config.json').write_text('{ broken', encoding='utf-8')
    assert not call('load')['Ok']
    assert (root / 'config.json').read_text() == '{ broken'
    assert not call('unknown')['Ok']
    print('PASS: isolated config round trip, legacy dates, stale revision, validation, atomic backup, corrupt config, operation allowlist, audio/schedule persistence, pause/resume revision, manual run queue')
