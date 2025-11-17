"""
Test script to verify resource loading works correctly for PyInstaller builds
Tests: VERSION file, icon file, Excel files, .env file
"""
import os
import sys

print("=" * 70)
print("Testing Resource Loading for PyInstaller Build")
print("=" * 70)
print()

# Test frozen state detection
print(f"Running in frozen mode: {getattr(sys, 'frozen', False)}")
if getattr(sys, 'frozen', False):
    print(f"sys._MEIPASS: {getattr(sys, '_MEIPASS', 'Not available')}")
    print(f"sys.executable: {sys.executable}")
print()

# Test the _SCRIPT_DIR logic from each module
print("-" * 70)
print("Testing _SCRIPT_DIR logic from each module:")
print("-" * 70)

# Simulate the logic from Base.py
if getattr(sys, 'frozen', False):
    base_script_dir = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
else:
    base_script_dir = os.path.join(os.path.dirname(__file__), 'botCTE', 'botCTE')

print(f"Base.py _SCRIPT_DIR would be: {base_script_dir}")

# Check for required resources
resources_to_check = [
    ('my_icon.ico', 'Icon file'),
    ('Complementares.xlsx', 'Complementares Excel'),
    ('Alíquota.xlsx', 'Alíquota Excel'),
    ('.env', 'Environment file'),
    ('VERSION', 'Version file')
]

print()
print("-" * 70)
print("Checking for required resources:")
print("-" * 70)

all_found = True
for resource, description in resources_to_check:
    resource_path = os.path.join(base_script_dir, resource)
    exists = os.path.exists(resource_path)
    status = "✅" if exists else "❌"
    print(f"{status} {description:20s} : {resource_path}")
    if not exists:
        all_found = False

print()
print("-" * 70)
print("Summary:")
print("-" * 70)

if all_found:
    print("✅ SUCCESS: All required resources found!")
    print()
    print("Your PyInstaller build should work correctly.")
else:
    print("⚠️  WARNING: Some resources are missing!")
    print()
    print("This is expected in development mode.")
    print("After building with PyInstaller, these files should be bundled.")

print()
print("=" * 70)
print("Test Complete")
print("=" * 70)
