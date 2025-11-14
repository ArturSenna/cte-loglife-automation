# PyInstaller runtime hook for OpenCV (cv2)
# This hook fixes the recursion error when loading cv2 binary extensions

import sys
import os

# Only run when frozen
if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
    # Prevent cv2 from being imported from wrong location
    # by ensuring the _MEIPASS path is used
    os.environ['OPENCV_IO_ENABLE_OPENEXR'] = '0'
