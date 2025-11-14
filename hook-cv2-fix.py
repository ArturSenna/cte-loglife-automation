# PyInstaller hook for cv2 (OpenCV)
# This prevents the recursion error during cv2 loading

from PyInstaller.utils.hooks import collect_submodules, get_module_file_attribute
import os

# Collect all cv2 submodules to prevent splitting
hiddenimports = collect_submodules('cv2')

# Get cv2 location
cv2_path = get_module_file_attribute('cv2')
cv2_dir = os.path.dirname(cv2_path)

# Collect all DLL files from cv2 directory
binaries = []
if os.path.exists(cv2_dir):
    for file in os.listdir(cv2_dir):
        if file.endswith('.dll') or file.endswith('.pyd'):
            full_path = os.path.join(cv2_dir, file)
            binaries.append((full_path, 'cv2'))

# Collect data files
datas = []
for file in os.listdir(cv2_dir):
    if file.endswith('.xml') or file.endswith('.dat'):
        full_path = os.path.join(cv2_dir, file)
        datas.append((full_path, 'cv2'))
