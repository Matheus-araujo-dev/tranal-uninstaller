# Probes da auditoria

Programas pequenos, **fora do Tranal**, que medem coisas que nao da para
medir pelo codigo do programa.

| Probe | O que mede | Medido em |
|---|---|---|
| `probe-wpn` | O banco de notificacoes do proprio Windows (`wpndatabase.db`), que e onde a Central de Acoes guarda o que o usuario ve. | `docs/auditoria/EVIDENCIA-CENTRAL-ACoes.md` |
| `probe-balao2` | Dispara um balao de bandeja de verdade (`Shell_NotifyIcon` com `NIF_INFO`), com AppUserModelID, e mostra o que o Windows aceitou. | `docs/auditoria/EVIDENCIA-CENTRAL-ACoes.md` |

## Como rodar

```
dotnet run --project tools\auditoria\probes\probe-wpn
dotnet run --project tools\auditoria\probes\probe-balao2
```

A medicao da Central de Acoes e a contagem do banco **antes** e **depois**
do balao: e a diferenca entre as duas contagens que diz se o balao chegou
la.

Nenhum dos dois mexe no Tranal, no registro do usuario nem nos arquivos do
programa: o `probe-balao2` cria e apaga a sua propria chave de AppUserModelID
em `HKCU\Software\Classes\AppUserModelId`.
