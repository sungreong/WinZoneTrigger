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
    (root / 'config.json').write_text('{ broken', encoding='utf-8')
    assert not call('load')['Ok']
    assert (root / 'config.json').read_text() == '{ broken'
    assert not call('unknown')['Ok']
    print('PASS: isolated config round trip, legacy dates, stale revision, validation, atomic backup, corrupt config, operation allowlist')
