# Building CTe LogLife Executable and Installer

This guide explains how to build the CTe LogLife application into a distributable executable and installer.

## Prerequisites

1. **Python Environment**: Ensure your virtual environment is activated
2. **Dependencies**: All requirements from `requirements.txt` must be installed
3. **Inno Setup**: Download and install from [https://jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php) (only needed for creating the installer)

## Build Process

### Step 1: Build the Executable

Run the build script in PowerShell:

```powershell
.\build_exe.ps1
```

This script will:

- Check if PyInstaller is installed (and install it if needed)
- Clean previous builds
- Create a multi-file executable in `dist\CTe LogLife\`
- The main executable will be `CTe LogLife.exe`

**Build time**: 3-5 minutes depending on your system

**Output location**: `dist\CTe LogLife\`

### Step 2: Test the Executable

Before creating an installer, test the executable:

1. Navigate to `dist\CTe LogLife\`
2. Run `CTe LogLife.exe`
3. Verify all functionality works correctly
4. Check that all data files (Excel files, icons) are accessible

### Step 3: Build the Installer

Once you've verified the executable works, create the installer:

```powershell
.\build_installer.ps1
```

This script will:

- Verify the executable exists
- Check for Inno Setup installation
- Compile the installer using the `installer_script.iss` configuration
- Create the installer in `installer_output\`

**Output**: `installer_output\CTe_LogLife_Setup_v3.0.exe`

## File Structure

### Build Files Created

- `CTe_LogLife.spec` - PyInstaller specification file
- `build_exe.ps1` - PowerShell script to build the executable
- `build_installer.ps1` - PowerShell script to build the installer
- `installer_script.iss` - Inno Setup configuration

### Generated Folders

- `build\` - Temporary build files (can be deleted)
- `dist\CTe LogLife\` - The executable and all dependencies
- `installer_output\` - The final installer executable

## What Gets Included

The executable bundle includes:

- Main application (`CTe LogLife.exe`)
- All Python dependencies
- Data files:
  - `my_icon.ico`
  - `Complementares.xlsx`
  - `Alíquota.xlsx`
  - `resources\` folder (if exists)
- All necessary DLLs and Python libraries

## Customization

### Changing App Version

Edit `installer_script.iss`:

```iss
#define MyAppVersion "3.0"  ; Change this version number
```

### Adding More Data Files

Edit `CTe_LogLife.spec`:

```python
datas += [
    (os.path.join(botcte_dir, 'your_file.ext'), '.'),
]
```

### Installer Options

Edit `installer_script.iss` to customize:

- Installation directory
- Desktop icon creation
- Start menu shortcuts
- Language options

## Troubleshooting

### Build Fails with "Module not found"

- Ensure all dependencies are installed: `pip install -r requirements.txt`
- Activate your virtual environment before building

### Executable Doesn't Run

- Test it from the `dist\CTe LogLife\` folder first
- Check the console output (set `console=True` in the spec file temporarily)
- Verify all data files are present

### Inno Setup Not Found

- Download and install from [https://jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php)
- Ensure it's installed in the default location

### Missing DLLs or Resources

- Add them to the `datas` list in `CTe_LogLife.spec`
- Rebuild the executable

## Distribution

Once the installer is created:

1. Test it on a clean Windows machine (if possible)
2. The installer will:
   - Install to `C:\Program Files\CTe LogLife\` by default
   - Create Start Menu shortcuts
   - Optionally create Desktop shortcuts
   - Handle uninstallation cleanly

## Notes

- **Multi-file vs One-file**: This setup creates a multi-file executable for better performance and easier debugging
- **Antivirus**: Some antivirus software may flag PyInstaller executables. You may need to sign the executable or add exclusions
- **Updates**: To release a new version, increment the version number in `installer_script.iss` and rebuild

## Quick Reference

```powershell
# Complete build process
.\build_exe.ps1           # Build executable (3-5 minutes)
.\build_installer.ps1     # Build installer (1-2 minutes)

# Clean builds
Remove-Item -Recurse -Force dist, build
```
