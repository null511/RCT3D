#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PublishDir
  #error PublishDir must be supplied
#endif
#ifndef ArtifactDir
  #error ArtifactDir must be supplied
#endif

[Setup]
AppId={{836994D6-A646-4B76-BE89-E314809AA15E}
AppName=RCT3DViewer
AppVersion={#AppVersion}
AppPublisher=null511
AppPublisherURL=https://github.com/null511/RCT3D
DefaultDirName={localappdata}\Programs\RCT3DViewer
DefaultGroupName=RCT3DViewer
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#ArtifactDir}
OutputBaseFilename=RCT3DViewer-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\RCT3D-Viewer.exe
CloseApplications=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\RCT3DViewer"; Filename: "{app}\RCT3D-Viewer.exe"; WorkingDir: "{app}"