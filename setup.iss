[Setup]
AppName=MacroGen
AppVersion=1.1.8
DefaultDirName={autopf}\MacroGen
DefaultGroupName=MacroGen
OutputDir=.\Releases
OutputBaseFilename=MacroGen_Official_Setup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=.\logo.ico

[Files]
Source: ".\Releases\Setup\Source\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MacroGen"; Filename: "{app}\MacroCreator.exe"
Name: "{autodesktop}\MacroGen"; Filename: "{app}\MacroCreator.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\MacroCreator.exe"; Description: "{cm:LaunchProgram,MacroGen}"; Flags: nowait postinstall
