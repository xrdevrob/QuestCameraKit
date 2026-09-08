#!/usr/bin/env python3
"""Fast clone-integrity and credential checks; no Unity installation required."""
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
TEXT_ASSETS = {'.cs', '.prefab', '.unity', '.asset', '.json', '.xml'}
CREDENTIAL = re.compile(rb'sk-(?:proj-)?[A-Za-z0-9_-]{30,}')


def check() -> None:
    paths = subprocess.check_output(
        ['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT
    ).decode().split('\0')
    failures = []
    for name in sorted(set(paths) - {''}):
        path = ROOT / name
        if not path.is_file():
            continue
        if name.startswith('Unity-QuestVisionKit/Assets/'):
            with path.open('rb') as stream:
                prefix = stream.read(128)
            if prefix.startswith(b'version https://git-lfs.github.com/spec/v1'):
                failures.append(f'{name}: unresolved Git LFS asset; run git lfs pull')
        if path.suffix in TEXT_ASSETS and CREDENTIAL.search(path.read_bytes()):
            failures.append(f'{name}: possible embedded API credential (value withheld)')
    if failures:
        raise SystemExit('\n'.join(failures))
    print('PASS: runtime assets are materialized and no API credentials found in text assets.')


if __name__ == '__main__':
    check()
