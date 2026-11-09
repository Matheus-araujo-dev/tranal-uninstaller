# Feed de atualizacoes dos programas

Este arquivo e o feed padrao que o Tranal consulta para saber quais
programas instalados tem versao nova publicada. O endereco usado pelo
aplicativo e:

```
https://raw.githubusercontent.com/Matheus-araujo-dev/tranal-uninstaller/main/feed/programas.json
```

Ele pode ser trocado nas preferencias do Tranal (Opcoes > Atualizacoes).

## Formato

```json
{
  "atualizacoes": [
    {
      "chave": "7zip",
      "programa": "7-Zip 24.09 (x64)",
      "editor": "Igor Pavlov",
      "versao": "24.09",
      "url": "https://www.7-zip.org/a/7z2409-x64.msi",
      "notas": "Correcoes de estabilidade."
    }
  ]
}
```

| Campo     | Significado |
|-----------|-------------|
| `chave`   | Identificador da entrada. E o que a lista negra Casa (o nome do programa tambem casa). |
| `programa`| O nome do programa **exatamente** como aparece na lista de instalados. E o que faz o cruzamento. |
| `editor`  | O fabricante. Serve para desambiguar dois programas com o mesmo nome. |
| `versao`  | A versao publicada. A comparacao e a semantica (`2.0` e menor que `10.0`; `1.0-beta` e menor que `1.0`). |
| `url`     | O endereco do instalador ou da pagina de atualizacao. E o que o Tranal abre. |
| `notas`   | As notas da versao. Opcional. |

Um arquivo invalido (JSON quebrado) devolve lista vazia: um feed fora do
ar nao pode derrubar a verificacao.

## Onde a regra mora

- Leitura do JSON: `Core/Updates/SoftwareUpdater.cs` (`UpdateFeedReader`).
- Comparacao de versoes: `Core/Updates/VersionComparer.cs`.
- Endereco padrao e lista negra: `Core/Updates/UpdateService.cs`.
