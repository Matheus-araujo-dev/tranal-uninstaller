#!/usr/bin/env node
// Matriz de capacidades por concorrente, extraida das importacoes PE medidas pelo REA.
// uso: node capacidades.js <dir-inventario> <saida.md>
const fs = require('fs');
const path = require('path');

const [, , dir, outPath] = process.argv;

// token de API -> capacidade do produto
const CAPS = {
  'RegDeleteKeyExA|RegDeleteKeyExW|RegDeleteKeyA|RegDeleteKeyW': 'Remove chaves de registro',
  'RegDeleteValueExA|RegDeleteValueExW|RegDeleteValueA|RegDeleteValueW': 'Remove valores de registro',
  'RegSetValueExA|RegSetValueExW|RegSetValueA|RegSetValueW': 'Escreve no registro',
  'RegEnumKeyExA|RegEnumKeyExW|RegEnumKeyA|RegEnumKeyW': 'Percorre o registro',
  'RegOpenKeyExA|RegOpenKeyExW|RegOpenKeyA|RegOpenKeyW': 'Abre chaves do registro',
  'CreateServiceA|CreateServiceW': 'Cria servicos do Windows',
  'OpenServiceA|OpenServiceW|OpenSCManagerA|OpenSCManagerW': 'Gerencia servicos do Windows',
  'DeleteService|ControlService|StartServiceA|StartServiceW': 'Controla servicos do Windows',
  'EnumServicesStatusA|EnumServicesStatusW|EnumServicesStatusExA|EnumServicesStatusExW': 'Lista servicos do Windows',
  'ITaskScheduler|ITaskService|ScheduleService|CoCreateInstance': 'Agenda tarefas',
  'SetupDiGetClassDevsA|SetupDiGetClassDevsW|SetupDiGetClassDevsExA|SetupDiGetClassDevsExW': 'Enumera dispositivos/Drivers',
  'SetupDiEnumDeviceInfo|SetupDiDestroyDeviceInfoList': 'Inspeciona dispositivos',
  'CoCreateInstance|CoInitializeEx|CoInitialize|OleInitialize': 'Usa automacao COM',
  'VariantInit|VariantClear|SafeArrayCreate': 'Usa automacao COM',
  'InternetOpenA|InternetOpenW|InternetOpenUrlA|InternetOpenUrlW|HttpOpenRequestA|HttpOpenRequestW': 'Acesso HTTP/FTP',
  'URLDownloadToFileA|URLDownloadToFileW': 'Baixa arquivos',
  'ShellExecuteA|ShellExecuteW|ShellExecuteExA|ShellExecuteExW': 'Executa programas',
  'CreateProcessA|CreateProcessW|CreateProcessAsUserW': 'Executa processos',
  'TerminateProcess': 'Encerra processos',
  'OpenProcess': 'Abre processos de terceiros',
  'CreateToolhelp32Snapshot|Process32First|Process32Next|EnumProcesses': 'Enumera processos',
  'WritePrivateProfileStringA|WritePrivateProfileStringW|GetPrivateProfileStringA|GetPrivateProfileStringW': 'Le/escreve arquivos INI',
  'GetWindowsDirectoryA|GetWindowsDirectoryW': 'Acessa a pasta do Windows',
  'GetSystemDirectoryA|GetSystemDirectoryW': 'Acessa a pasta System32',
  'SHGetFolderPathA|SHGetFolderPathW|SHGetKnownFolderPath|SHGetSpecialFolderPathW': 'Acessa pastas especiais',
  'GetEnvironmentVariableA|GetEnvironmentVariableW|ExpandEnvironmentStringsA|ExpandEnvironmentStringsW': 'Le variables de ambiente',
  'GetVolumeInformationA|GetVolumeInformationW|GetDiskFreeSpaceExA|GetDiskFreeSpaceExW|GetLogicalDrives': 'Inspeciona disco/-volume',
  'FindFirstFileA|FindFirstFileW|FindFirstFileExW|FindNextFileA|FindNextFileW': 'Varre o disco',
  'DeleteFileA|DeleteFileW|MoveFileExA|MoveFileExW|RemoveDirectoryA|RemoveDirectoryW': 'Apaga arquivos/pastas',
  'CopyFileA|CopyFileW|CopyFileExA|CopyFileExW': 'Copia arquivos',
  'SetFileAttributesA|SetFileAttributesW': 'Muda atributos de arquivo',
  'GetFileAttributesExA|GetFileAttributesExW|GetFileAttributesA|GetFileAttributesW': 'Le atributos de arquivo',
  'CreateFileA|CreateFileW|CreateFileMappingW|MapViewOfFile': 'Acessa arquivos e mapeia memoria',
  'GetFileSizeEx|GetFileSize': 'Mede tamanho em disco',
  'GetFileVersionInfoSizeW|GetFileVersionInfoW|VerQueryValueW': 'Le versoes de arquivo',
  'GetFileTime|SetFileTime|FileTimeToLocalFileTime': 'Le datas de arquivo',
  'CryptAcquireContextA|CryptAcquireContextW|CryptGenRandom|CryptEncrypt|CryptDecrypt|BCryptGenRandom': 'Criptografia/aleatoriedade',
  'GetUserNameA|GetUserNameW': 'Le o usuario',
  'LookupAccountSidA|LookupAccountSidW|ConvertSidToStringSidA|ConvertSidToStringSidW': 'Resolve SID/permissoes',
  'AdjustTokenPrivileges|OpenProcessToken|LookupPrivilegeValueA|LookupPrivilegeValueW': 'Eleva privilegios',
  'IsWow64Process': 'DetectaWOW64/32-64 bits',
  'Wow64DisableWow64FsRedirection|Wow64RevertWow64FsRedirection': 'Alterna visao 32/64 bits',
  'CreateServiceCtrlDispatcher|RegisterServiceCtrlHandler|StartServiceCtrlDispatcher': 'Opera como servico do Windows',
  'SetWindowsHookExA|SetWindowsHookExW|UnhookWindowsHookEx': 'Instala hooks de janela',
  'SendInput|keybd_event|mouse_event': 'Envia entrada de teclado/mouse',
  'PrintDlgA|PrintDlgW|EnumPrintersA|EnumPrintersW|GetPrinterA|GetPrinterW|OpenPrinterA|OpenPrinterW': 'Imprime',
  'GetOpenFileNameA|GetOpenFileNameW|GetSaveFileNameA|GetSaveFileNameW': 'Dialogos de arquivo',
  'AddClipboardData|SetClipboardData|OpenClipboard|EmptyClipboard|GetClipboardData': 'Area de transferencia',
  'CreateFontIndirectW|CreateFontW|AddFontResourceExW|EnumFontFamiliesExW': 'Gerencia fontes',
  'SystemParametersInfoA|SystemParametersInfoW': 'Ajusta o desktop (papel de parede, tela)',
  'GetVolumeNameForVolumeMountPointA|GetVolumeNameForVolumeMountPointW|FindFirstVolumeA|FindFirstVolumeW': 'Gerencia volumes/ponto de montagem',
  'DeviceIoControl': 'Comunica com drivers',
  'GetLogicalDrives|GetDiskFreeSpaceExW|GetDiskFreeSpaceExA': 'Inspeciona disco/espaco',
  'OpenEventA|OpenEventW|CreateEventA|CreateEventW': 'Sincroniza por eventos',
  'WaitForSingleObject|WaitForMultipleObjects': 'Espera por objetos/termino de processo',
  'CreateThread|CreateRemoteThread|ResumeThread|SuspendThread': 'Cria/controla threads',
  'VirtualAlloc|VirtualProtect|VirtualQuery|VirtualFree': 'Gerencia memoria',
  'GetModuleFileNameA|GetModuleFileNameW|GetModuleHandleA|GetModuleHandleW': 'Localiza o proprio executavel',
  'WinHttpOpen|WinHttpConnect|WinHttpOpenRequest|HttpSendRequestA|HttpSendRequestW': 'Acesso HTTP/FTP',
  'MsiEnumProductsA|MsiEnumProductsW|MsiGetProductInfoA|MsiGetProductInfoW|MsiOpenProductA|MsiOpenProductW': 'Consulta produtos MSI',
  'CommandLineToArgvW|GetCommandLineA|GetCommandLineW': 'Argumentos de linha de comando',
  'SetCurrentDirectoryA|SetCurrentDirectoryW|GetCurrentDirectoryA|GetCurrentDirectoryW': 'Diretorio de trabalho',
  'GetUserDefaultUILanguage|GetSystemDefaultUILanguage|GetLocaleInfoW|SetThreadUILanguage': 'Idioma/localizacao',
};

const files = fs.readdirSync(dir).filter(f => f.endsWith('.imports.txt'));
const out = [];
out.push('# Matriz de capacidades por concorrente');
out.push('');
out.push('Fonte: tabelas de importacao e delay-load da PE, lidas pelo REA (`inspect_pe_imports`).');
out.push('Cada celula lista as APIs realmente importadas que provam a capacidade.');
out.push('');

const capsFound = new Map();
for (const f of files) {
  const id = f.replace('.imports.txt', '');
  const txt = fs.readFileSync(path.join(dir, f), 'utf8');
  const found = [];
  for (const [pat, cap] of Object.entries(CAPS)) {
    const rx = new RegExp('(^|[^A-Za-z0-9_])(' + pat.split('|').join('|') + ')([^A-Za-z0-9_]|$)', 'g');
    const hits = new Set();
    let m;
    while ((m = rx.exec(txt)) !== null) hits.add(m[2]);
    if (hits.size) found.push([cap, [...hits].join(', ')]);
  }
  capsFound.set(id, found);
}

const allCaps = [...new Set([...capsFound.values()].flat().map(c => c[0]))].sort();
out.push('| Capacidade | ' + files.map(f => f.replace('.imports.txt', '')).join(' | ') + ' |');
out.push('|---|' + files.map(() => '---').join('|') + '|');
for (const cap of allCaps) {
  const cells = files.map(f => {
    const id = f.replace('.imports.txt', '');
    const hit = (capsFound.get(id) || []).find(c => c[0] === cap);
    return hit ? hit[1] : '';
  });
  out.push('| ' + cap + ' | ' + cells.join(' | ') + ' |');
}
out.push('');
out.push('## Totais por concorrente');
out.push('');
out.push('| Concorrente | Capacidades com API importada |');
out.push('|---|---|');
for (const [id, found] of capsFound) out.push('| ' + id + ' | ' + found.length + ' |');

fs.writeFileSync(outPath, out.join('\r\n'), 'utf8');
console.log('gravado: ' + outPath + ' | concorrentes=' + capsFound.size + ' | capacidades=' + allCaps.length);
