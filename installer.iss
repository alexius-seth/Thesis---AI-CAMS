; ==============================================================================
; AI-CAMS - Inno Setup 6 Installation Script
; AI-Assisted Centralized Assessment Monitoring System
; University of Luzon - College of Computer Studies
; ==============================================================================

#define MyAppName "AI-CAMS"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "University of Luzon - College of Computer Studies"
#define MyAppExeName "AICAMS.exe"

[Setup]
; Application Metadata
AppId={{E8B7A58E-2F3B-4927-BA8F-3A188C0E763A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}

; Installation Folders & Start Menu Group
DefaultDirName={autopf}\AI-CAMS
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=.\AICAMS_Installation
OutputBaseFilename=AICAMS_Setup
SetupIconFile=FINAL LOGO.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Platform & Architecture (64-bit Windows)
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

; Compression & Packaging
Compression=lzma2/ultra64
SolidCompression=yes

; Modern Wizard Styling
WizardStyle=modern
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; 1. Core Application Binaries & Toolchains
; Excludes appsettings.json so updates do not overwrite configured workstation settings
Source: "publish-selfcontained\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.json"

; 2. Configuration File (Safety: Only install if not present; grant Users write permission)
Source: "publish-selfcontained\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist; Permissions: users-modify
Source: "publish-selfcontained\appsettings.json"; DestDir: "{app}"; DestName: "appsettings.default.json"; Flags: ignoreversion

; 3. Prerequisites: Microsoft Visual C++ 2015-2022 Redistributable (x64)
Source: "AICAMS_Installation\Prerequisites\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist
Source: "Prerequisites\VC_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall skipifsourcedoesntexist

; 4. Documentation & Database Staging Assets
Source: "AICAMS_Installation\Documentation\*"; DestDir: "{app}\Documentation"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "AICAMS_Installation\Database_Backups\*"; DestDir: "{app}\Database"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; 5. Brand Identity Assets
Source: "FINAL LOGO.ico"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}"; Permissions: users-modify
Name: "{app}\Tools"; Permissions: users-modify

[Icons]
; Start Menu Program Shortcuts
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\FINAL LOGO.ico"
Name: "{group}\Installation Guide & Docs"; Filename: "{app}\Documentation\Installation_Guide.txt"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

; Desktop Shortcut
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\FINAL LOGO.ico"; Tasks: desktopicon

[Run]
; Silently install Visual C++ 2015-2022 Redistributable if needed
Filename: "{tmp}\VC_redist.x64.exe"; Parameters: "/quiet /norestart"; Flags: runhidden; Check: ShouldInstallVCRedist

; Post-installation application launch checkbox
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// Helper function to inspect Windows Registry for Visual C++ 2015-2022 x64 Runtime
function VCRedistNeedsInstall: Boolean;
var
  InstalledVal: Cardinal;
begin
  Result := True;

  // Query the native 64-bit registry view
  if IsWin64 then
  begin
    if RegQueryDWordValue(HKEY_LOCAL_MACHINE_64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'Installed', InstalledVal) and (InstalledVal = 1) then
    begin
      Result := False;
      Exit;
    end;
  end;

  // Fallback check on standard 32-bit registry view
  if RegQueryDWordValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'Installed', InstalledVal) and (InstalledVal = 1) then
    Result := False;
end;

function ShouldInstallVCRedist: Boolean;
begin
  Result := FileExists(ExpandConstant('{tmp}\VC_redist.x64.exe')) and VCRedistNeedsInstall();
end;

