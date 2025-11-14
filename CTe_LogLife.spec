# -*- mode: python ; coding: utf-8 -*-

"""
PyInstaller spec file for CTe LogLife
Creates a multi-file executable (not onefile) for better performance and easier debugging
"""

import os
import sys
from PyInstaller.utils.hooks import collect_data_files, collect_submodules, collect_dynamic_libs, collect_all
import certifi

# Get the base directory
block_cipher = None
base_dir = os.path.abspath('.')
botcte_dir = os.path.join(base_dir, 'botCTE', 'botCTE')

# Collect ALL cv2 components using collect_all (most comprehensive)
cv2_datas, cv2_binaries, cv2_hiddenimports = collect_all('cv2', include_py_files=True)

# Get certifi certificate bundle location
certifi_path = os.path.dirname(certifi.__file__)

# Collect all botcity hidden imports
hiddenimports = [
    'botcity.core',
    'botcity.maestro',
    'pandas._libs.tslibs.timedeltas',
    'pandas._libs.tslibs.nattype',
    'pandas._libs.tslibs.np_datetime',
    'pandas._libs.skiplist',
    'openpyxl.cell._writer',
    'tkcalendar',
    'ttkthemes',
    'PIL._tkinter_finder',
    'numpy.core._multiarray_umath',
    'certifi',
]

# Add all submodules
hiddenimports += collect_submodules('botcity')
hiddenimports += collect_submodules('ttkthemes')
hiddenimports += collect_submodules('babel')

# Add cv2 hidden imports from collect_all
hiddenimports += cv2_hiddenimports

# Exclude problematic cv2 imports to prevent recursion
excludes = []

# Collect data files from packages
datas = cv2_datas[:]  # Start with cv2 datas from collect_all
datas += collect_data_files('ttkthemes')
datas += collect_data_files('babel')
datas += collect_data_files('tkcalendar')

# Add certifi certificates for HTTPS requests
datas += collect_data_files('certifi')
# Also add the cacert.pem file explicitly
datas += [(os.path.join(certifi_path, 'cacert.pem'), 'certifi')]

# Collect OpenCV binaries using collect_all results
binaries = cv2_binaries[:]

# Add application-specific data files
datas += [
    (os.path.join(botcte_dir, 'my_icon.ico'), '.'),
    (os.path.join(botcte_dir, 'Complementares.xlsx'), '.'),
    (os.path.join(botcte_dir, 'Alíquota.xlsx'), '.'),
]

# Add resources folder if it exists
resources_dir = os.path.join(botcte_dir, 'resources')
if os.path.exists(resources_dir):
    datas += [(resources_dir, 'resources')]

a = Analysis(
    [os.path.join(botcte_dir, 'Base.py')],
    pathex=[botcte_dir],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=excludes,
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='CTe LogLife',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=False,  # Set to False for GUI application (no console window)
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon=os.path.join(botcte_dir, 'my_icon.ico'),
)

coll = COLLECT(
    exe,
    a.binaries,
    a.zipfiles,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='CTe LogLife',
)
