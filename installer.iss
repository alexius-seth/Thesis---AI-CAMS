[Setup]
AppName=AI-CAMS
AppVersion=1.0.0
DefaultDirName={autopf}\AI-CAMS
DefaultGroupName=AI-CAMS
OutputDir=.\installer-output
OutputBaseFilename=AICAMS_Setup
Compression=lzma2/fast
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: ".\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\AI-CAMS"; Filename: "{app}\AICAMS.exe"
Name: "{autodesktop}\AI-CAMS"; Filename: "{app}\AICAMS.exe"

[Run]
Filename: "{app}\AICAMS.exe"; Description: "Launch AI-CAMS"; Flags: postinstall nowait skipifsilent
