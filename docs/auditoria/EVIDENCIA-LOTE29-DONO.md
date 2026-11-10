# Evidencia do lote 29: o que ja existia e nao tinha dono

Medido em 10/11/2026, no codigo de `src/Tranal.Uninstaller/`, na suite de
testes e no aplicativo de verdade.

Este lote nao inventou feature: ele deu **dono de produto** a modulos que
ja estavam prontos no nucleo e nao eram chamados por ninguem. E, no caminho,
corrigiu um **bug real** que so apareceu quando o consumo passou a existir.

## 1. A medicao que abriu o lote

Uma varredura do codigo (`tools/auditoria/` + `src/`)(listou **16 classes
estaticas sem consumidor de produto** - so os testes as chamavam, ou ninguem
chamava:

| Modulo | O que e | Quem usava |
|---|---|---|
| `Core/Discovery/ShortcutDiscovery.cs` | varredura de atalhos do Menu Iniciar e da Area de Trabalho | so os testes |
| `Core/Management/WindowsUpdateManager.cs` | remocao de KB do Windows (`wusa.exe`) | so os testes |
| `Core/Reporting/PortableArchive.cs` | pacote portatil em `.zip` | so os testes |
| `Core/Recovery/ProgramBackup.cs` | backup do programa com undo/redo | so os testes |
| `Core/Recovery/UndoChangesCenter.cs` | o "Undo Changes Center" | so os testes |
| `Core/Modules/ShortcutFixer.cs` | varredura e reparo de atalhos quebrados | so os testes |

"Modulo orfao nao conta": o codigo existe, mas a pessoa nunca chega nele.
Nenhuma das linhas do RECON sabia disso - elas mediam o resultado (o que a
tela mostra), e nao o codigo morto.

## 2. As linhas

| Recorrencia | Funcionalidade | Antes | Agora |
|---|---|---|---|
| GEEK T2#34 (RECON-CATALOGO-HIBIT-GEEK, RECON-A, RECON-FEATURES-NOVAS) | Varredura de atalhos do Menu Iniciar e da Area de Trabalho, achando icone e localizacao | PARCIAL | OK |
| GEEK #13 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | a mesma funcionalidade, na tabela do Geek | NAO EXISTE | JA EXISTE |
| GEEK T4#80 (RECON-CATALOGO-HIBIT-GEEK) | Fixador de Atalhos (atalhos corrompidos) | AUSENTE | JA EXISTE |
| HiBit #80 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | a mesma funcionalidade, na tabela do HiBit | NAO EXISTE | JA EXISTE |
| HiBit T2#21 (RECON-CATALOGO-HIBIT-GEEK) | Desinstalar atualizacoes do Windows (`wusa.exe /uninstall`) | OK (evidencia errada) | OK (evidencia medida) |
| HiBit #21 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | a mesma funcionalidade, na tabela do HiBit | NAO EXISTE | JA EXISTE |
| IObit #29 (RECON-HIBIT-GEEK-IOBIT-UNINSTALR) | Desinstalar Windows Updates | NAO EXISTE | JA EXISTE |
| TOTALUNINSTALL T6#34 (RECON-D) | Limite de tamanho da pasta de backup, em MB | PARCIAL | OK |
| TOTALUNINSTALL T6#35 (RECON-D) | Transferir os programas monitorados para outro PC, via `.zip` | PARCIAL | OK |
| SOFTORGANIZER #22 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE e RECON-C) | Undo Changes Center | PARCIAL | OK |
| SOFTORGANIZER #25 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE e RECON-C) | Aviso "use o Undo Changes Center para cancelar" | AUSENTE | OK |
| SOFTORGANIZER #23 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE) | Reverter a desinstalacao cronologicamente | PARCIAL | PARCIAL (evidencia medida) |
| UNINSTALLTOOL #38 (RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE) | Reverter a instalacao rastreada | PARCIAL | PARCIAL (evidencia medida) |
| SOFTORGANIZER #24 (RECON-C) | Descarte de mudancas antigas do historico | PARCIAL | PARCIAL (evidencia medida) |
| UNINSTALLTOOL T3#38 (RECON-D) | a mesma funcionalidade, na tabela do Uninstall Tool | PARCIAL | PARCIAL (evidencia medida) |

Cobertura: de **77,0%** (725 funcionalidades, 528 OK, 166 PARCIAL, 1 AUSENTE,
30 FORA) para **77,7%** (725, 533 OK, 161 PARCIAL, 1 AUSENTE, 30 FORA). Na
prova de cobertura com o Revo Pro dentro (`PARIDADE-11-CONCORRENTES.md`), de
**79,0%** (795 funcionalidades, 596 OK) para **79,6%** (795, 601 OK). O Geek
subiu de 86,4% para **88,6%**, o Total Uninstall de 74,5% para **76,6%** e o
Soft Organizer de 76,6% para **79,2%**.

## 3. O que o lote deu de dono

| Modulo | Quem chama agora | O que a pessoa passa a ver |
|---|---|---|
| `ShortcutDiscovery` | tela de Descoberta (`Ui/Discovery/DiscoveryWindow.xaml.cs:245`) | a categoria "Atalhos do Menu Iniciar e da Area de Trabalho", com icone e local de cada programa |
| `WindowsUpdateManager` | tela de Descoberta (`:131`) e linha de comando (`src/Tranal.Cli/IntegrationCommands.cs:34` e `:35`) | `/windows-updates` lista o que esta instalado; `/windows-update-remove "KB..."` remove |
| `PortableArchive` + `ProgramListImporter` | linha de comando (`src/Tranal.Cli/PortableCommands.cs:33`, `:34`, `:35`) | `/portable-export`, `/portable-import` e `/portable-import-list` |
| `ProgramBackup` + `UndoChangesCenter` | o fluxo de remocao (`Core/UninstallSupport.cs:1620`) | a remocao entra no Centro de mudancas, e o conteudo volta |
| `ShortcutFixer` | a tela de limpeza (`Ui/Tools/CleanupViews.cs:419` e `:456`) | a varredura e o reparo dos atalhos quebrados |

A regra do "quem chama" continua em um lugar so: `Core/UninstallSupport.cs`
(`BackupLeftovers`, `:1620`) e a tela e a linha de comando chamam o **mesmo**
metodo do nucleo.

## 4. O bug que o consumo revelou

Ligar o centro de mudancas ao fluxo de remocao ** quebrou um bug que estava
escondido desde sempre:

`ProgramBackup.GravarOperacoes` relia o arquivo das operacoes do disco e o
regravava. A marcacao "esta desfeita" vivia so na memoria do metodo que
desfez - e o que voltava para o disco era o estado antigo. Resultado
medido: **desfazer devolvia o conteudo, mas a mudanca continuava marcada
como nao desfeita**.

A correcao (`Core/Recovery/ProgramBackup.cs`): o metodo que muda o estado
**entrega a operacao alterada** para a gravacao, e a gravacao substitui a
linha correspondente em vez de reler o disco.

> `GravarOperacoes(RecoverOperation alterada)` - a alteracao vem no
> parametro; reler o arquivo e regrava-lo perderia o estado novo.

O bug nunca apareceu antes porque **ninguem consumia o estado**: o centro de
mudancas nao existia na interface, e a CLI so restaurava sobras.

## 5. O furo da propria auditoria

Ao remediar as linhas, a cadeia mostrou que o `RECON-D-2026-11-08.md`
inteiro **nao aplicava nada**: o arquivo escreve o estado na segunda coluna
(`| T6#34 | PARCIAL | ... |`) e o aplicador procurava o estado a partir da
terceira. MEDIDO: 38 linhas (Total Uninstall) e 48 linhas (Uninstall Tool)
passaram a aplicar depois da correcao.

A correcao foi em `tools/auditoria/aplicar-recon.ps1`: a busca do estado
comeca na coluna 2. O numero de cobertura **nao mudou** com a correcao
(77,0% antes e depois) - o furo nao inflava nada, ele apenas impedia que as
remediacoes do RECON-D valessem.

E o mesmo raciocinio vale para o resto: **a funcionalidade que aparece em
varios relatorios precisa ser corrigida em todos**. MEDIDO: o
`SOFTORGANIZER #22` estava remediado no `RECON-UNINSTALLTOOL-SOFTORGANIZER-WISE.md`
e continuava PARCIAL, porque o `RECON-C-2026-11-08.md` (aplicado por ultimo
para o Soft Organizer) ainda media o estado antigo. As quatro linhas do
RECON-C foram corrigidas tambem.

## 6. Os testes

- `src/Tranal.Tests/Lote29FeatureTests.cs`: **25 testes proprios** (o modulo
  tem consumidor, o icone do atalho, o numero do artigo do KB, a recusa de
  artigo invalido, o pacote portatil, o registro no centro de mudancas, o
  desfazer/refazer, a expiracao do snapshot e as chaves de catalogo).
- `tools/test-lote29.ps1`: **10 verificacoes** no aplicativo de verdade (5 da
  linha de comando e 5 da tela). O teste **nao instala nem remove nada**: ele
  le a lista de atualizacoes do Windows, grava um pacote portatil na pasta
  temporaria e **apaga tudo no final**.
- Suite completa: **1572 testes verdes**.
