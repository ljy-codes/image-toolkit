#ifndef MyAppVersion
  #define MyAppVersion "1.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef InstallerOutput
  #define InstallerOutput "..\artifacts\installer"
#endif
#ifndef SourceRoot
  #define SourceRoot ".."
#endif

#define MyAppName "苏影枢"
#define MyAppPublisher "ljy-codes"
#define MyAppExeName "ImageToolkit.App.exe"

[Setup]
AppId={{8A3B086E-61AF-4EAC-B3F8-E7CB35A0F11D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ImageToolkit
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir={#InstallerOutput}
OutputBaseFilename=ImageToolkitSetup
Compression=lzma2
SolidCompression=yes
; Inno Setup 6.7+ built-in dark style covers Setup and Uninstall controls.
WizardStyle=modern dark polar includetitlebar
WizardBackColor=#141D26
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
VersionInfoProductName={#MyAppName}
SetupIconFile={#SourceRoot}\src\ImageToolkit.App\Assets\ImageToolkit.ico
#ifdef SignToolCommand
SignTool={#SignToolCommand}
SignedUninstaller=yes
#endif

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceRoot}\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeleteUserData: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  UserDataDirectory: String;
begin
  if (CurUninstallStep = usUninstall) and not UninstallSilent then
  begin
    DeleteUserData :=
      MsgBox(
        '是否同时删除当前 Windows 用户的全部苏影枢数据？' + #13#10 + #13#10 +
        '删除范围包括：已下载的 AI 模型、配置、参数方案和日志。' + #13#10 +
        '此操作无法恢复。选择“否”将保留这些数据，便于重新安装后继续使用。',
        mbConfirmation,
        MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;

  if (CurUninstallStep = usPostUninstall) and DeleteUserData then
  begin
    UserDataDirectory := ExpandConstant('{localappdata}\ImageToolkit');
    if DirExists(UserDataDirectory) and
       not DelTree(UserDataDirectory, True, True, True) then
    begin
      MsgBox(
        '部分用户数据未能删除。请关闭仍在使用相关文件的程序后，手动删除：' + #13#10 +
        UserDataDirectory,
        mbError,
        MB_OK);
    end;
  end;
end;
