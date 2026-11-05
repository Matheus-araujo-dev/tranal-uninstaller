using Xunit;

// A suíte toca estado global: o arquivo de preferências, a pasta de backups,
// a lista de exclusões e chaves de registro do HKCU. Duas classes que rodam em
// paralelo podem pisar uma na outra — e o pior caso já aconteceu: uma suíte
// apagou pontos de restauração reais do usuário e gravou o settings.json
// dele.
//
// A suíte inteira leva 1 segundo: serializar tudo custa quase nada e remove
// uma classe inteira de falha intermitente (verde local, vermelho no CI).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
