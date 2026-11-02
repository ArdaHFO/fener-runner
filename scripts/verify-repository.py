#!/usr/bin/env python3
"""Offline source-publication checks. No Unity build or cloud access is performed."""
from __future__ import annotations

import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def repository_files() -> list[Path]:
    """Include pending files locally, while respecting the actual Git ignore rules."""
    try:
        result = subprocess.run(
            ['git', '-C', str(ROOT), 'ls-files', '--cached', '--others', '--exclude-standard', '-z'],
            capture_output=True, check=True, timeout=30,
        )
        return sorted({ROOT / item.decode('utf-8') for item in result.stdout.split(b'\0') if item})
    except (OSError, subprocess.SubprocessError):
        print('Note: Git unavailable; inspecting the extracted source archive instead.')
        return sorted(p for p in ROOT.rglob('*') if p.is_file() and '.git' not in p.relative_to(ROOT).parts)


def verify_layout(files: list[Path]) -> None:
    required = [
        'LICENSE', 'README.md', 'README.tr.md', 'THIRD_PARTY_NOTICES.md', '.gitignore',
        'UnityProject/ProjectSettings/ProjectVersion.txt',
        'UnityProject/Packages/manifest.json', 'UnityProject/Packages/packages-lock.json',
        'UnityProject/Assets/FenerRunner/Generated/Scenes/FenerRunner.unity',
        'UnityProject/Assets/FenerRunner/Art/Fonts/OFL.txt',
        'docs/validation/v0.20.0.json',
    ]
    for name in required:
        if not (ROOT / name).is_file():
            fail(f'Missing required file: {name}')
    forbidden_dirs = {'library', 'temp', 'obj', 'logs', 'usersettings', '.utmp', '__pycache__', '.local-build'}
    forbidden_suffixes = {'.keystore', '.jks', '.pfx', '.p12', '.pem', '.key', '.apk', '.aab', '.pyc'}
    sensitive_patterns = [
        re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
        re.compile(rb'\bgh[pousr]_[A-Za-z0-9]{30,}\b'),
        re.compile(rb'\bgithub_pat_[A-Za-z0-9_]{30,}\b'),
    ]
    for path in files:
        rel = path.relative_to(ROOT)
        if path.is_symlink():
            fail(f'Symlink is not allowed in the distributable tree: {rel.as_posix()}')
            continue
        if not path.is_file():
            fail(f'Tracked file is missing: {rel.as_posix()}')
            continue
        if any(part.lower() in forbidden_dirs for part in rel.parts[:-1]) or rel.parts[0].lower() in {'work', 'captures', 'validation', 'builds'}:
            fail(f'Generated or private directory included: {rel.as_posix()}')
        if path.suffix.lower() in forbidden_suffixes or path.name == '.env':
            fail(f'Private/build artifact included: {rel.as_posix()}')
        if path.stat().st_size >= 100 * 1024 * 1024:
            fail(f'File reaches GitHub ordinary Git size limit: {rel.as_posix()}')
        if path.suffix.lower() in {'.cs', '.ps1', '.py', '.json', '.yml', '.yaml', '.md', '.txt', '.asset'}:
            content = path.read_bytes()
            if any(pattern.search(content) for pattern in sensitive_patterns):
                fail(f'Possible authentication secret in: {rel.as_posix()}')
            if re.search(rb'[A-Za-z]:[/\\]Users[/\\][^\s/\\]+', content):
                fail(f'Machine-specific home path in: {rel.as_posix()}')


def verify_packages() -> None:
    try:
        manifest = json.loads((ROOT / 'UnityProject/Packages/manifest.json').read_text(encoding='utf-8-sig'))
        locked = json.loads((ROOT / 'UnityProject/Packages/packages-lock.json').read_text(encoding='utf-8-sig'))
        for name, version in manifest['dependencies'].items():
            actual = locked['dependencies'].get(name, {}).get('version')
            if actual != version:
                fail(f'Package lock differs from manifest: {name} ({version} vs {actual})')
            if not re.fullmatch(r'\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?', version):
                fail(f'Dependency is not a pinned registry version: {name}')
    except (OSError, ValueError, KeyError, TypeError) as error:
        fail(f'Cannot validate package manifests: {error}')


def verify_assets(files: list[Path]) -> None:
    assets = ROOT / 'UnityProject/Assets'
    seen: dict[str, Path] = {}
    asset_files = [path for path in files if assets in path.parents]
    for path in asset_files:
        if path.name.startswith('.'):
            continue
        if path.suffix != '.meta':
            if not Path(str(path) + '.meta').is_file():
                fail(f'Missing Unity metadata: {path.relative_to(ROOT).as_posix()}')
            continue
        if not path.with_suffix('').exists():
            fail(f'Orphan Unity metadata: {path.relative_to(ROOT).as_posix()}')
        match = re.search(r'^guid: ([0-9a-f]{32})$', path.read_text(encoding='utf-8-sig'), re.MULTILINE)
        if not match:
            fail(f'Missing or malformed Unity GUID: {path.relative_to(ROOT).as_posix()}')
        elif match[1] in seen:
            fail(f'Duplicate Unity GUID: {path.relative_to(ROOT).as_posix()} and {seen[match[1]].relative_to(ROOT).as_posix()}')
        else:
            seen[match[1]] = path
    for directory in assets.rglob('*'):
        if directory.is_dir() and not directory.name.startswith('.') and not Path(str(directory) + '.meta').exists():
            fail(f'Missing folder metadata: {directory.relative_to(ROOT).as_posix()}')
    print(f'Unity metadata: {len(seen)} unique GUIDs. Package/built-in GUID resolution requires Unity.')


def verify_links(files: list[Path]) -> None:
    count = 0
    for path in files:
        if path.suffix.lower() != '.md':
            continue
        text = path.read_text(encoding='utf-8-sig')
        # Ignore illustrative paths in code fences, but verify Markdown and HTML media.
        text = re.sub(r'```.*?```', '', text, flags=re.DOTALL)
        targets = re.findall(r'!?\[[^\]]*\]\(<?([^\s)>]+)>?(?:\s+"[^"]*")?\)', text)
        targets += re.findall(r'(?:src|href)=["\']([^"\']+)["\']', text)
        for target in targets:
            parsed = urlsplit(target)
            if parsed.scheme or parsed.netloc or not parsed.path:
                continue
            if parsed.path.startswith('/'):
                fail(f'Nonportable absolute link in {path.relative_to(ROOT).as_posix()}: {target}')
                continue
            resolved = (path.parent / unquote(parsed.path)).resolve()
            if ROOT != resolved and ROOT not in resolved.parents:
                fail(f'Link escapes repository in {path.relative_to(ROOT).as_posix()}: {target}')
            elif not resolved.exists():
                fail(f'Broken local link in {path.relative_to(ROOT).as_posix()}: {target}')
            count += 1
    print(f'Documentation: checked {count} local file/media links (remote URLs and section anchors are not fetched).')


def main() -> int:
    files = repository_files()
    verify_layout(files)
    verify_packages()
    verify_assets(files)
    verify_links(files)
    if ERRORS:
        for error in ERRORS:
            print('FAIL: ' + error, file=sys.stderr)
        print(f'{len(ERRORS)} integrity issue(s).', file=sys.stderr)
        return 1
    print(f'PASS: {len(files)} repository files. Source integrity only; Unity/device testing is separate.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
