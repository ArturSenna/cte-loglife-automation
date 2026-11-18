# Builds the C# version of CTe LogLife.
#
#   .\build_dotnet.ps1                 run the tests, then publish to publish\CTe LogLife
#   .\build_dotnet.ps1 -SkipTests      publish without running the tests
#   .\build_dotnet.ps1 -Output <dir>   publish to another folder
#
# The published folder is self-contained: it runs on a machine without .NET installed.
# For the installer that goes to GitHub releases, use build_installer_dotnet.ps1.

param(
    [switch]$SkipTests,
    [string]$Output
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$props = Get-Content 'Directory.Build.props' -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') { throw 'Version not found in Directory.Build.props' }
$version = $Matches[1]
if (-not $Output) { $Output = Join-Path $PSScriptRoot 'publish\CTe LogLife' }
$Output = [IO.Path]::GetFullPath($Output)

Write-Host "CTe LogLife $version" -ForegroundColor Cyan

# Publishing replaces the folder. With the app open from it, half of the files would be deleted
# under a running robot: a running exe cannot be opened for writing, so that is the test.
$exe = Join-Path $Output 'CTe LogLife.exe'
if (Test-Path $exe) {
    try {
        [IO.File]::Open($exe, 'Open', 'ReadWrite', 'None').Dispose()
    }
    catch {
        throw "CTe LogLife is open from $Output. Close it first: publishing replaces that folder."
    }
}

if (-not $SkipTests) {
    Write-Host 'Running tests...' -ForegroundColor Cyan
    dotnet test CteLogLife.sln -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed: nothing was published.' }
}

Write-Host 'Publishing...' -ForegroundColor Cyan
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
dotnet publish src\CteLogLife.App\CteLogLife.App.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:DebugType=none --nologo -v q -o $Output
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

$size = (Get-ChildItem $Output -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Published to {0} ({1:N0} MB)" -f $Output, $size) -ForegroundColor Green
