using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Updates;
using Tranal.Uninstaller.Ui.Shared;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// As telas de atualizacao: as versoes novas dos programas instalados e a
/// versao nova do proprio Tranal.
///
/// As duas usam o mesmo painel generico (<see cref="ItemListPanel"/>) e as
/// duas regras morem no nucleo (<see cref="UpdateService"/>,
/// <see cref="SoftwareUpdater"/> e <see cref="AutoUpdateClient"/>): a tela
/// apenas mostra o que o nucleo decidiu e devolve o comando.
/// </summary>
public static class UpdateViews
{
    /// <summary>Cria a tela da ferramenta pelo id do catalogo.</summary>
    /// <param name="toolId">O id da ferramenta.</param>
    public static FrameworkElement Create(string toolId) => toolId switch
    {
        ToolCatalog.SoftwareUpdates => CreateSoftwareUpdates(),
        ToolCatalog.AppUpdate => CreateAppUpdate(),
        _ => new ItemListPanel(),
    };

    /// <summary>
    /// Linha exibida pela tela de atualizacoes dos programas.
    /// </summary>
    private sealed class UpdateRow : INotifyPropertyChanged
    {
        public string Program { get; init; } = string.Empty;
        public string Publisher { get; init; } = string.Empty;
        public string Installed { get; init; } = string.Empty;
        public string Available { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        private bool _selected = true;

        /// <summary>True quando o item esta marcado.</summary>
        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value) return;

                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        /// <summary>Item original do nucleo.</summary>
        public object Source { get; init; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Tela das atualizacoes dos programas instalados.
    ///
    /// O que ela faz: le o feed de atualizacoes, cruza com o que esta
    /// instalado na maquina e mostra o que tem versao nova — inclusive o que
    /// foi deixado de fora e por que (a lista negra).
    /// </summary>
    private static FrameworkElement CreateSoftwareUpdates()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<UpdateRow>();

        // O plano vivo: e o que o botao "Atualizar" executa. Fica aqui para
        // a tela nao ter que remontar o plano a cada clique.
        UpdatePlan plano = new(Array.Empty<UpdatePlanItem>());

        panel.Configure(
            Loc.Get("Module.SoftwareUpdates"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Program"), Width = 250, BindingPath = "Program" },
                new ColumnDef { Header = Loc.Get("Common.Publisher"), Width = 190, BindingPath = "Publisher" },
                new ColumnDef { Header = Loc.Get("Update.CurrentVersion"), Width = 120, BindingPath = "Installed" },
                new ColumnDef { Header = Loc.Get("Update.LatestVersion"), Width = 120, BindingPath = "Available" },
                new ColumnDef { Header = Loc.Get("Common.Status"), Width = 220, BindingPath = "Status" },
                new ColumnDef { Header = string.Empty, Width = 40, IsCheckable = true },
            },
            new[]
            {
                new CommandDef { AutomationId = "SoftwareUpdateCheck", Text = Loc.Get("Tool.Updates.Check"), IsPrimary = true, Action = () => Verificar() },
                new CommandDef { AutomationId = "SoftwareUpdateApply", Text = Loc.Get("Tool.Updates.Apply"), Action = () => Atualizar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Updates.Starting");
        return panel;

        void Verificar()
        {
            panel.RunAsync(async () =>
            {
                var instalados = await Task.Run(() => ProgramScanner.Scan(new ProgramScanner.ScanOptions
                {
                    IncludeSystemComponents = false,
                    IncludeMsiProducts = true,
                }));

                var atualizador = UpdateService.AtualizadorDeProgramas();
                plano = await atualizador.MontarPlanoAsync(instalados);

                rows.Clear();

                foreach (var item in plano.Items)
                {
                    rows.Add(new UpdateRow
                    {
                        Program = item.Candidate.ProgramName,
                        Publisher = item.Candidate.Publisher,
                        Installed = item.Candidate.InstalledVersion,
                        Available = item.Candidate.AvailableVersion,
                        Status = item.Pending
                            ? Loc.Get("Tool.Updates.Available")
                            : Loc.Get("Tool.Updates.Blacklisted", item.Reason),
                        Source = item,
                    });
                }

                panel.PanelStatus = rows.Count == 0
                    ? Loc.Get("Tool.Updates.Empty")
                    : Loc.Get("Status.Done") + " — " + rows.Count + " item(ns)";
            });
        }

        void Atualizar()
        {
            var marcados = rows.Where(r => r.Selected).Select(r => (UpdatePlanItem)r.Source).ToList();
            if (marcados.Count == 0)
            {
                panel.PanelStatus = Loc.Get("Tool.Updates.NothingSelected");
                return;
            }

            panel.RunAsync(async () =>
            {
                var atualizador = UpdateService.AtualizadorDeProgramas();
                var resultados = await atualizador.ExecutarAsync(new UpdatePlan(marcados));

                var abertos = resultados.Count(r => r.Success);
                panel.PanelStatus = Loc.Get("Tool.Updates.Opened", abertos, resultados.Count);
            });
        }
    }

    /// <summary>
    /// Tela da atualizacao do proprio Tranal.
    ///
    /// Mostra a versao que esta rodando, a versao publicada e as notas da
    /// versao. O botao de baixar deixa o pacote na pasta temporaria e abre
    /// o instalador.
    /// </summary>
    private static FrameworkElement CreateAppUpdate()
    {
        var panel = new ItemListPanel();
        var rows = new ObservableCollection<UpdateRow>();

        UpdateCheckResult? resultado = null;

        panel.Configure(
            Loc.Get("Module.AppUpdate"),
            new[]
            {
                new ColumnDef { Header = Loc.Get("Common.Type"), Width = 200, BindingPath = "Program" },
                new ColumnDef { Header = Loc.Get("Common.Version"), Width = 220, BindingPath = "Available" },
                new ColumnDef { Header = Loc.Get("Common.Detail"), Width = 520, BindingPath = "Status" },
            },
            new[]
            {
                new CommandDef { AutomationId = "AppUpdateCheck", Text = Loc.Get("Tool.Updates.Check"), IsPrimary = true, Action = () => Verificar() },
                new CommandDef { AutomationId = "AppUpdateDownload", Text = Loc.Get("Update.Download"), Action = () => Baixar() },
            });

        panel.SetItems(rows);
        panel.PanelStatus = Loc.Get("Tool.Updates.Starting");
        return panel;

        void Verificar()
        {
            panel.RunAsync(async () =>
            {
                resultado = await UpdateService.VerificarTranalAsync();

                rows.Clear();

                var atual = UpdateService.VersaoAtual();

                rows.Add(new UpdateRow
                {
                    Program = Loc.Get("Update.CurrentVersion"),
                    Available = atual,
                    Status = Loc.Get("Tool.Updates.App"),
                });

                if (resultado is null || !resultado.Success)
                {
                    panel.PanelStatus = resultado?.ErrorMessage ?? Loc.Get("Update.CheckFailed");
                    return;
                }

                var publicada = resultado.LatestVersion ?? string.Empty;
                var nova = UpdateService.TemVersaoNova(atual, publicada);

                rows.Add(new UpdateRow
                {
                    Program = Loc.Get("Update.LatestVersion"),
                    Available = publicada,
                    Status = nova
                        ? Loc.Get("Tool.Updates.Available")
                        : Loc.Get("Update.NoUpdate"),
                });

                if (!string.IsNullOrWhiteSpace(resultado.ReleaseNotes))
                {
                    rows.Add(new UpdateRow
                    {
                        Program = Loc.Get("Tool.Updates.Notes"),
                        Available = string.Empty,
                        Status = resultado.ReleaseNotes!,
                    });
                }

                panel.PanelStatus = nova ? Loc.Get("Tool.Updates.Available") : Loc.Get("Update.NoUpdate");
            });
        }

        void Baixar()
        {
            var url = resultado?.DownloadUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                panel.PanelStatus = Loc.Get("Update.NoUpdate");
                return;
            }

            panel.RunAsync(async () =>
            {
                var destino = UpdateService.CaminhoDoPacote(url!);

                var ok = await AutoUpdateClient.DownloadAsync(url!, destino, null, default);

                if (!ok)
                {
                    panel.PanelStatus = Loc.Get("Update.CheckFailed");
                    return;
                }

                AbrirArquivo(destino);
                panel.PanelStatus = Loc.Get("Update.Done") + " — " + destino;
            });
        }
    }

    /// <summary>
    /// Abre o pacote baixado com o programa padrao do Windows.
    /// </summary>
    /// <param name="caminho">O caminho do arquivo.</param>
    private static void AbrirArquivo(string caminho)
    {
        try
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch
        {
            // O Windows recusou abrir o pacote: o caminho fica na linha de
            // status, e o usuario abre o arquivo na mao.
        }
    }
}
