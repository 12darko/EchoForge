; ╔══════════════════════════════════════════════════════════════╗
; ║           EchoForge - AI Music Video Studio                 ║
; ║           Professional Installer Script                     ║
; ╚══════════════════════════════════════════════════════════════╝

#define MyAppName "EchoForge"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "EchoForge Studio"
#define MyAppURL "https://github.com/12darko/EchoForge"
#define MyAppExeName "EchoForge.WPF.exe"
#define MyAppDesc "AI-Powered Music Video Creator"

[Setup]
AppId={{A5213E9B-BE8E-4E27-9CD5-400B1E10B6E2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\installer_output
OutputBaseFilename=EchoForge_Setup_v{#MyAppVersion}
; Icon
SetupIconFile=..\src\EchoForge.WPF\Assets\echoforge_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; UI Settings
WizardStyle=modern
WizardSizePercent=120,120
; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Misc
DisableProgramGroupPage=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=
; Uninstall
UninstallDisplayName={#MyAppName} {#MyAppVersion}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application executable
Source: "..\publish\EchoForge\EchoForge.WPF.exe"; DestDir: "{app}"; Flags: ignoreversion
; PDB files (optional for debugging)
Source: "..\publish\EchoForge\*.pdb"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDesc}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDesc}"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Code]
// Custom welcome page message
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'EchoForge v{#MyAppVersion} - AI Music Video Studio' + #13#10 + #13#10 +
    'Bu kurulum sihirbazı EchoForge uygulamasını bilgisayarınıza kuracaktır.' + #13#10 + #13#10 +
    'Özellikler:' + #13#10 +
    '• AI destekli müzik videosu oluşturma' + #13#10 +
    '• YouTube kanal yönetimi ve otomatik yükleme' + #13#10 +
    '• Profesyonel video düzenleme araçları' + #13#10 +
    '• Çoklu dil desteği (TR/EN)' + #13#10 + #13#10 +
    'Devam etmek için İleri butonuna tıklayın.';
end;
