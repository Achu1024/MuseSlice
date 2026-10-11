; MuseSlice 安装脚本（Inno Setup 6）
;
; 构建方式见 build-installer.ps1，或直接：
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MuseSlice.iss
;
; 前置：先 dotnet build MuseSlice.sln -c Release

#define AppName "MuseSlice"
#define AppVersion "0.1.0"
#define AppPublisher "Achu1024"
#define AppUrl "https://github.com/Achu1024/MuseSlice"
#define ExeName "MuseSlice.exe"
#define BuildDir "..\bin\MuseSlice\Release"
#define IconFile "..\icon\MuseSlice.ico"
#define IconName "MuseSlice.ico"

[Setup]
AppId={{8F3A2C61-5D74-4B19-9E20-7C4A1B6D3E55}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; 每用户安装：配置写 %APPDATA%、日记写「我的文档」、自启写 HKCU，全程不碰系统目录。
; 顺带让桌面快捷方式落在使用者自己的桌面，而非 C:\Users\Public\Desktop。
PrivilegesRequired=lowest
CloseApplications=yes
OutputDir=..\dist
OutputBaseFilename=MuseSlice-Setup-{#AppVersion}
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#IconName}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0

[Languages]
; Inno 自带语言包不含简体中文；要中文向导就把 ChineseSimplified.isl 放进
; installer\languages\ 再打开下面那行（译文件见 https://jrsoftware.org/files/istrans/）
; Name: "chinese"; MessagesFile: "compiler:Default.isl,languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; 桌面快捷方式：默认勾选 —— 装完即在桌面看到入口
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#BuildDir}\{#ExeName}";      DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\MuseSlice.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\{#ExeName}.config";  DestDir: "{app}"; Flags: ignoreversion
Source: "{#IconFile}";                 DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\sounds\*";        DestDir: "{app}\sounds"; Flags: ignoreversion recursesubdirs

[Icons]
; 图标一律指安装目录里的那份 —— {#IconFile} 带 ..\ 是给编译器读的源路径，
; 原样塞进快捷方式会变成解不开的相对路径。
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\{#IconName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; IconFilename: "{app}\{#IconName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载前先请出进程，否则 exe 被占用删不掉
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#ExeName}"; Flags: runhidden; RunOnceId: "KillApp"
