# Evidencia do lote 30: a assinatura do desinstalador e o comando do usuario

Medido em 10/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo de verdade.

Este lote deu dono a **duas** funcionalidades que os concorrentes medem e que
o Tranal nao entregava: **a assinatura digital do desinstalador** (o que o
Soft Organizer verifica antes de rodar um executavel e o que o Wise chama de
"Trust/Unknown" nos detalhes da entrada) e **o comando do usuario** (o "executar
aplicativos externos" do Bulk Cookie Uninstaller).

No caminho, o lote encontrou e corrigiu **um bug real** e **uma duplicacao de
regra** que discordavam entre si.

## 1. O que o lote entregou

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| SOFTORGANIZER #59 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE e RECON-C) | Verificacao de assinatura digital de executaveis | PARCIAL | OK |
| WISE #62 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE e RECON-E) | Detalhes da entrada: Path, Uninstall Command, Details, Trust/Unknown/App site | PARCIAL | OK |
| BCU T4#80 (RECON-B) | Linha de comando **antes/depois** e "executar aplicativos externos" (desabilitavel) | PARCIAL | OK |

Cobertura: de **77,7%** (725 funcionalidades, 533 OK, 161 PARCIAL, 1 AUSENTE,
30 FORA) para **78,1%** (725, 536 OK, 158 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**79,6%** (795 funcionalidades, 601 OK) para **80,0%** (795, 604 OK). O BCU
subiu de 86,6% para **87,6%**, o Soft Organizer de 79,2% para **80,5%** e o
Wise de 68,6% para **69,8%**.

## 2. A assinatura do desinstalador

### O que ja existia e nao tinha dono

`Core/Discovery/UninstallerCertificate.cs` (`Verify`, `:131`) ja le a cadeia do
certificado e pergunta ao Windows (`WinVerifyTrust` com `X509Chain`) se a
assinatura e confiavel. **Nao tinha dono**: so os testes a chamavam. A pessoa
nao tinha como saber se o desinstalador do programa era assinado.

### Onde ela aparece agora

- **Tela**: `Ui/UninstallerSignatureWindow.xaml.cs:45` (`Descrever`) mostra o
  arquivo, o estado da assinatura, o signatario, o emissor, o periodo de
  validade, a cadeia e os problemas da cadeia, com o botao de copiar
  (`Ui/UninstallerSignatureWindow.xaml:23`). O item e do menu "Mais comandos"
  (`MainWindow.xaml.cs`, `CmdUninstallerSignature`).
- **Linha de comando**: `src/Tranal.Cli/SignatureCommands.cs:29` (`TryRun`)
  responde por `/signature "arquivo"` (um arquivo) e por `/signatures` (todos os
  desinstaladores instalados, com a contagem e o tempo).

### O custo medido — e por que ela nao virou coluna

MEDIDO: `UninstallerCertificate.Verify` leva cerca de **160 ms por arquivo**. A
varredura de 51 desinstaladores leva **8,1 s**. Uma coluna que verifica 51
arquivos a cada repintura da lista seria **impossivel de usar** — por isso a
verificacao e **sob demanda**: a pessoa abre a tela do programa que quer ver.

### O que a .NET 10 nao consegue fazer (medido, nao suposto)

`msiexec.exe`, `cmd.exe` e `notepad.exe` **nao tem PKCS#7 embutido no PE**
(medido por parser de PE: `cert off=0 size=0`). A assinatura deles vem do
**catalogo de componentes do Windows**, nao de um editor de software. O
`Get-AuthenticodeSignature` do PowerShell diz "Valid" para os tres; o
`X509Certificate.CreateFromSignedFile` **falha** ("O objeto necessario nao foi
encontrado") para os tres, tanto no .NET 10 quanto no .NET Framework 4.0.

E o `WinVerifyTrust` nao distingue os dois casos: com a acao
`00AAC56B-CD44-11d0-8CC2-00C04FC295EE` ele devolve `0x800B0100`
(CERT_E_UNTRUSTEDROOT) **tanto para o `msiexec.exe` (catalog-signed) quanto
para o `tranal.exe` (nao assinado)**, em todas as combinacoes medidas de
`fdwRevocationChecks` (0, 1, 16, 64) x `dwProvFlags` (0, 0x100, 0x200, 0x400,
0x800, 0x1000), com caminho e com handle de arquivo.

As tentativas de ler o catalogo por P/Invoke tambem foram medidas:
`CryptCATAdminAcquireContext` (v1) funciona, mas
`CryptCATAdminCalcHashFromFileHandle` da **access violation 0xC0000005**;
`CryptCATAdminAcquireContext2` com o GUID do Sistema da **errno 5
(ACCESS_DENIED)**; `CryptQueryObject` com `CERT_QUERY_OBJECT_FILE` da
**0x80070057 (E_INVALIDARG)** em todas as flags testadas; e
`WTHelperGetProvSignerFromChain` devolve **nulo** mesmo com assinatura valida.

**Decisao: nao implementar o catalogo.** A deteccao por catalogo nao e
alcancavel com as APIs gerenciadas do .NET 10 nem com P/Invoke simples e
estavel. Em vez de mentir, o produto **avisa**: a tela diz que o executavel e um
componente do Windows e que a assinatura vem do catalogo de componentes do
Windows (`Signature.WindowsComponent`), e que o programa e desinstalado pelo
instalador do Windows (`Signature.WindowsInstaller`).

## 3. O bug real: a regra do executavel do desinstalador estava em seis lugares

A pergunta "qual e o executavel do desinstalador" tinha **seis copias** com
resultados **discordantes** entre si:

| Onde | O que respondia |
|---|---|
| `Core/ProgramScanner.cs:330` (`ExtractExecutablePath`) | o nome cru do comando |
| `Core/Management/ExecutablePath.cs` (`ExecutablePathResolver.Extract`) | o nome cru do comando |
| `Core/Discovery/DriveScanner.cs:181` (`ExtractExecutable`) | o nome cru do comando |
| `Core/Tools/AutorunManager.cs:1405` (`ExtractExecutablePath`) | o nome cru do comando |
| `Core/Tools/FileAssociator.cs:416` (`ExtractExecutable`) | o nome cru do comando |
| as copias novas do lote 30 (a tela e a linha de comando) | cada uma a sua |

O sintoma apareceu no aplicativo de verdade: o primeiro item da lista e o
"AMD Install Manager", cujo comando de desinstalacao e `MsiExec.exe /X{...}`
(**sem caminho**). A coluna "Localizacao do desinstalador" mostrava
`MsiExec.exe`, e a tela de assinatura dizia que o programa **nao tinha
desinstalador registrado** — porque `MsiExec.exe` nao existe no disco com esse
nome. A linha de comando pulava o programa pelo mesmo motivo.

**A correcao**: a pergunta tem uma resposta so, no nucleo, em
`Core/Discovery/UninstallerExecutable.cs`:

- `Declared(program)` (`:41`) — **o que o registro nomeia** (sem inventar
  caminho; vazio quando o comando nao nomeia executavel);
- `Resolve(program)` (`:64`) — **onde o executavel esta**: caminho absoluto
  quando o comando ja traz raiz, e, quando o comando traz so o nome,
  **procura no PATH como o proprio Windows faz** (e a pasta System32 entra na
  busca). E por isso que `MsiExec.exe /X{...}` vira
  `C:\Windows\System32\msiexec.exe` — o executavel que realmente roda;
- `IsWindowsInstaller(program)` (`:88`) e `IsWindowsComponent(caminho)`
  (`:110`) — os dois casos de componente do Windows.

Os tres consumidores (a coluna, a tela e a linha de comando) passam a chamar
o **mesmo** metodo. As copias duplicadas que o lote criou foram removidas
(`UninstallerLocationOf`, em `ProgramColumns.cs`, deixou de existir).

## 4. O achado da tela em duas linguas

A primeira versao da tela mostrava, lado a lado, o estado **traduzido**
("Estado: Sem assinatura digital") e o texto do **nucleo**, em portugues fixo
("O arquivo nao tem assinatura digital"). A mesma informacao duas vezes — e, no
caso do instalador do Windows, **contraditoria**: a nota do componente do
Windows dizia que a assinatura vem do catalogo do Windows, e o texto do nucleo
dizia que o arquivo nao tem assinatura digital.

**A correcao estrutural**: o nucleo separa as duas coisas.

- `UninstallerSignature.Message` — a explicacao do estado, **em portugues**, para
  a linha de comando (a CLI e pt-BR por decisao do projeto);
- `UninstallerSignature.Detail` (`:82`) — o **detalhe tecnico** da falha de
  verificacao (o texto da excecao), que a interface envolve numa frase
  traduzida (`Signature.VerifyFailed`).

E a tela **nao** mostra mais o texto do nucleo: ela fala a lingua do catalogo
(1503 chaves em pt-BR e en-US, as mesmas nos dois idiomas). O teste
`A_tela_fala_a_lingua_do_catalogo` garante que nenhuma chave crua apareca no
texto.

## 5. O comando do usuario

O "executar aplicativos externos" do BCU e o "rodar o comando antes/depois" do
Revo. O Tranal **tinha** o comando antes/depois (a tela e a gravacao por
programa), mas o `ShellCommand` (`Core/Automation/UninstallPlan.cs`) nao tinha
quem o chamasse, e nao havia como executar o comando na hora para ver o que ele
faz.

Agora:

- o botao **"Executar"** da tela de comandos roda o comando e mostra a saida
  (`Ui/UninstallCommandsWindow.xaml.cs:67`, `RunButton_Click`, com a saida em
  `Ui/UninstallCommandsWindow.xaml:38`);
- a execucao de aplicativos externos e **desabilitavel**, como no BCU: a
  preferencia `AllowRunningUserCommands`
  (`Core/Settings/AppSettings.cs:123`, ligada por padrao) e marcada em
  `Ui/OptionsWindow.xaml:139` (`OptAllowUserCommands`) e aplicada em
  `Ui/UninstallCommandsWindow.xaml.cs:49` — desligada, o botao some e o comando
  continua sendo gravado (e roda no desinstalador, se a pessoa pediu).

## 6. Os testes

- `src/Tranal.Tests/Lote30FeatureTests.cs`: **23 testes proprios** (a
  assinatura de arquivo inexistente e vazio, o `kernel32.dll` assinado e
  confiavel, o mapeamento dos codigos de estado, o detalhe da falha, os
  consumidores na tela e na linha de comando, a recusa do uso errado, o
  comando que roda e devolve a saida, o caminho que escapa o `%`, a opcao
  desabilitavel, a regra do executavel em um lugar so e as chaves de catalogo
  nos dois idiomas).
- `tools/test-lote30.ps1`: **13 verificacoes** no aplicativo de verdade (5 da
  linha de comando e 8 da tela). O teste **nao instala nem remove nada**: ele so
  LE a assinatura de arquivos do proprio Windows.
- Suite completa: **1595 testes verdes**.
