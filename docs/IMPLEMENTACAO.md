# Plano de implementação — do backlog medido ao produto

Este documento é o mapa de execução do backlog registrado em
[`docs/auditoria/MATRIZ.md`](auditoria/MATRIZ.md) §F. Ele existe para que a
execução não dependa da memória de ninguém: cada lote diz **o que** vai ser
construído, **onde o código mora** e **como se prova que ficou pronto**.

> Regra que vale para todos os lotes: **a regra fica em um lugar só e o resto
> consome.** Nenhum lote cria uma segunda interpretação de "sobra", "confiança",
> "backup", "regra", "snapshot" ou "rastreio".

## Como o backlog está dividido

Os 64 itens do §F foram agrupados em **8 lotes**. O agrupamento não é por
concorrente (a matriz H continua sendo a fonte de "de onde veio"), e sim por
**área do código** — é o que permite que dois lotes andersem em paralelo sem
brigar pelo mesmo arquivo.

| Lote | Área | Itens | Arquivos novos (diretório exclusivo) |
|---|---|---|---|
| **A** | Motor de desinstalação e automação | 5, 6, 9, 10, 15, 20, 24, 43 | `Core/Automation/`, `Ui/Automation/` |
| **B** | Rastreio, snapshot e comparação A×B | 7, 30, 31, 32, 36, 41, 42, 44, 48 | `Core/Tracing/`, `Ui/Tracing/` |
| **C+D** | Backup, histórico, recuperação e regras | 22, 23, 26, 33, 35, 49 | `Core/Recovery/`, `Core/Rules/`, `Ui/Recovery/` |
| **E** | Descoberta ampliada | 8, 11, 12, 13, 14, 16 | `Core/Discovery/`, `Ui/Discovery/` |
| **F** | Painéis de gestão do sistema | 18, 19, 21, 25, 37 | `Core/Management/`, `Ui/Management/` |
| **G** | Exportação, importação, impressão | 29, 34, 38, 39, 45, 46, 56 | `Core/Reporting/`, `Ui/Reporting/` |
| **H** | Interface e personalização | 17, 50, 51, 52, 53, 54, 55, 57, 58, 59, 60, 61, 62, 63, 64 | `Core/Appearance/`, `Ui/Appearance/`, `Core/Columns/` |

Cada lote é uma unidade de entrega: compila, tem teste, e é publicado sozinho.
Um lote quebrado não derruba os outros.

## Contratos que atravessam os lotes

Estes nomes são **reservados**: quando um lote cita um destes tipos, ele está
citando este documento.

### Sessão de desinstalação (Lote A)

```csharp
// Core/Automation/UninstallPlan.cs
public enum UninstallStepKind { PreCommand, BuiltIn, Scan, Remove, PostCommand, Manual }
public sealed class UninstallStep { UninstallStepKind Kind; string Description; string? Command; }
public sealed class UninstallPlan { string ProgramName; UninstallMode Mode; IReadOnlyList<UninstallStep> Steps; string Id; }

// Core/Automation/UninstallSession.cs — retomada após reinício
public sealed class UninstallSession {
    string Id; string ProgramName; UninstallPlan Plan; int LastCompletedStep; string FilePath;
    static UninstallSession Save(UninstallSession); static IReadOnlyList<UninstallSession> Pending();
    static UninstallSession? Load(string id);
}
```

A sessão é o que faz o item **43** ("continuar a desinstalação interrompida por
reinício") existir: o passo concluído é gravado a cada etapa, e a retomada
reaproveita o mesmo plano.

### Motor de regras (Lote C+D)

```csharp
// Core/Rules/RemovalRule.cs
public enum RuleCategory { Include, Exclude, Protect, Cleanup }
public sealed class RemovalRule {
    string Id; RuleCategory Category; string Pattern; bool IsRegex; bool IsPath; string? Note;
}
// Core/Rules/RuleEngine.cs — o ÚNICO lugar que decide "isto entra na varredura?"
public static class RuleEngine {
    static IReadOnlyList<RemovalRule> Load(); static void Save(IReadOnlyList<RemovalRule>);
    static RuleDecision Decide(string pathOrKey, IReadOnlyList<RemovalRule> rules);
}
```

`Core/UninstallSupport.cs` (varredura de sobras) e `Core/Tools/JunkCleaner.cs`
passam a **consumir** `RuleEngine.Decide` — hoje os dois duplicam o julgamento.

### Rastreio versionado (Lote B)

```csharp
// Core/Tracing/TraceEntry.cs
public enum TraceChangeKind { Created, Deleted, Renamed, Overwritten, Unknown }
public sealed class TraceEntry {
    string Path; TraceChangeKind Kind; string? OldName; string? OldValue;
    string? NewValue; long? Size; DateTimeOffset Timestamp; string? OwnerProcess;
}
```

O `TracePayload` legado (`Core/Modules/TraceStore.cs:502`) guarda só
`Files`/`Folders`/`RegistryKeys`. O lote B cria o formato versionado **ao
lado** e a integração faz a migração — o formato novo é o canônico.

### Formatos de saída (Lote G)

```csharp
// Core/Reporting/ReportFormats.cs
public static class ReportFormats {
    static string ToExcel(IReadOnlyList<InstalledProgram> programs, IReadOnlyList<ProgramColumn>? columns);
    static string ToReg(IEnumerable<RegistryChange> changes);
    static string ToBatchScript(IEnumerable<InstalledProgram> programs);
}
```

`Core/Exporters.cs` continua sendo a porta de entrada do TXT/CSV/HTML/XML
já existente; o Excel e o `.reg` entram **no mesmo arquivo** para que não
exista uma segunda porta de exportação.

## Como se prova que um lote ficou pronto

1. `dotnet build` da solução em **Release** na cópia limpa do `HEAD` da branch:
   **0 erros e 0 avisos**.
2. `dotnet test`: **todos verdes**, sem teste ignorado para "voltar depois".
3. `git archive HEAD` → pasta limpa → build + teste da solução: é a única
   verificação que pega o erro que a árvore de trabalho esconde (já pegou dois:
   `a946e3a` e `ede7ff4`).
4. O item do backlog vira **✅** na §F da `MATRIZ.md`, com a frase que diz
   **onde** o código mora.
5. A regra nova entra no `AGENTS.md`, porque regra que não está escrita é
   regra que o próximo leitor adivinha.

## Estado

| Lote | Itens | Situação |
|---|---|---|
| A | 8 | em andamento |
| B | 9 | em andamento |
| C+D | 6 | em andamento |
| E | 6 | em andamento |
| F | 5 | planejado |
| G | 7 | planejado |
| H | 14 | planejado |
| já entregue | 9 | ✅ (§F: 1, 2, 3, 4, 27, 28, 40, 47 e a classificação) |

**55 itens pendentes** no início desta execução.
