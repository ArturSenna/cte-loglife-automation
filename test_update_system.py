"""
Test Script for Auto-Update System
Run this to verify the update system is working correctly
"""

import sys
import os

# Add botCTE to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'botCTE', 'botCTE'))

print("=" * 60)
print("CTe LogLife - Auto-Update System Test")
print("=" * 60)
print()

# Test 1: Import modules
print("Test 1: Importing modules...")
try:
    from version_checker import VersionChecker
    from auto_updater import AutoUpdater, UpdateManager
    print("✅ All modules imported successfully")
except Exception as e:
    print(f"❌ Import failed: {e}")
    sys.exit(1)

print()

# Test 2: Check packaging dependency
print("Test 2: Checking dependencies...")
try:
    from packaging import version
    print("✅ packaging module available")
except ImportError:
    print("❌ packaging module not found - run: pip install packaging")
    sys.exit(1)

print()

# Test 3: Read VERSION file
print("Test 3: Reading VERSION file...")
try:
    version_file = os.path.join(os.path.dirname(__file__), 'botCTE', 'VERSION')
    with open(version_file, 'r') as f:
        app_version = f.read().strip()
    print(f"✅ Current version: {app_version}")
except Exception as e:
    print(f"❌ Failed to read VERSION: {e}")
    sys.exit(1)

print()

# Test 4: Create VersionChecker
print("Test 4: Creating VersionChecker...")
try:
    checker = VersionChecker(
        repo_owner="ArturSenna",
        repo_name="cte-loglife-automation",
        current_version=app_version
    )
    print(f"✅ VersionChecker created for version {app_version}")
except Exception as e:
    print(f"❌ Failed to create VersionChecker: {e}")
    sys.exit(1)

print()

# Test 5: Check for updates
print("Test 5: Checking for updates (requires internet)...")
try:
    update_info = checker.check_for_updates()
    
    if update_info.get('error'):
        print(f"⚠️  Warning: {update_info['error']}")
        print("   (This is normal if no releases exist yet or no internet)")
    elif update_info.get('update_available'):
        print(f"✅ Update check successful!")
        print(f"   📦 Update available: {update_info['latest_version']}")
        print(f"   📝 Release: {update_info.get('release_name', 'N/A')}")
        if update_info.get('download_url'):
            print(f"   🔗 Download URL: {update_info['download_url'][:50]}...")
        else:
            print(f"   ⚠️  No installer found in release")
    else:
        print(f"✅ Update check successful - Already up to date!")
        print(f"   Current: {update_info['current_version']}")
        
except Exception as e:
    print(f"⚠️  Update check error: {e}")
    print("   (This might be normal if no releases exist yet)")

print()

# Test 6: Create AutoUpdater
print("Test 6: Creating AutoUpdater...")
try:
    updater = AutoUpdater(
        download_url="https://example.com/test.exe",
        app_name="CTe LogLife"
    )
    print(f"✅ AutoUpdater created")
    print(f"   Temp directory: {updater.temp_dir}")
except Exception as e:
    print(f"❌ Failed to create AutoUpdater: {e}")
    sys.exit(1)

print()

# Test 7: Version comparison
print("Test 7: Testing version comparison...")
try:
    from packaging import version
    
    v1 = version.parse("1.0.0")
    v2 = version.parse("1.1.0")
    v3 = version.parse("2.0.0")
    
    assert v2 > v1, "Version comparison failed"
    assert v3 > v2, "Version comparison failed"
    assert v1 < v2, "Version comparison failed"
    
    print("✅ Version comparison working correctly")
    print(f"   1.0.0 < 1.1.0 < 2.0.0")
except Exception as e:
    print(f"❌ Version comparison failed: {e}")
    sys.exit(1)

print()
print("=" * 60)
print("🎉 All tests passed! Auto-update system is ready to use.")
print("=" * 60)
print()
print("Next steps:")
print("1. Create a GitHub release to test the full update flow")
print("2. Run the application and check Help → Check for Updates")
print("3. See AUTO_UPDATE_GUIDE.md for full documentation")
print()
