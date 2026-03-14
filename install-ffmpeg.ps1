$ErrorActionPreference = "Stop"

$ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$destDir = Join-Path $PSScriptRoot "src\EchoForge.API\Tools\ffmpeg"
$zipPath = Join-Path $PSScriptRoot "ffmpeg.zip"

Write-Host "Downloading FFmpeg from $ffmpegUrl..."
Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipPath

Write-Host "Extracting FFmpeg..."
Expand-Archive -Path $zipPath -DestinationPath $PSScriptRoot -Force

# Find the extracted folder (it usually has a version name)
$extractedFolder = Get-ChildItem -Path $PSScriptRoot -Directory -Filter "ffmpeg-*-essentials_build" | Select-Object -First 1

if ($extractedFolder) {
    # Create destination tools directory if not exists
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force
    }

    # Copy bin folder contents
    $binSource = Join-Path $extractedFolder.FullName "bin"
    Copy-Item -Path "$binSource\*" -Destination $destDir -Recurse -Force

    Write-Host "FFmpeg installed successfully to $destDir"
    
    # Cleanup
    Remove-Item -Path $zipPath -Force
    Remove-Item -Path $extractedFolder.FullName -Recurse -Force
}
else {
    Write-Error "Could not find extracted FFmpeg folder."
}

Write-Host "Done! You can now run start-app.ps1"
