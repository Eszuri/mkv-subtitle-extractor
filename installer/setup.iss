; Inno Setup Script for MKS Subtitle Studio
; Full Installation Package with File Association and Context Menu

#define MyAppName "MKS Subtitle Studio"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "MKS Studio"
#define MyAppURL "https://github.com/mks-studio"
#define MyAppExeName "MksStudio.UI.exe"
#define MyAppAssocName "Matroska Subtitle File"
#define MyAppAssocExt ".mks"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{8B234F90-7812-421F-9E34-A78C1234E567}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
ChangesAssociations=yes
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\dist
OutputBaseFilename=MksStudio_Setup_v1.0
SetupIconFile=..\src\MksStudio.UI\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "assoc_mks"; Description: "Associate .mks files with MKS Subtitle Studio"; GroupDescription: "File Associations:"
Name: "shell_mkv"; Description: "Add 'Export Subtitle' to Windows Explorer context menu for .mkv files"; GroupDescription: "Windows Integration:"
Name: "shell_subs"; Description: "Add 'Translate Subtitle' to Windows Explorer context menu for subtitle files"; GroupDescription: "Windows Integration:"

[Files]
Source: "..\dist\MksStudio-v1.0-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; File association for .mks
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assoc_mks
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey; Tasks: assoc_mks
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: assoc_mks
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: assoc_mks

; Context menu for .mkv files (Right Click MKV -> Export Subtitle)
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\MksStudioExport"; ValueType: string; ValueName: ""; ValueData: "Export Subtitle"; Flags: uninsdeletekey; Tasks: shell_mkv
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\MksStudioExport"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_mkv
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mkv\shell\MksStudioExport\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-export ""%1"""; Tasks: shell_mkv

; Context menu for subtitle files (Right Click Subtitle -> Translate Subtitle)
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.srt\shell\MksStudioTranslate"; ValueType: string; ValueName: ""; ValueData: "Translate Subtitle"; Flags: uninsdeletekey; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.srt\shell\MksStudioTranslate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.srt\shell\MksStudioTranslate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-translate ""%1"""; Tasks: shell_subs

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ass\shell\MksStudioTranslate"; ValueType: string; ValueName: ""; ValueData: "Translate Subtitle"; Flags: uninsdeletekey; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ass\shell\MksStudioTranslate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ass\shell\MksStudioTranslate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-translate ""%1"""; Tasks: shell_subs

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.vtt\shell\MksStudioTranslate"; ValueType: string; ValueName: ""; ValueData: "Translate Subtitle"; Flags: uninsdeletekey; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.vtt\shell\MksStudioTranslate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.vtt\shell\MksStudioTranslate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-translate ""%1"""; Tasks: shell_subs

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ssa\shell\MksStudioTranslate"; ValueType: string; ValueName: ""; ValueData: "Translate Subtitle"; Flags: uninsdeletekey; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ssa\shell\MksStudioTranslate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.ssa\shell\MksStudioTranslate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-translate ""%1"""; Tasks: shell_subs

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.sub\shell\MksStudioTranslate"; ValueType: string; ValueName: ""; ValueData: "Translate Subtitle"; Flags: uninsdeletekey; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.sub\shell\MksStudioTranslate"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: shell_subs
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.sub\shell\MksStudioTranslate\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --quick-translate ""%1"""; Tasks: shell_subs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
