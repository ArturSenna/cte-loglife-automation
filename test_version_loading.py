"""
Test script to verify VERSION file loading works correctly
"""
import os
import sys

print("=" * 60)
print("Testing VERSION File Loading")
print("=" * 60)
print()

# Test the version loading logic directly
def get_app_version():
    """Read version from VERSION file, supporting both dev and bundled environments."""
    _SCRIPT_DIR = os.path.join(os.path.dirname(__file__), 'botCTE', 'botCTE')
    version_locations = []
    
    # When running as PyInstaller bundle, try _MEIPASS first
    if getattr(sys, 'frozen', False):
        # Running as compiled executable
        bundle_dir = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
        version_locations.append(os.path.join(bundle_dir, 'VERSION'))
        version_locations.append(os.path.join(os.path.dirname(sys.executable), 'VERSION'))
    
    # Development environment locations
    version_locations.append(os.path.join(os.path.dirname(_SCRIPT_DIR), 'VERSION'))
    version_locations.append(os.path.join(_SCRIPT_DIR, 'VERSION'))
    
    print("Searching for VERSION file in:")
    for loc in version_locations:
        exists = "✅" if os.path.exists(loc) else "❌"
        print(f"  {exists} {loc}")
    print()
    
    # Try each location
    for version_file in version_locations:
        if os.path.exists(version_file):
            try:
                # Try UTF-8 first (standard encoding)
                with open(version_file, 'r', encoding='utf-8') as f:
                    version = f.read().strip()
                    if version:
                        print(f"✅ Version loaded from: {version_file}")
                        return version
            except (UnicodeError, UnicodeDecodeError):
                # Fallback to UTF-16
                try:
                    with open(version_file, 'r', encoding='utf-16') as f:
                        version = f.read().strip()
                        if version:
                            print(f"✅ Version loaded from: {version_file}")
                            return version
                except Exception:
                    pass
            except Exception as e:
                print(f"❌ Could not read VERSION from {version_file}: {e}")
                continue
    
    print("⚠️  Warning: VERSION file not found in any expected location")
    return '1.0.0'

# Test the function
print(f"Running in frozen mode: {getattr(sys, 'frozen', False)}")
print()

version = get_app_version()
print()
print(f"Loaded version: {version}")
print()

# Verify it matches expected version
expected_version = "1.2.0"
if version == expected_version:
    print(f"✅ SUCCESS: Version matches expected: {expected_version}")
else:
    print(f"⚠️  WARNING: Version mismatch - got '{version}', expected '{expected_version}'")

print()
print("=" * 60)
print("Test Complete")
print("=" * 60)
