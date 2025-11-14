# Auto-Update System Guide

## Overview

The CTe LogLife application now includes an automatic update system that checks for new releases from GitHub and allows users to download and install updates directly from within the application.

## Features

- **Automatic Update Checking**: Checks for updates on startup (runs in background)
- **Manual Update Check**: Users can manually check via Help > Check for Updates menu
- **Beautiful Update Dialog**: Shows version info, release notes, and download progress
- **One-Click Installation**: Download and install updates with a single click
- **GitHub Releases Integration**: Pulls updates from GitHub releases automatically
- **Semantic Versioning**: Uses proper semantic versioning (e.g., 1.0.0, 1.2.3)

## How It Works

### For Users

1. **Automatic Check on Startup**

   - When you start the application, it checks for updates in the background
   - If an update is available, a dialog will appear with details
   - You can choose to install now or skip

2. **Manual Check**

   - Go to **Help → Check for Updates**
   - See if you're up to date or if a new version is available
   - Download and install if available

3. **Installing Updates**
   - Click "Download and Install" in the update dialog
   - The installer will download with a progress bar
   - Once downloaded, the installer will launch
   - The application will close to allow the update

### For Developers

#### File Structure

```
botCTE/
  botCTE/
    version_checker.py    # Checks GitHub for new releases
    auto_updater.py        # Handles download and installation
    Base.py                # Main app (integrates update UI)
  VERSION                  # Current version (1.0.0)
update_config.ini          # Update configuration
```

#### Creating a New Release

1. **Update the VERSION file**

   ```
   # In botCTE/VERSION
   1.1.0
   ```

2. **Build the executable**

   ```powershell
   .\build_exe.ps1
   ```

3. **Build the installer**

   ```powershell
   .\build_installer.ps1
   ```

4. **Create GitHub Release**

   - Go to GitHub repository
   - Click "Releases" → "Draft a new release"
   - Tag version: `v1.1.0` (must start with 'v')
   - Release title: `Version 1.1.0` or descriptive name
   - Description: Add release notes (what's new, bug fixes, etc.)
   - Upload the installer: `CTe_LogLife_Setup.exe`
   - Publish release

5. **Users Get Update**
   - When users start the app, they'll be notified
   - They can download and install with one click

#### Version Numbering

Use **semantic versioning** (MAJOR.MINOR.PATCH):

- **MAJOR**: Breaking changes (1.0.0 → 2.0.0)
- **MINOR**: New features (1.0.0 → 1.1.0)
- **PATCH**: Bug fixes (1.0.0 → 1.0.1)

Examples:

- `1.0.0` - Initial release
- `1.0.1` - Bug fix
- `1.1.0` - New feature added
- `2.0.0` - Major rewrite or breaking change

## Configuration

Edit `update_config.ini` to customize update behavior:

```ini
[Repository]
REPO_OWNER = ArturSenna
REPO_NAME = cte-loglife-automation

[Update]
CHECK_ON_STARTUP = true          # Check on app startup
CHANNEL = stable                 # stable, beta, or dev
AUTO_DOWNLOAD = false            # Auto-download (still requires user to install)
SHOW_NOTIFICATION = true         # Show update available notification

[Advanced]
CHECK_INTERVAL_DAYS = 1          # How often to check (0 = every startup)
REQUEST_TIMEOUT = 10             # Network timeout in seconds
```

## API Reference

### VersionChecker Class

```python
from version_checker import VersionChecker

# Create checker
checker = VersionChecker(
    repo_owner="ArturSenna",
    repo_name="cte-loglife-automation",
    current_version="1.0.0"
)

# Check for updates
update_info = checker.check_for_updates()

# Returns:
{
    'update_available': True/False,
    'latest_version': '1.1.0',
    'current_version': '1.0.0',
    'download_url': 'https://...',
    'release_notes': 'What\'s new...',
    'release_name': 'Version 1.1.0'
}
```

### AutoUpdater Class

```python
from auto_updater import AutoUpdater

# Create updater
updater = AutoUpdater(
    download_url="https://github.com/.../installer.exe",
    app_name="CTe LogLife"
)

# Set progress callback
def on_progress(progress):
    print(f"Download: {progress}%")

updater.set_download_callback(on_progress)

# Download and install
updater.download_and_install(silent=False)
```

### UpdateManager Class

```python
from auto_updater import UpdateManager
from version_checker import VersionChecker

# Create components
checker = VersionChecker(...)
manager = UpdateManager(checker)

# Check and update
update_info = manager.check_and_prompt_update()
if update_info['update_available']:
    manager.perform_update(update_info)
```

## UI Integration

The update system is integrated into the main application:

1. **Menu Bar**: Help → Check for Updates
2. **About Dialog**: Shows current version
3. **Startup Check**: Background check on launch
4. **Update Dialog**: Beautiful UI for downloading/installing

## Testing

### Test Update Check

```python
python -c "from botCTE.version_checker import VersionChecker; vc = VersionChecker(current_version='0.1.0'); print(vc.check_for_updates())"
```

### Test Download

```python
python -c "from botCTE.auto_updater import AutoUpdater; au = AutoUpdater(download_url='URL'); au.download_installer()"
```

## Troubleshooting

### "Failed to fetch release information"

- **Cause**: Network issue or GitHub API rate limit
- **Solution**: Wait a few minutes and try again
- **Check**: Visit https://github.com/ArturSenna/cte-loglife-automation/releases

### "No download URL available"

- **Cause**: Release doesn't have an installer file (.exe) attached
- **Solution**: Ensure the GitHub release has the installer uploaded
- **Workaround**: Download manually from GitHub releases page

### Update check doesn't work

- **Check**: Ensure `packaging` is installed: `pip install packaging`
- **Check**: Verify VERSION file exists and contains valid version
- **Check**: GitHub repository name matches in Base.py

### Installer doesn't start

- **Check**: Antivirus might be blocking the downloaded file
- **Solution**: Whitelist the application or download location
- **Alternative**: Download installer manually from GitHub

## Best Practices

1. **Always test locally** before creating a release
2. **Write clear release notes** describing changes
3. **Use semantic versioning** consistently
4. **Test the installer** before publishing release
5. **Keep VERSION file updated** with each release
6. **Tag releases properly** (v1.0.0, v1.1.0, etc.)

## Security Considerations

- Downloads are from GitHub (HTTPS)
- User confirmation required before installation
- Installer signature should be added (future enhancement)
- No automatic silent installation without user consent

## Future Enhancements

Potential improvements for the update system:

- [ ] Code signing for installer
- [ ] Delta updates (download only changes)
- [ ] Rollback capability
- [ ] Beta channel support
- [ ] Offline update capability
- [ ] Update scheduling
- [ ] Silent background updates

## Support

For issues or questions:

- GitHub Issues: https://github.com/ArturSenna/cte-loglife-automation/issues
- Email: [your-email]
- Documentation: README.md

---

**Note**: This update system requires the application to be distributed as an installer (via Inno Setup) for the full update functionality to work properly.
