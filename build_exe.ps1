# Build script for CTe LogLife executable
# This script builds the application using PyInstaller
# Usage: .\build_exe.ps1 [-Console]

param(
    [switch]$Console = $false
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "CTe LogLife - Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

if ($Console) {
    Write-Host "[INFO] Building with CONSOLE window enabled (for debugging)" -ForegroundColor Yellow
} else {
    Write-Host "[INFO] Building WITHOUT console window (GUI only)" -ForegroundColor Cyan
}
Write-Host ""

# Check if virtual environment is activated
if ($env:VIRTUAL_ENV) {
    Write-Host "[OK] Virtual environment detected: $env:VIRTUAL_ENV" -ForegroundColor Green
} else {
    Write-Host "[WARNING] No virtual environment detected" -ForegroundColor Yellow
    Write-Host "Attempting to activate venv..." -ForegroundColor Yellow
    
    if (Test-Path ".\venv\Scripts\Activate.ps1") {
        & ".\venv\Scripts\Activate.ps1"
        Write-Host "[OK] Virtual environment activated" -ForegroundColor Green
    } else {
        Write-Host "[ERROR] Could not find virtual environment" -ForegroundColor Red
        Write-Host "Please activate your virtual environment first" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""

# Check if PyInstaller is installed
Write-Host "Checking PyInstaller installation..." -ForegroundColor Cyan
try {
    $pyinstallerVersion = & pyinstaller --version 2>&1
    Write-Host "[OK] PyInstaller version: $pyinstallerVersion" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] PyInstaller not found" -ForegroundColor Red
    Write-Host "Installing PyInstaller..." -ForegroundColor Yellow
    pip install pyinstaller
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Failed to install PyInstaller" -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] PyInstaller installed successfully" -ForegroundColor Green
}

Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Cyan
if (Test-Path ".\dist") {
    Remove-Item -Path ".\dist" -Recurse -Force
    Write-Host "[OK] Removed old dist folder" -ForegroundColor Green
}
if (Test-Path ".\build") {
    Remove-Item -Path ".\build" -Recurse -Force
    Write-Host "[OK] Removed old build folder" -ForegroundColor Green
}

Write-Host ""

# Build the executable
Write-Host "Building executable..." -ForegroundColor Cyan
Write-Host "This may take several minutes..." -ForegroundColor Yellow
Write-Host ""

if ($Console) {
    # Temporarily modify the spec file to enable console
    $specContent = Get-Content "CTe_LogLife.spec" -Raw
    $specContent = $specContent -replace "console=False", "console=True"
    $specContent | Set-Content "CTe_LogLife_temp.spec"
    
    & pyinstaller CTe_LogLife_temp.spec --clean
    
    # Clean up temp spec file
    Remove-Item "CTe_LogLife_temp.spec" -Force
} else {
    & pyinstaller CTe_LogLife.spec --clean
}

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "[SUCCESS] Build completed successfully!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""
Write-Host "Executable location: .\dist\CTe LogLife\" -ForegroundColor Cyan
Write-Host "Main executable: .\dist\CTe LogLife\CTe LogLife.exe" -ForegroundColor Cyan

if ($Console) {
    Write-Host ""
    Write-Host "[DEBUG MODE] Console window is ENABLED" -ForegroundColor Yellow
    Write-Host "This version will show a console window for debugging" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Test the executable by running it from the dist folder" -ForegroundColor White
Write-Host "2. Run build_installer.ps1 to create the installer" -ForegroundColor White
Write-Host ""
