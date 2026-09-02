# SeaCull Build Script
# Creates two distribution packages: Full and Plugin Only

param(
    [string]$SevenZipPath = ""
)

Set-Location $PSScriptRoot

function Resolve-RepoPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $PSScriptRoot $Path
}

function Find-SevenZip {
    $commonPaths = @(
        "${env:ProgramFiles}\7-Zip\7z.exe",
        "${env:ProgramFiles(x86)}\7-Zip\7z.exe",
        "${env:ProgramW6432}\7-Zip\7z.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    try {
        $pathResult = Get-Command "7z.exe" -ErrorAction Stop
        return $pathResult.Source
    }
    catch {
        return $null
    }
}

function New-ZipArchive {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path $DestinationPath) {
        Remove-Item (Resolve-RepoPath $DestinationPath) -Force
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory((Resolve-RepoPath $SourcePath), (Resolve-RepoPath $DestinationPath))
    Write-Host "Created: $DestinationPath" -ForegroundColor Green
}

function New-SevenZipArchive {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$SevenZipExe
    )

    if (Test-Path $DestinationPath) {
        Remove-Item (Resolve-RepoPath $DestinationPath) -Force
    }

    $currentDir = Get-Location
    $absoluteDestinationPath = Resolve-RepoPath $DestinationPath
    Set-Location (Resolve-RepoPath $SourcePath)

    $arguments = @("a", "-t7z", "`"$absoluteDestinationPath`"", "*")

    try {
        $process = Start-Process -FilePath $SevenZipExe -ArgumentList $arguments -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -eq 0) {
            Write-Host "Created: $DestinationPath" -ForegroundColor Green
        }
        else {
            Write-Host "Failed to create 7z archive: $DestinationPath (Exit code: $($process.ExitCode))" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "Error creating 7z archive: $_" -ForegroundColor Red
    }
    finally {
        Set-Location $currentDir
    }
}

function Install-BepInEx {
    param(
        [string]$Destination = "BepInExBin",
        [string]$Version = "5.4.23.5"
    )

    $zipName = "BepInEx_win_x64_$Version.zip"
    $url = "https://github.com/BepInEx/BepInEx/releases/download/v$Version/$zipName"

    Write-Host "Downloading BepInEx $Version (win x64)..." -ForegroundColor White
    Write-Host "Using $zipName" -ForegroundColor Green

    $zipPath = Join-Path $env:TEMP "SeaCull_BepInEx_win_x64.zip"
    Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing

    if (Test-Path $Destination) {
        Remove-Item $Destination -Recurse -Force
    }

    Expand-Archive -Path $zipPath -DestinationPath $Destination -Force
    Remove-Item $zipPath -Force
    Write-Host "Extracted BepInEx to $Destination" -ForegroundColor Green
}

Write-Host "SeaCull Build Script Starting..." -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

Install-BepInEx

Write-Host "Building project..." -ForegroundColor White
& dotnet build -c Release -p:Optimize=true --disable-build-servers
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
Write-Host "Project built successfully!" -ForegroundColor Green

Write-Host ""

$dllPath = "bin\Release\net35\SeaCull.dll"
$configPath = "SeaCull.ini"
$bepInExBinPath = "BepInExBin"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: $dllPath not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $configPath)) {
    Write-Host "ERROR: $configPath not found!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $bepInExBinPath)) {
    Write-Host "ERROR: $bepInExBinPath folder not found!" -ForegroundColor Red
    exit 1
}

Write-Host "All required files found!" -ForegroundColor Green

if ([string]::IsNullOrEmpty($SevenZipPath)) {
    $SevenZipPath = Find-SevenZip
}

if ([string]::IsNullOrEmpty($SevenZipPath) -and -not $env:CI -and -not $env:GITHUB_ACTIONS) {
    $SevenZipPath = Read-Host "Please enter the full path to 7z.exe (or press Enter to skip 7z creation)"
}

$use7Zip = $false
if (-not [string]::IsNullOrEmpty($SevenZipPath) -and (Test-Path $SevenZipPath)) {
    $use7Zip = $true
    Write-Host "Using 7-Zip: $SevenZipPath" -ForegroundColor Green
}
else {
    Write-Host "7-Zip not available - will create .zip files only" -ForegroundColor Yellow
}

$fullPackagePath = "SeaCull - Full"
$pluginOnlyPath = "SeaCull - Plugin Only"

if (Test-Path $fullPackagePath) {
    Remove-Item $fullPackagePath -Recurse -Force
    Write-Host "Cleaned up existing $fullPackagePath folder" -ForegroundColor Yellow
}

if (Test-Path $pluginOnlyPath) {
    Remove-Item $pluginOnlyPath -Recurse -Force
    Write-Host "Cleaned up existing $pluginOnlyPath folder" -ForegroundColor Yellow
}

Write-Host "`nCreating folder structures..." -ForegroundColor Cyan

Write-Host "Building Full Package..." -ForegroundColor White

New-Item -ItemType Directory -Path "$fullPackagePath\BepInEx\plugins" -Force | Out-Null
Copy-Item $dllPath "$fullPackagePath\BepInEx\plugins\SeaCull.dll" -Force
Copy-Item $configPath "$fullPackagePath\SeaCull.ini" -Force
Write-Host "Copied SeaCull.dll and SeaCull.ini to Full package" -ForegroundColor Green

$bepInExItems = Get-ChildItem $bepInExBinPath -Recurse
foreach ($item in $bepInExItems) {
    $relativePath = $item.FullName.Substring((Get-Item $bepInExBinPath).FullName.Length + 1)
    $destinationPath = Join-Path $fullPackagePath $relativePath

    if ($item.PSIsContainer) {
        New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
    }
    else {
        $destinationDir = Split-Path $destinationPath -Parent
        New-Item -ItemType Directory -Path $destinationDir -Force -ErrorAction SilentlyContinue | Out-Null
        Copy-Item $item.FullName $destinationPath -Force
    }
}
Write-Host "Copied BepInExBin contents to Full package" -ForegroundColor Green

Write-Host "`nBuilding Plugin Only Package..." -ForegroundColor White

New-Item -ItemType Directory -Path "$pluginOnlyPath\BepInEx\plugins" -Force | Out-Null
Copy-Item $dllPath "$pluginOnlyPath\BepInEx\plugins\SeaCull.dll" -Force
Copy-Item $configPath "$pluginOnlyPath\SeaCull.ini" -Force
Write-Host "Copied SeaCull.dll and SeaCull.ini to Plugin Only package" -ForegroundColor Green

Write-Host "`nCreating archives..." -ForegroundColor Cyan

$archivesFolder = "Archives"
if (Test-Path $archivesFolder) {
    Remove-Item $archivesFolder -Recurse -Force
    Write-Host "Cleaned up existing Archives folder" -ForegroundColor Yellow
}
New-Item -ItemType Directory -Path $archivesFolder -Force | Out-Null

Write-Host "Creating ZIP archives..." -ForegroundColor White
New-ZipArchive -SourcePath $fullPackagePath -DestinationPath "$archivesFolder\SeaCull - Full.zip"
New-ZipArchive -SourcePath $pluginOnlyPath -DestinationPath "$archivesFolder\SeaCull - Plugin Only.zip"

if ($use7Zip) {
    Write-Host "Creating 7-Zip archives..." -ForegroundColor White
    New-SevenZipArchive -SourcePath $fullPackagePath -DestinationPath "$archivesFolder\SeaCull - Full.7z" -SevenZipExe $SevenZipPath
    New-SevenZipArchive -SourcePath $pluginOnlyPath -DestinationPath "$archivesFolder\SeaCull - Plugin Only.7z" -SevenZipExe $SevenZipPath
}
else {
    Write-Host "Skipping 7-Zip archives (7-Zip not available)" -ForegroundColor Yellow
}

Write-Host "`nCleaning up temporary folders..." -ForegroundColor White
Remove-Item $fullPackagePath -Recurse -Force
Remove-Item $pluginOnlyPath -Recurse -Force
Write-Host "Cleaned up package folders" -ForegroundColor Green

Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan

Write-Host "`nCreated archives in '$archivesFolder' folder:" -ForegroundColor White
Get-ChildItem $archivesFolder | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }

$archiveCount = (Get-ChildItem $archivesFolder).Count
Write-Host "`nTotal archives created: $archiveCount" -ForegroundColor Cyan
