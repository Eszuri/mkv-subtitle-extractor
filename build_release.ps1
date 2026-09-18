# MKS Subtitle Studio - Automated Complete Binary & Setup Installer Build Script

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

$OutputDir = Join-Path $ScriptDir "dist\MksStudio-v1.0-win-x64"
$ZipFile = Join-Path $ScriptDir "dist\MksStudio-v1.0-win-x64.zip"
$InstallerFile = Join-Path $ScriptDir "dist\MksStudio_Setup_v1.0.exe"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  MKS Subtitle Studio - Complete Build & Installer Creation" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Clean previous build
if (Test-Path $OutputDir) {
    Write-Host "[1/6] Cleaning previous dist directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}
if (Test-Path $ZipFile) {
    Remove-Item -Path $ZipFile -Force
}
if (Test-Path $InstallerFile) {
    Remove-Item -Path $InstallerFile -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 2. Ensure MKVToolNix binaries are present for bundling
Write-Host "[2/6] Verifying MKVToolNix bundled binaries..." -ForegroundColor Yellow
$LocalToolsDir = Join-Path $ScriptDir "src\MksStudio.UI\tools\mkvtoolnix"
if (-not (Test-Path $LocalToolsDir)) {
    New-Item -ItemType Directory -Path $LocalToolsDir -Force | Out-Null
}

$SystemMkvDir = "C:\Program Files\MKVToolNix"
if (-not (Test-Path "$LocalToolsDir\mkvmerge.exe") -and (Test-Path "$SystemMkvDir\mkvmerge.exe")) {
    Write-Host " Copying mkvmerge.exe & mkvextract.exe from system '$SystemMkvDir'..." -ForegroundColor Cyan
    Copy-Item "$SystemMkvDir\mkvmerge.exe" "$LocalToolsDir\" -Force
    Copy-Item "$SystemMkvDir\mkvextract.exe" "$LocalToolsDir\" -Force
}

# 3. Publish Self-Contained Binary
Write-Host "[3/5] Publishing self-contained win-x64 release build..." -ForegroundColor Yellow
dotnet publish src/MksStudio.UI/MksStudio.UI.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed!"
    exit 1
}

# 4. Copy Samples & Documentation
Write-Host "[4/5] Copying sample assets and documentation..." -ForegroundColor Yellow
$SampleDest = Join-Path $OutputDir "sample"
New-Item -ItemType Directory -Path $SampleDest -Force | Out-Null
if (Test-Path "sample/demo_multitrack.mks") {
    Copy-Item "sample/demo_multitrack.mks" "$SampleDest\" -Force
}
Copy-Item "README.md" "$OutputDir\" -Force

# 5. Create Distribution Zip Archive & Setup Installer
Write-Host "[5/5] Creating portable ZIP distribution archive & compiling installer..." -ForegroundColor Yellow
Compress-Archive -Path "$OutputDir\*" -DestinationPath $ZipFile -Force
$InnoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $InnoCompiler)) {
    $InnoCompiler = "C:\Program Files\Inno Setup 6\ISCC.exe"
}

if (Test-Path $InnoCompiler) {
    & $InnoCompiler "installer\setup.iss"
    if ($LASTEXITCODE -eq 0) {
        Write-Host " Setup Installer generated successfully!" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation finished with code $LASTEXITCODE"
    }
} else {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found. Skipping .exe installer compilation."
}

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " BUILD & PACKAGING COMPLETE!" -ForegroundColor Green
Write-Host " 1. Portable Folder   : $OutputDir" -ForegroundColor White
Write-Host " 2. Distribution ZIP  : $ZipFile" -ForegroundColor White
if (Test-Path $InstallerFile) {
    Write-Host " 3. Setup Installer   : $InstallerFile" -ForegroundColor Cyan
}
Write-Host "==========================================================" -ForegroundColor Green
