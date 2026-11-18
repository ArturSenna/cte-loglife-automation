# Builds the installer of the C# version of CTe LogLife: the file to attach to a GitHub release.
#
#   .\build_installer_dotnet.ps1              tests, publish, trial installation, installer
#   .\build_installer_dotnet.ps1 -SkipTests   without running the tests
#   .\build_installer_dotnet.ps1 -SkipCheck   without the trial installation
#   .\build_installer_dotnet.ps1 -KeepFiles   keep the published folder (obj\installer) afterwards
#
# Result: installer_output\CTe_LogLife_Setup_v<version>.exe, with <version> taken from
# Directory.Build.props. Installed copies look at the latest GitHub release, compare its tag with
# their own version and download the .exe whose name contains "setup": keep that file name, and
# tag the release v<version>.
#
# The app is published to a folder of its own, so this can run while CTe LogLife is open from
# publish\CTe LogLife.
#
# The trial installation compiles a copy of the installer with an AppId of its own (no shortcuts,
# no registry entries, no administrator rights), installs it into a temporary folder made to look
# like an installed Python version, and compares the result with what was published. It never
# touches a real installation, and no installer is produced when it fails.
#
# Needs Inno Setup 6.3 or newer: https://jrsoftware.org/isdl.php

param(
    [switch]$SkipTests,
    [switch]$SkipCheck,
    [switch]$KeepFiles
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$definition = Join-Path $PSScriptRoot 'installer_dotnet.iss'
$stage = Join-Path $PSScriptRoot 'obj\installer\CTe LogLife'
$outDir = Join-Path $PSScriptRoot 'installer_output'
$warnings = @()

function Step($text) { Write-Host $text -ForegroundColor Cyan }

function GitLines {
    # Nothing here depends on git: without it (or outside a repository) the checks are skipped.
    try { return @(& git.exe @args 2>$null) } catch { return @() }
}

# Installs the self-check copy of the installer over a fake Python installation and compares.
function Test-Installer($iscc, $version) {
    $work = Join-Path ([IO.Path]::GetTempPath()) ('cte-loglife-installer-check-' + [guid]::NewGuid().ToString('N'))
    $target = Join-Path $work 'app'
    $log = Join-Path $work 'install.log'
    $login = "LOGLIFE_USER=check`r`nLOGLIFE_PASSWORD=check`r`n"

    New-Item -ItemType Directory -Force -Path (Join-Path $target '_internal'), (Join-Path $target 'Templates') | Out-Null
    [IO.File]::WriteAllText((Join-Path $target '_internal\.env'), $login)
    [IO.File]::WriteAllText((Join-Path $target '_internal\python311.dll'), 'python version')
    [IO.File]::WriteAllText((Join-Path $target 'Templates\removed-in-this-version.png'), 'old screenshot')
    [IO.File]::WriteAllText((Join-Path $target 'folderpath.txt'), 'C:\CTe')

    & $iscc /Q /DSelfCheck "/DMyAppVersion=$version" "/DSourceDir=$stage" "/O$work" /Fcheck $definition
    if ($LASTEXITCODE -ne 0) { throw 'The installer script did not compile.' }

    $arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR=`"$target`" /LOG=`"$log`""
    $setup = Start-Process -FilePath (Join-Path $work 'check.exe') -ArgumentList $arguments -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw "The trial installation ended with code $($setup.ExitCode). Log: $log" }

    $problems = @()
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File -Force) {
        $relative = $file.FullName.Substring($stage.Length + 1)
        $installed = Join-Path $target $relative
        if (-not (Test-Path -LiteralPath $installed)) { $problems += "not installed: $relative" }
        elseif ((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            $problems += "installed with other contents: $relative"
        }
    }
    # Only the operator's own files of the Python version may stay; its program files must be gone.
    $kept = '.env', 'folderpath.txt'
    foreach ($file in Get-ChildItem -LiteralPath $target -Recurse -File -Force) {
        $relative = $file.FullName.Substring($target.Length + 1)
        if ($kept -contains $relative) { continue }
        if (-not (Test-Path -LiteralPath (Join-Path $stage $relative))) { $problems += "left behind: $relative" }
    }
    $keptLogin = Join-Path $target '.env'
    if (-not (Test-Path -LiteralPath $keptLogin)) { $problems += 'the login file of the Python version (_internal\.env) was lost' }
    elseif ([IO.File]::ReadAllText($keptLogin) -ne $login) { $problems += 'the login file of the Python version was changed' }
    if (-not (Test-Path -LiteralPath (Join-Path $target 'folderpath.txt'))) { $problems += 'folderpath.txt of the Python version was lost' }

    if ($problems.Count -gt 0) {
        $shown = ($problems | Select-Object -First 15) -join "`n  "
        throw "The trial installation is wrong ($($problems.Count) problem(s)):`n  $shown`nFolder: $target`nLog: $log"
    }
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------- version

$props = Get-Content 'Directory.Build.props' -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') { throw 'Version not found in Directory.Build.props' }
$version = $Matches[1].Trim()
if ($version -notmatch '^\d+(\.\d+){1,3}$') {
    throw "Version '$version' must be numbers separated by dots (2.0.0): installed copies compare it as a number."
}

$released = @(GitLines tag --list | ForEach-Object {
        $parsed = $null
        if ([version]::TryParse($_.Trim().TrimStart('v', 'V'), [ref]$parsed)) { $parsed }
    } | Sort-Object -Descending)
if ($released.Count -gt 0 -and [version]$version -le $released[0]) {
    $warnings += "Version $version is not above the newest tag ($($released[0])). Installed copies only update to a " +
        'higher version: raise <Version> in Directory.Build.props.'
}
if (@(GitLines status --porcelain).Count -gt 0) {
    $warnings += 'There are uncommitted changes: commit and push them, or the release tag will not match this installer.'
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php' }

# ---------------------------------------------------------------- publish

& (Join-Path $PSScriptRoot 'build_dotnet.ps1') -SkipTests:$SkipTests -Output $stage

$built = (Get-Item -LiteralPath (Join-Path $stage 'CTe LogLife.exe')).VersionInfo.ProductVersion
if ($built -ne $version) { throw "The published exe says version $built, not $version." }
if (-not (Get-ChildItem -LiteralPath (Join-Path $stage 'Templates') -Recurse -File -ErrorAction SilentlyContinue)) {
    throw 'The published app has no Bsoft screenshots (Templates).'
}
if (@(Get-ChildItem -LiteralPath (Join-Path $stage 'Data') -Filter *.xlsx -ErrorAction SilentlyContinue).Count -lt 2) {
    throw 'The published app has no tax tables (Data).'
}
# The installer is public: a login or a machine's own settings must never travel in it.
$private = @(Get-ChildItem -LiteralPath $stage -Recurse -File -Force |
        Where-Object { $_.Name -like '.env*' -or $_.Name -in 'credentials.bin', 'settings.json' })
if ($private.Count -gt 0) {
    throw "These files must not go into a public installer:`n  $(($private | ForEach-Object FullName) -join "`n  ")"
}

# ---------------------------------------------------------------- installer

if (-not $SkipCheck) {
    Step 'Trying the installer in a temporary folder...'
    Test-Installer $iscc $version
    Write-Host 'Trial installation matches the published app.' -ForegroundColor Green
}

Step 'Building the installer (the compression takes about a minute)...'
$installer = Join-Path $outDir "CTe_LogLife_Setup_v$version.exe"
if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Force }
& $iscc /Q "/DMyAppVersion=$version" "/DSourceDir=$stage" "/O$outDir" $definition
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
if (-not (Test-Path -LiteralPath $installer)) { throw "Inno Setup did not write $installer" }

if (-not $KeepFiles) {
    Remove-Item -LiteralPath (Split-Path $stage -Parent) -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------- what to do with it

$size = (Get-Item -LiteralPath $installer).Length / 1MB
Write-Host ''
Write-Host ("Installer: {0} ({1:N0} MB)" -f $installer, $size) -ForegroundColor Green
Write-Host "SHA-256:   $((Get-FileHash -LiteralPath $installer).Hash)"
Write-Host ''
Write-Host "To release it, create a GitHub release with the tag v$version and attach that file:"
$origin = GitLines remote get-url origin | Select-Object -First 1
if ($origin -match 'github\.com[:/](?<owner>[^/]+)/(?<repo>.+?)(\.git)?/?$') {
    Write-Host "  https://github.com/$($Matches.owner)/$($Matches.repo)/releases/new?tag=v$version&title=CTe%20LogLife%20$version"
}
Write-Host 'Installed copies offer the update once it is the latest release. A release marked as'
Write-Host 'pre-release is not offered to them: use that to try the installer on one machine first.'
foreach ($warning in $warnings) {
    Write-Host ''
    Write-Host "WARNING: $warning" -ForegroundColor Yellow
}
