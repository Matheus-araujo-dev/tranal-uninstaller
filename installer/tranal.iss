; ---------------------------------------------------------------------------
; Tranal Uninstaller Pro - instalador (setup)
;
; Gerado por tools\publish-tranal.ps1 (-Variant setup). O script recebe os
; caminhos por defines na linha de comando:
;
;   /DAppPayloadDir=...   pasta com o aplicativo publicado (versao leve)
;   /DRuntimeFile=...     redistribuivel do .NET Desktop Runtime
;   /DRuntimeFileName=... nome do redistribuivel
;   /DOutputDir=...       onde gravar o Setup.exe
;   /DAppVersion=...      versao publicada
;   /DLicenseFile=...     arquivo de licenca
;   /DRepoRoot=...        raiz do repositorio
;
; Decisoes:
;  - O wizard e o do Inno Setup, com as traducoes oficiais: pt-BR e ingles.
;  - O usuario escolhe a pasta de instalacao (a premissa do dono).
;  - O app instalado e a versao leve (1,3 MB). O runtime do .NET (que o
;    WPF exige) viaja dentro do setup e so e instalado quando o computador
;    nao o tem: assim o setup nao depende de nada externo e o programa
;    instalado ocupa o minimo de espaco.
;  - O runtime e instalado em PrepareToInstall, antes de qualquer arquivo do
;    programa: se a instalacao do runtime falhar, a instalacao e cancelada e
;    o computador fica como estava. E o codigo de saida e conferido aqui
;    (0 = sucesso, 3010 = sucesso mas precisa reiniciar); na secao [Run] nao
;    existe como ignorar o codigo de saida, e confiar no numero do instalador
;    do .NET seria pior do que conferir o resultado.
;  - O desinstalador remove a entrada do menu de contexto do Windows: deixar
;    ela apontando para um executavel apagado seria uma referencia orfa. A
;    remocao e feita pelo proprio codigo do programa, e nao aqui: a regra do
;    registro fica em um lugar so.
;
; IMPORTANTE 1: este arquivo e gravado em UTF-8 COM BOM. O Inno Setup 6 le
; script sem BOM como ANSI (cp1252) e os acentos sairiam errados no menu
; Iniciar.
;
; IMPORTANTE 2: a compressao e zip, e nao lzma2. Medido nesta maquina: com
; lzma2 o McAfee bloqueava e apagava o setup como "programa potencialmente
; indesejavel" (falso positivo conhecido em instaladores com carga LZMA).
; Com zip o bloqueio some e o tamanho praticamente nao muda, porque o
; redistribuivel do runtime (60 MB) ja vem compactado.
; ---------------------------------------------------------------------------

#ifndef AppPayloadDir
  #error "AppPayloadDir nao definido"
#endif
#ifndef RuntimeFile
  #error "RuntimeFile nao definido"
#endif
#ifndef RuntimeFileName
  #error "RuntimeFileName nao definido"
#endif
#ifndef OutputDir
  #error "OutputDir nao definido"
#endif
#ifndef AppVersion
  #error "AppVersion nao definido"
#endif
#ifndef LicenseFile
  #error "LicenseFile nao definido"
#endif

#define AppName "Tranal Uninstaller Pro"
#define AppPublisher "Tranal"
#define AppExeName "TranalUninstaller.exe"
#define AppGroup "Tranal Group"
#define AppHomeUrl "https://github.com/Matheus-araujo-dev/tranal-uninstaller"

[Setup]
AppId={{4F0C1D6E-2A83-4B57-9E14-7C8D5B2A9F30}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppHomeUrl}
AppSupportURL={#AppHomeUrl}/issues
AppUpdatesURL={#AppHomeUrl}/releases/latest
DefaultDirName={autopf}\{#AppGroup}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no
OutputBaseFilename=TranalUninstaller-Setup
OutputDir={#OutputDir}
Compression=zip
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=110
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
LicenseFile={#LicenseFile}
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
; O icone do setup e o mesmo do programa: o .ico e gerado por
; tools\gerar-icone.ps1 e entra no executavel via ApplicationIcon.
SetupIconFile={#RepoRoot}\src\Tranal.Uninstaller\Assets\Tranal.ico
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

; Textos proprios: o wizard e bilingue, entao tudo que o Inno nao traduz
; sozinho precisa de uma versao por idioma.
[CustomMessages]
brazilianportuguese.Desinstalar=Desinstalar
english.Desinstalar=Uninstall
brazilianportuguese.PaginaInicial=Página inicial do projeto
english.PaginaInicial=Project home page
brazilianportuguese.RuntimeFalhou=Não foi possível instalar o .NET Desktop Runtime, que o Tranal Uninstaller Pro precisa para funcionar.\n\nA instalação foi cancelada e nada foi alterado no computador. Baixe o .NET Desktop Runtime 10 em https://dotnet.microsoft.com/download/dotnet/10, instale-o e execute o instalador do Tranal novamente.
english.RuntimeFalhou=The .NET Desktop Runtime, which Tranal Uninstaller Pro needs, could not be installed.\n\nThe installation was cancelled and nothing on your computer was changed. Download the .NET Desktop Runtime 10 from https://dotnet.microsoft.com/download/dotnet/10, install it, and run the Tranal installer again.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; O programa (versao leve). Sem "ignoreversion": em uma atualizacao os
; arquivos novos precisam substituir os antigos, e a comparacao de data faz
; isso (a publicacao sempre gera arquivos mais novos).
Source: "{#AppPayloadDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs; Excludes: "*.pdb"

; O redistribuivel do runtime: fica embutido no setup, mas nao e copiado
; durante a instalacao. Ele e extraido para a pasta temporaria e instalado
; pelo codigo, so quando o computador nao tem o .NET Desktop Runtime.
Source: "{#RuntimeFile}"; DestDir: "{tmp}"; Flags: dontcopy

[Icons]
; O icone dos atalhos sai do proprio executavel, que ja carrega o icone do
; programa — assim nao ha um arquivo .ico extra para instalar.
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{cm:Desinstalar}"; Filename: "{uninstallexe}"
Name: "{group}\{cm:PaginaInicial}"; Filename: "{#AppHomeUrl}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Executa o programa no final (a caixa aparece na pagina de conclusao).
; A checagem e feita de novo, sobre o estado real do disco: se o runtime
; nao estiver presente, o programa nao abre.
;
; "runascurrentuser" e obrigatorio: a bandeira "postinstall" faz o
; instalador rodar o programa como o usuario que NAO elevated, e o Tranal
; exige administrador — sem essa bandeira o Windows barra o lancamento com
; "CreateProcess falhou; codigo 740" e o programa nunca abre.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
    Flags: nowait postinstall skipifsilent runascurrentuser; Check: RuntimeDisponivel

[UninstallRun]
; Remove a entrada do menu de contexto do Windows: o executavel sera
; apagado, e deixar o registro apontando para ele seria uma referencia orfa.
Filename: "{app}\{#AppExeName}"; Parameters: "--unregister-context-menu"; \
    Flags: runhidden waituntilterminated; RunOnceId: "RemoverMenuContexto"

[Code]
var
  RuntimePresente: Boolean;

{ Deteccao do .NET Desktop Runtime }

function PastaDoRuntime(const Raiz: String): Boolean;
var
  Info: TFindRec;
begin
  Result := False;
  if not FindFirst(Raiz + '\Microsoft.WindowsDesktop.App\10.*', Info) then
    Exit;
  try
    Result := True;
  finally
    FindClose(Info);
  end;
end;

function ChecarRuntime: Boolean;
begin
  { A pasta e a fonte que vale: e nela que o host procura o framework. O
    registro do .NET nem sempre reflete o que existe de fato - nesta maquina
    o runtime 10 estava instalado sem a chave correspondente. }
  if PastaDoRuntime(ExpandConstant('{commonpf}\dotnet\shared')) then
  begin
    Result := True;
    Exit;
  end;

  { Instalacao por usuario. }
  Result := PastaDoRuntime(ExpandConstant('{localappdata}\Microsoft\dotnet\shared'));
end;

function RuntimeAusente: Boolean;
begin
  { Estado no inicio da instalacao: o runtime ainda nao foi tentado. }
  Result := not RuntimePresente;
end;

function RuntimeDisponivel: Boolean;
begin
  { Estado real, conferido sobre o disco. }
  Result := ChecarRuntime;
end;

{ Execucao }

function InitializeSetup(): Boolean;
begin
  { Roda antes do wizard: e aqui que se descobre se o runtime ja existe. }
  RuntimePresente := ChecarRuntime;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Codigo: Integer;
  Caminho: String;
begin
  Result := '';
  NeedsRestart := False;

  if RuntimePresente then
    Exit;

  { O redistribuivel do runtime e extraido do setup para a pasta
    temporaria e instalado aqui, antes de qualquer arquivo do programa. }
  ExtractTemporaryFile('{#RuntimeFileName}');
  Caminho := ExpandConstant('{tmp}\' + '{#RuntimeFileName}');

  if not Exec(Caminho, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, Codigo) then
  begin
    Result := '{cm:RuntimeFalhou}';
    Exit;
  end;

  { 0 = sucesso; 3010 = sucesso, mas o Windows precisa reiniciar;
    1641 = o proprio instalador ja reiniciou o Windows. }
  if (Codigo <> 0) and (Codigo <> 3010) and (Codigo <> 1641) then
  begin
    Result := '{cm:RuntimeFalhou}';
    Exit;
  end;

  if (Codigo = 3010) or (Codigo = 1641) then
    NeedsRestart := True;

  { Confere o resultado de verdade: a pasta do runtime existe agora? }
  RuntimePresente := ChecarRuntime;
  if not RuntimePresente then
    Result := '{cm:RuntimeFalhou}';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { O redistribuivel so foi usado como temporario. }
  if CurStep = ssDone then
    DeleteFile(ExpandConstant('{tmp}\' + '{#RuntimeFileName}'));
end;
