---
description: EchoForge Premium Installer Build Süreci
---

## Premium Installer Nedir?

EchoForge kendi özel WPF kurulum sihirbazına sahiptir (`src/EchoForge.Installer`).
Bu kurulum penceresi, uygulamayı zip olarak içine gömer, masaüstü kısayolu oluşturur,
registry'e uninstaller yazar ve kurulum tamamlanınca uygulamayı başlatır.

## Adımlar

1. **EchoForge.WPF versiyonunu güncelle**  
   `src/EchoForge.WPF/EchoForge.WPF.csproj` dosyasındaki `<Version>`, `<AssemblyVersion>`, `<FileVersion>` değerlerini yeni sürüme çek (ör: 2.2.2).

2. **Installer versiyonunu güncelle**  
   `src/EchoForge.Installer/EchoForge.Installer.csproj` dosyasındaki `<Version>`, `<AssemblyVersion>`, `<FileVersion>` değerlerini aynı sürüme çek.

// turbo
3. **Build scriptini çalıştır**  
   Proje kökünde şu komutu çalıştır:
   ```powershell
   powershell -ExecutionPolicy Bypass -File "build_premium_setup.ps1"
   ```
   Bu script şunları yapar:
   - EchoForge.WPF'yi publish eder (`publish\EchoForge\`)
   - Publish çıktısını `payload.zip` olarak sıkıştırır
   - payload.zip'i EchoForge.Installer içine gömer
   - EchoForge.Installer'ı tek EXE olarak derler
   - Çıktı: `installer_output\EchoForge_PremiumSetup.exe`

4. **Güncelleme zip dosyasını da oluştur (opsiyonel)**
   ```powershell
   Compress-Archive -Path "publish\EchoForge\*" -DestinationPath "publish\EchoForge-v2.2.2.zip" -Force
   ```

## Önemli Notlar

- **⚠️ Inno Setup KULLANILMIYOR** — `installer/setup.iss` dosyası eski dönemden kalma, kullanma!
- **Asıl kurulum:** `src/EchoForge.Installer` WPF projesi
- **UnInstaller:** Kurulum sırasında `EchoForge_Uninstall.exe` adıyla kurulum dizinine kopyalanır. `--uninstall` argümanı ile çağrılır.
- **Registry:** `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EchoForge` altına yazılır.
- **Versiyon:** Registry'deki DisplayVersion, Assembly'den otomatik okunur — hardcode değildir.
