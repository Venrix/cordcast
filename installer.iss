#define AppName "cordcast"
#define AppPublisher "Venrix"
#define AppExeName "cord_cast.exe"
#define AppSourceDir "app\build\windows\x64\runner\Release"
#define Vst3SourceDir "vst\build\CordCastSend_artefacts\Release\VST3"
#define Vst3Bundle Vst3SourceDir + "\CordCastSend.vst3"
#define Vst3Available DirExists(Vst3Bundle)

[Setup]
AppId={{1DE2BFB6-DF09-4563-8B1B-1E997A479DD8}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Venrix/CordCast
AppSupportURL=https://github.com/Venrix/CordCast/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=app\build\windows\installer
OutputBaseFilename={#AppName}-{#AppVersion}-windows-setup
SetupIconFile=app\windows\runner\resources\app_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
; WinGet silent install flags
AllowCancelDuringInstall=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
#if Vst3Available
Name: "vst3"; Description: "Install VST3 plugin for DAW audio routing (CordCast Send)"; GroupDescription: "Additional components:"; Flags: unchecked
#endif

[Files]
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
#if Vst3Available
Source: "{#Vst3Bundle}\*"; DestDir: "{code:GetVst3Dir}\CordCastSend.vst3"; Flags: ignoreversion recursesubdirs; Tasks: vst3
#endif

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
; VST3 bundle cleanup — try both admin and user paths; missing paths are silently ignored
Type: filesandordirs; Name: "{commonpf64}\Common Files\VST3\CordCastSend.vst3"
Type: filesandordirs; Name: "{localappdata}\Programs\Common\VST3\CordCastSend.vst3"

[Code]
function GetVst3Dir(Param: String): String;
begin
  if IsAdminInstallMode then
    Result := ExpandConstant('{commonpf64}\Common Files\VST3')
  else
    Result := ExpandConstant('{localappdata}\Programs\Common\VST3');
end;
