; Emberport installer script for Inno Setup 6
; Build with: iscc installer\Emberport.iss
;
; Expects:
;   publish\                     self contained build (dotnet publish, no single file)
;   bin\apache\<build>\          portable Apache
;   bin\php\<build>\             portable PHP
;   bin\mysql\<build>\           portable MySQL
;   bin\redis\<build>\           portable Redis
;   tools\phpmyadmin\            phpMyAdmin

#define AppName        "Emberport"
#define AppVersion     "1.0.0"
#define AppPublisher   "Hojjat Jahanpour"
#define AppURL         "https://github.com/hojjatjh/Emberport"
#define AppExeName     "Emberport.exe"

; Bundled builds. Change these when you refresh a server, nothing else moves.
#define ApacheBuild    "httpd-2.4.68-260617-Win64-VS18"
#define PhpBuild       "php-8.5.8-Win32-vs17-x64"
#define PhpLegacyBuild "php-8.2.32-Win32-vs16-x64"
#define MySqlBuild     "mysql-9.7.1-winx64"
#define RedisBuild     "redis-x64-5.0.14.1"

[Setup]
; A stable AppId is what lets a new version upgrade the old one in place.
AppId={{B7F4C0E2-9A31-4D77-8E56-3C1A0F2D6B84}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}

; Not Program Files. Emberport keeps its whole workspace next to the executable,
; and a path without spaces keeps Apache and MySQL configuration simple.
DefaultDirName={sd}\Emberport
DefaultGroupName={#AppName}
AllowNoIcons=yes
DisableProgramGroupPage=yes
DisableDirPage=no
DirExistsWarning=no

; Administrator rights are needed once, to create the folder outside the profile
; and to grant every user write access to it.
PrivilegesRequired=admin

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

LicenseFile=..\LICENSE
SetupIconFile=..\src\Emberport.App\Assets\logo.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}

OutputDir=..\dist
OutputBaseFilename=Emberport-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "full";    Description: "Full stack, everything included"
Name: "compact"; Description: "Apache, PHP and phpMyAdmin only"
Name: "custom";  Description: "Choose what to install"; Flags: iscustom

[Components]
Name: "app";         Description: "Emberport";                     Types: full compact custom; Flags: fixed
Name: "apache";      Description: "Apache {#ApacheBuild}";         Types: full compact custom
Name: "php";         Description: "PHP (current build)";           Types: full compact custom
Name: "phplegacy";   Description: "PHP (older build, optional)";   Types: full
Name: "mysql";       Description: "MySQL {#MySqlBuild}";           Types: full custom
Name: "redis";       Description: "Redis {#RedisBuild}";           Types: full custom
Name: "phpmyadmin";  Description: "phpMyAdmin";                    Types: full compact custom

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"
; Starting with Windows is deliberately not offered here. The installer runs
; elevated, so anything it writes to HKCU lands in the administrator hive
; instead of the real user's. Emberport already owns that switch in Settings.

[Dirs]
; The workspace is created at install time and made writable for normal users,
; so Emberport never needs to run elevated afterwards.
Name: "{app}";              Permissions: users-modify
Name: "{app}\bin";          Permissions: users-modify
Name: "{app}\bin\apache";   Permissions: users-modify
Name: "{app}\bin\php";      Permissions: users-modify
Name: "{app}\bin\mysql";    Permissions: users-modify
Name: "{app}\bin\redis";    Permissions: users-modify
Name: "{app}\tools";        Permissions: users-modify
Name: "{app}\config";       Permissions: users-modify
Name: "{app}\data";         Permissions: users-modify
Name: "{app}\backups";      Permissions: users-modify
Name: "{app}\www";          Permissions: users-modify

[Files]
; --- the application -------------------------------------------------------
; The whole self contained publish folder, runtime included. No single file
; bundle: WPF has to unpack its native libraries at runtime and that is exactly
; what fails on locked down machines. An installer makes the bundle pointless.
Source: "..\publish\*";  DestDir: "{app}"; Components: app; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE";    DestDir: "{app}"; DestName: "LICENSE.txt"; Components: app; Flags: ignoreversion
Source: "..\README.md";  DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Components: app; Flags: ignoreversion

; --- bundled servers -------------------------------------------------------
; Everything generated at runtime is excluded on purpose. Emberport rewrites
; those files on first launch, and shipping them would carry my machine's paths
; and my phpMyAdmin secret onto someone else's computer.

Source: "..\bin\apache\{#ApacheBuild}\*"; DestDir: "{app}\bin\apache\{#ApacheBuild}"; \
    Components: apache; Excludes: "logs\*,conf\emberport.conf,*.log,*.pid"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\bin\php\{#PhpBuild}\*"; DestDir: "{app}\bin\php\{#PhpBuild}"; \
    Components: php; Excludes: "*.log"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\bin\php\{#PhpLegacyBuild}\*"; DestDir: "{app}\bin\php\{#PhpLegacyBuild}"; \
    Components: phplegacy; Excludes: "*.log"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\bin\mysql\{#MySqlBuild}\*"; DestDir: "{app}\bin\mysql\{#MySqlBuild}"; \
    Components: mysql; Excludes: "data\*,*.log,*.pid,*.err"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\bin\redis\{#RedisBuild}\*"; DestDir: "{app}\bin\redis\{#RedisBuild}"; \
    Components: redis; Excludes: "*.log,dump.rdb"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

Source: "..\tools\phpmyadmin\*"; DestDir: "{app}\tools\phpmyadmin"; \
    Components: phpmyadmin; Excludes: "config.inc.php,tmp\*"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

; --- notes in the folders that stayed empty --------------------------------
Source: "placeholder\WHERE-TO-PUT-BUILDS.txt"; DestDir: "{app}\bin\apache"; Flags: ignoreversion
Source: "placeholder\WHERE-TO-PUT-BUILDS.txt"; DestDir: "{app}\bin\php";    Flags: ignoreversion
Source: "placeholder\WHERE-TO-PUT-BUILDS.txt"; DestDir: "{app}\bin\mysql";  Flags: ignoreversion
Source: "placeholder\WHERE-TO-PUT-BUILDS.txt"; DestDir: "{app}\bin\redis";  Flags: ignoreversion
Source: "placeholder\WHERE-TO-PUT-BUILDS.txt"; DestDir: "{app}\tools";      Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
Filename: "{#AppURL}";           Description: "Open the project page"; Flags: shellexec nowait postinstall skipifsilent unchecked

[UninstallDelete]
; Only files Emberport generates itself. Databases, backups and the document
; root are deliberately left behind so an uninstall can never destroy work.
Type: filesandordirs; Name: "{app}\config"
Type: files;          Name: "{app}\bin\apache\{#ApacheBuild}\conf\emberport.conf"
Type: files;          Name: "{app}\tools\phpmyadmin\config.inc.php"
Type: files;          Name: "{app}\bin\apache\WHERE-TO-PUT-BUILDS.txt"
Type: files;          Name: "{app}\bin\php\WHERE-TO-PUT-BUILDS.txt"
Type: files;          Name: "{app}\bin\mysql\WHERE-TO-PUT-BUILDS.txt"
Type: files;          Name: "{app}\bin\redis\WHERE-TO-PUT-BUILDS.txt"
Type: files;          Name: "{app}\tools\WHERE-TO-PUT-BUILDS.txt"

[Code]
var
  ErrorCode: Integer;

function VcRedistInstalled: Boolean;
var
  Installed: Cardinal;
begin
  // PHP and MySQL both link against the Visual C++ 2015-2022 runtime.
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not VcRedistInstalled) then
  begin
    if MsgBox('The Visual C++ 2015-2022 Redistributable (x64) was not found.' + #13#10 +
              'PHP and MySQL will not start without it.' + #13#10#13#10 +
              'Open the Microsoft download page now?', mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://aka.ms/vc14/vc_redist.x64.exe', '', '', SW_SHOW, ewNoWait, ErrorCode);
  end;
end;
