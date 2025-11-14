# Build Installer Script for CTe LogLife
# This script compiles the Inno Setup installer

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "CTe LogLife - Installer Builder" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Check if dist folder exists
if (-not (Test-Path ".\dist\CTe LogLife")) {
    Write-Host "[ERROR] Executable not found!" -ForegroundColor Red
    Write-Host "Please run build_exe.ps1 first to create the executable" -ForegroundColor Yellow
    exit 1
}

Write-Host "[OK] Executable found" -ForegroundColor Green
Write-Host ""

# Check for Inno Setup
Write-Host "Checking for Inno Setup..." -ForegroundColor Cyan

$innoSetupPaths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)

$isccPath = $null
foreach ($path in $innoSetupPaths) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
}

if ($null -eq $isccPath) {
    Write-Host "[ERROR] Inno Setup not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Inno Setup from:" -ForegroundColor Yellow
    Write-Host "https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "After installation, run this script again." -ForegroundColor Yellow
    
    # Offer to open the download page
    $response = Read-Host "Would you like to open the download page now? (Y/N)"
    if ($response -eq 'Y' -or $response -eq 'y') {
        Start-Process "https://jrsoftware.org/isdl.php"
    }
    exit 1
}

Write-Host "[OK] Inno Setup found at: $isccPath" -ForegroundColor Green
Write-Host ""

# Create output directory if it doesn't exist
if (-not (Test-Path ".\installer_output")) {
    New-Item -ItemType Directory -Path ".\installer_output" | Out-Null
}

# Compile the installer
Write-Host "Building installer..." -ForegroundColor Cyan
Write-Host "This may take a few minutes..." -ForegroundColor Yellow
Write-Host ""

& $isccPath "installer_script.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Installer build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "[SUCCESS] Installer created successfully!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
Write-Host ""

# Find the created installer
$installerFile = Get-ChildItem -Path ".\installer_output\*.exe" | Select-Object -First 1

if ($installerFile) {
    Write-Host "Installer location: $($installerFile.FullName)" -ForegroundColor Cyan
    Write-Host "Installer size: $([math]::Round($installerFile.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "You can now distribute this installer to users!" -ForegroundColor Green
    Write-Host ""
    
    # Offer to open the folder
    $response = Read-Host "Would you like to open the installer folder? (Y/N)"
    if ($response -eq 'Y' -or $response -eq 'y') {
        Invoke-Item ".\installer_output"
    }
} else {
    Write-Host "[WARNING] Could not find the installer file" -ForegroundColor Yellow
}
