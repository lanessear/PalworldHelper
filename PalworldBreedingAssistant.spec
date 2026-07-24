# -*- mode: python ; coding: utf-8 -*-
from pathlib import Path

root = Path(SPECPATH)

a = Analysis(
    ['launcher.py'],
    pathex=[str(root)],
    binaries=[],
    datas=[
        (str(root / 'templates'), 'templates'),
        (str(root / 'data'), 'data'),
        (str(root / 'pal_name_aliases.json'), '.'),
        (str(root / 'passive_skill_aliases.json'), '.'),
        (str(root / 'pal_exporter.py'), '.'),
    ],
    hiddenimports=[
        'flask', 'jinja2', 'werkzeug', 'paramiko',
        'palworld_save_tools', 'palworld_save_tools.commands.convert'
    ],
    hookspath=[],
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
)
pyz = PYZ(a.pure)
exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name='PalworldBreedingAssistant',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
