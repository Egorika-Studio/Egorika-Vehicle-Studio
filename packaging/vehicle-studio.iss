#ifndef AppVersion
  #error Pass /DAppVersion from package-release.ps1
#endif
#ifndef OnlinePackageHash
  #error Build with package-online-setup.ps1
#endif

[Setup]
AppId={{5CE9BB33-9DE7-480D-9DA3-147BF8114533}
AppName=Egorika Vehicle Studio
AppPublisher=Egorika Studio
AppPublisherURL=https://github.com/Egorika-Studio/Egorika-Vehicle-Studio
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Egorika Vehicle Studio
DefaultGroupName=Egorika Vehicle Studio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputBaseFilename=VehicleStudioInstallEngine
ArchiveExtraction=full
#define FileSource "{tmp}\vehicle-studio-package"
#define FileFlags "external ignoreversion"
SetupIconFile=..\Egorika.ico
UninstallDisplayIcon={app}\EgorikaVehicleStudio.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.NeedFramework=.NET Framework 4.8 or later is required. Install it from Microsoft and run Setup again.
russian.NeedFramework=Требуется .NET Framework 4.8 или новее. Установите его с сайта Microsoft и запустите установщик снова.
spanish.NeedFramework=Se requiere .NET Framework 4.8 o posterior. Instálalo desde Microsoft y ejecuta de nuevo el instalador.
french.NeedFramework=.NET Framework 4.8 ou ultérieur est requis. Installez-le depuis Microsoft et relancez l’installation.
brazilianportuguese.NeedFramework=É necessário o .NET Framework 4.8 ou posterior. Instale-o pelo site da Microsoft e execute o instalador novamente.

[Dirs]
Name: "{app}\runtime"; Attribs: hidden

[Files]
Source: "{#FileSource}\EgorikaVehicleStudio.exe"; DestDir: "{app}"; Flags: {#FileFlags}; ExternalSize: {#OnlineExeSize}
Source: "{#FileSource}\SharpDX.dll"; DestDir: "{app}\runtime"; Flags: {#FileFlags}; ExternalSize: {#OnlineSharpDXSize}
Source: "{#FileSource}\SharpDX.Mathematics.dll"; DestDir: "{app}\runtime"; Flags: {#FileFlags}; ExternalSize: {#OnlineMathSize}
Source: "{#FileSource}\thumbnails\*.png"; DestDir: "{app}\thumbnails"; Flags: {#FileFlags}; ExternalSize: {#OnlineThumbnailSize}
Source: "..\LICENSE"; DestDir: "{app}\runtime\licenses"; DestName: "Egorika-MIT.txt"; Flags: ignoreversion
Source: "..\SharpDX-LICENSE.txt"; DestDir: "{app}\runtime\licenses"; Flags: ignoreversion

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\Egorika Vehicle Studio"; Filename: "{app}\EgorikaVehicleStudio.exe"
Name: "{group}\{cm:UninstallProgram,Egorika Vehicle Studio}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Egorika Vehicle Studio"; Filename: "{app}\EgorikaVehicleStudio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EgorikaVehicleStudio.exe"; Description: "{cm:LaunchProgram,Egorika Vehicle Studio}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\runtime\CodeWalker.Core.dll"
Type: filesandordirs; Name: "{app}\runtime\.egorika-update-*"
Type: dirifempty; Name: "{app}\runtime"

[Code]
var
  DownloadPage: TDownloadWizardPage;

procedure InitializeWizard();
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing),
    SetupMessage(msgPreparingDesc), nil);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  DownloadPage.Clear;
  DownloadPage.Add('{#OnlinePackageUrl}', 'vehicle-studio.zip', '{#OnlinePackageHash}');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := GetExceptionMessage;
    end;
  finally
    DownloadPage.Hide;
  end;
  if Result = '' then
  begin
    try
      ExtractArchive(ExpandConstant('{tmp}\vehicle-studio.zip'),
        ExpandConstant('{tmp}\vehicle-studio-package'), '', True, nil);
    except
      Result := GetExceptionMessage;
    end;
  end;
end;

function SetFileAttributes(FileName: string; Attributes: Cardinal): Boolean;
  external 'SetFileAttributesW@kernel32.dll stdcall';

function InitializeSetup(): Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then
    MsgBox(CustomMessage('NeedFramework'), mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
    SetFileAttributes(ChangeFileExt(ExpandConstant('{uninstallexe}'), '.dat'), 2);
end;
