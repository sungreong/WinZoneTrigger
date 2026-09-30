"""Launch the actual Tauri executable with isolated configuration and no automation."""
import os
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='winzone-package-') as folder:
    data = Path(folder)
    (data / 'automation-state.json').write_text('{"ProcessId":123}', encoding='utf-8-sig')
    result = subprocess.run([str(root / 'bin' / 'WinZoneTrigger.exe'), '--verify-package'],
                            env=dict(os.environ, WINZONE_TEST_DATA=folder), timeout=60)
    report = (data / 'package-check.txt').read_text()
    print(report)
    assert result.returncode == 0 and report.startswith('PASS:'), report
