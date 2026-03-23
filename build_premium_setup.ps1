# build_premium_setup.ps1
# ECHOFORGE PREMIUM WPF INSTALLER BUILDER

$ErrorActionPreference = "Stop"

$RootFolder = "d:\Gemini\Projects\YoutubeOtomation"
$PublishDir = "$RootFolder\publish\EchoForge"
$InstallerProjDir = "$RootFolder\src\EchoForge.Installer"
$SetupOutput = "$RootFolder\installer_output\EchoForge_PremiumSetup.exe"
$PayloadZip = "$InstallerProjDir\payload.zip"

Write-Host "1. EchoForge WPF Derleniyor..." -ForegroundColor Cyan
dotnet publish "$RootFolder\src\EchoForge.WPF\EchoForge.WPF.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $PublishDir

if (Test-Path $PayloadZip) {
    Remove-Item $PayloadZip -Force
}

Write-Host "2. Yayım Dosyaları (Payload) Zip'leniyor..." -ForegroundColor Cyan
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PayloadZip -Force

Write-Host "3. Özel Premium Setup Derleniyor..." -ForegroundColor Cyan
# ZIP dosyasını C# projesine "Embedded Resource" olarak eklememiz lazım.
$CsprojFile = "$InstallerProjDir\EchoForge.Installer.csproj"
$CsprojContent = Get-Content $CsprojFile -Raw
if ($CsprojContent -notmatch "EmbeddedResource Include=`"payload.zip`"") {
    $NewCsproj = $CsprojContent.Replace("</Project>", "  <ItemGroup>`n    <EmbeddedResource Include=`"payload.zip`" />`n  </ItemGroup>`n</Project>")
    Set-Content -Path $CsprojFile -Value $NewCsproj
}

dotnet build "$InstallerProjDir\EchoForge.Installer.csproj" -c Release
dotnet publish "$InstallerProjDir\EchoForge.Installer.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$RootFolder\installer_output\PremiumSetup"

if (Test-Path "$RootFolder\installer_output\PremiumSetup\EchoForge_Setup.exe") {
    Copy-Item "$RootFolder\installer_output\PremiumSetup\EchoForge_Setup.exe" -Destination $SetupOutput -Force
    Write-Host "✅ İşlem Başarılı! Custom Setup EXEsini şurada bulabilirsiniz: $SetupOutput" -ForegroundColor Green
} else {
    Write-Host "❌ Kurulum Dosyası (Setup) oluşturulamadı." -ForegroundColor Red
}
