# Quick Start: Force Update Feature

## What's New? 🎉

CTe LogLife now has an **automatic update system**! No more manual downloads or complex update processes.

## For Users

### Automatic Updates

1. **On Startup**: The app automatically checks for updates
2. **If Update Available**: A dialog appears with update details
3. **One Click**: Click "Download and Install" to update
4. **Done**: App closes, installer runs, you're updated!

### Manual Update Check

- **Menu**: Help → Check for Updates
- **Result**: See if you're up to date or install available updates

### Update Dialog Features

- 📋 Current vs. Latest version comparison
- 📝 Release notes (what's new)
- 📊 Download progress bar
- ⬇️ One-click download and install

## For Developers

### Quick Release Process

1. **Update Version**

   ```powershell
   # Edit botCTE/VERSION
   echo "1.1.0" > botCTE\VERSION
   ```

2. **Build Everything**

   ```powershell
   # Build executable
   .\build_exe.ps1

   # Build installer
   .\build_installer.ps1
   ```

3. **Create GitHub Release**

   - Go to: https://github.com/ArturSenna/cte-loglife-automation/releases/new
   - Tag: `v1.1.0` (with 'v' prefix!)
   - Title: "Version 1.1.0 - [Brief Description]"
   - Description: Add release notes
   - Upload: `installer_output/CTe_LogLife_Setup.exe`
   - Click: "Publish release"

4. **Users Get Notified Automatically!** ✅

### Version Numbers

- `1.0.0` → `1.0.1` = Bug fix
- `1.0.0` → `1.1.0` = New feature
- `1.0.0` → `2.0.0` = Major change

### Files Created

```
botCTE/botCTE/
  ├── version_checker.py    ← Checks GitHub for updates
  ├── auto_updater.py        ← Downloads and installs updates
  └── Base.py                ← Updated with UI integration

botCTE/
  └── VERSION                ← Now uses 1.0.0 format

Root/
  ├── update_config.ini      ← Configuration options
  └── AUTO_UPDATE_GUIDE.md   ← Full documentation
```

## Testing

### Test Version Check

```powershell
cd botCTE\botCTE
python -c "from version_checker import VersionChecker; print(VersionChecker(current_version='1.0.0').check_for_updates())"
```

### Test in Application

1. Run the app: `python -m botCTE.botCTE`
2. Check: Help → Check for Updates
3. Should show: "You are using the latest version!" (if no releases yet)

## Common Issues

**"Module not found: packaging"**

```powershell
pip install packaging
```

**"Failed to fetch release information"**

- Check internet connection
- Verify repository exists: ArturSenna/cte-loglife-automation
- Wait a few minutes (GitHub API rate limit)

**Update dialog doesn't appear**

- Create a GitHub release first
- Make sure release has .exe file attached
- Verify VERSION file is lower than release tag

## Requirements

✅ Python package: `packaging` (added to requirements.txt)
✅ GitHub repository with releases feature
✅ Inno Setup installer (already configured)
✅ Internet connection (to check for updates)

## Next Steps

1. **Install packaging**: `pip install packaging`
2. **Test the feature**: Run app and check Help → Check for Updates
3. **Create first release**: Follow "Quick Release Process" above
4. **Test update flow**: Lower VERSION to 0.9.0, run app, should offer update

## Configuration

Edit `update_config.ini` to customize:

```ini
CHECK_ON_STARTUP = true    # Check when app starts
AUTO_DOWNLOAD = false      # Don't auto-download (user clicks button)
SHOW_NOTIFICATION = true   # Show when update available
```

## Summary

✨ **What You Get:**

- Automatic update checking on startup
- Beautiful update dialog with release notes
- One-click download and installation
- Manual update check from menu
- GitHub releases integration
- Progress tracking during download

🚀 **How to Release:**

1. Update VERSION file
2. Build exe and installer
3. Create GitHub release with installer
4. Users get notified automatically

📖 **Full Documentation:** See AUTO_UPDATE_GUIDE.md

---

**Ready to Use!** The feature is fully integrated and ready for testing. Create your first GitHub release to see it in action! 🎊
