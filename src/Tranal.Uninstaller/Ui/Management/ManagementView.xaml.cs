using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Ui.Management;

/// <summary>
/// Uma permissao como aparece na lista: o tipo e o estado sao **texto**, e
/// nao o valor do enumerador. Um enumerador que vaza para a tela mostra
/// "ScheduledTask" onde o usuario le "Tarefa agendada".
/// </summary>
internal sealed class PermissionRow
{
    public required AppPermissionEntry Entry { get; init; }
    public string KindText { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string StateText { get; init; } = string.Empty;
}

/// <summary>
/// Um achado de saude como aparece na lista.
/// </summary>
internal sealed class HealthRow
{
    public required HealthFinding Finding { get; init; }
    public string CategoryText { get; init; } = string.Empty;
    public string SeverityText { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

/// <summary>
/// Painel de gestao do sistema: as permissoes que um programa deixa no
/// Windows, os servicos, as tarefas agendadas, os drivers e a saude do
/// software.
///
/// Sao cinco paineis que o Tranal tinha espalhados (o gerenciador de
/// inicializacao, o painel de servicos do Windows, o catalogo de ferramentas)
/// ou que nao existiam (os drivers, a saude). Aqui eles ficam juntos porque
/// a pergunta do usuario e uma so: **o que este programa deixa rodando?**
///
/// A tela e embutida no painel de ferramentas da janela principal, como as
/// outras: nao e uma janela a parte que o usuario abre e fecha.
/// </summary>
public partial class ManagementView : UserControl
{
    private readonly ObservableCollection<InstalledProgram> _programs = new();
    private readonly ObservableCollection<PermissionRow> _permissions = new();
    private readonly ObservableCollection<WindowsService> _services = new();
    private readonly ObservableCollection<ScheduledTask> _tasks = new();
    private readonly ObservableCollection<DriverPackage> _drivers = new();
    private readonly ObservableCollection<HealthRow> _health = new();

    /// <summary>
    /// Constroi o painel e prepara as listas.
    ///
    /// A carga acontece no <c>Loaded</c>: a listagem de servicos e de tarefas
    /// agendadas conversa com o Windows e demora — e um painel que congela na
    /// abertura e pior do que um painel que abre e preende.
    /// </summary>
    public ManagementView()
    {
        InitializeComponent();

        // GridViewColumn.Header nao aceita binding: texto via codigo.
        SetHeaders();

        ProgramBox.ItemsSource = _programs;
        ProgramBox.DisplayMemberPath = nameof(InstalledProgram.DisplayName);
        PermissionsList.ItemsSource = _permissions;
        ServicesList.ItemsSource = _services;
        TasksList.ItemsSource = _tasks;
        DriversList.ItemsSource = _drivers;
        HealthList.ItemsSource = _health;

        Loaded += (_, _) => LoadAll();
        ProgramBox.SelectionChanged += (_, _) => LoadPermissions();
    }

    /// <summary>
    /// Coloca os rotulos das colunas a partir do catalogo de idiomas.
    /// </summary>
    private void SetHeaders()
    {
        void Header(GridView grid, int index, string key)
        {
            if (index < grid.Columns.Count) grid.Columns[index].Header = Loc.Get(key);
        }

        if (PermissionsList.View is GridView permissions)
        {
            Header(permissions, 0, "Mgmt.ColumnType");
            Header(permissions, 1, "Mgmt.ColumnName");
            Header(permissions, 2, "Mgmt.ColumnDetail");
        }

        if (ServicesList.View is GridView services)
        {
            Header(services, 0, "Mgmt.ColumnName");
            Header(services, 1, "Mgmt.ColumnState");
            Header(services, 2, "Mgmt.ColumnStartMode");
            Header(services, 3, "Mgmt.ColumnDetail");
        }

        if (TasksList.View is GridView tasks)
        {
            Header(tasks, 0, "Mgmt.ColumnName");
            Header(tasks, 1, "Mgmt.ColumnState");
            Header(tasks, 2, "Mgmt.ColumnDetail");
        }

        if (DriversList.View is GridView drivers)
        {
            Header(drivers, 0, "Mgmt.ColumnPublished");
            Header(drivers, 1, "Mgmt.ColumnProvider");
            Header(drivers, 2, "Mgmt.ColumnClass");
            Header(drivers, 3, "Mgmt.ColumnVersion");
            Header(drivers, 4, "Mgmt.ColumnSigner");
        }

        if (HealthList.View is GridView health)
        {
            Header(health, 0, "Mgmt.ColumnCategory");
            Header(health, 1, "Mgmt.ColumnSeverity");
            Header(health, 2, "Mgmt.ColumnFinding");
            Header(health, 3, "Mgmt.ColumnDetail");
        }
    }

    private void LoadAll()
    {
        StatusText.Text = Loc.Get("Status.Scanning");

        if (_programs.Count == 0)
        {
            var programs = ProgramScanner.Scan(new ProgramScanner.ScanOptions
            {
                IncludeSystemComponents = false,
                IncludeMsiProducts = false,
                ComputeFolderSize = false,
            });

            foreach (var program in programs.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                _programs.Add(program);
        }

        _services.Clear();
        foreach (var service in ServiceManager.List()) _services.Add(service);

        _tasks.Clear();
        foreach (var task in ScheduledTaskManager.List()) _tasks.Add(task);

        _drivers.Clear();
        foreach (var driver in DriverManager.List()) _drivers.Add(driver);

        StatusText.Text = Loc.Get("Mgmt.Loaded");
    }

    private void LoadPermissions()
    {
        _permissions.Clear();

        if (ProgramBox.SelectedItem is not InstalledProgram program)
        {
            UpdatePermissionButtons();
            return;
        }

        foreach (var entry in AppPermissionManager.For(program))
        {
            _permissions.Add(new PermissionRow
            {
                Entry = entry,
                KindText = KindText(entry.Kind),
                Name = entry.Name,
                Detail = entry.Detail,
                StateText = entry.Enabled ? Loc.Get("Mgmt.StateActive") : Loc.Get("Mgmt.StateInactive"),
            });
        }

        UpdatePermissionButtons();
    }

    private void UpdatePermissionButtons()
    {
        var selected = PermissionsList.SelectedItem as PermissionRow;

        EnableButton.IsEnabled = selected is not null && !selected.Entry.Enabled;
        DisableButton.IsEnabled = selected is not null && selected.Entry.CanDisable;
        RemovePermissionButton.IsEnabled = selected is not null && selected.Entry.CanRemove;
    }

    private static string KindText(AppPermissionKind kind) => kind switch
    {
        AppPermissionKind.Service => "Serviço",
        AppPermissionKind.Autorun => "Inicialização",
        AppPermissionKind.ContextMenu => "Menu de contexto",
        AppPermissionKind.ScheduledTask => "Tarefa agendada",
        _ => kind.ToString(),
    };

    private static string SeverityText(HealthSeverity severity) => severity switch
    {
        HealthSeverity.Info => "Informativo",
        HealthSeverity.Warning => "Atenção",
        HealthSeverity.Critical => "Crítico",
        _ => severity.ToString(),
    };

    private static string CategoryText(HealthCategory category) => category switch
    {
        HealthCategory.Leftovers => "Sobras",
        HealthCategory.RedundantFiles => "Arquivos repetidos",
        HealthCategory.Permissions => "Permissões",
        HealthCategory.Notifications => "Avisos",
        _ => category.ToString(),
    };

    private void PermissionsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdatePermissionButtons();

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        LoadAll();
        LoadPermissions();
    }

    private void EnableButton_Click(object sender, RoutedEventArgs e)
    {
        if (PermissionsList.SelectedItem is not PermissionRow row) return;

        var result = AppPermissionManager.Enable(row.Entry);
        StatusText.Text = result.Message;
        LoadPermissions();
    }

    private void DisableButton_Click(object sender, RoutedEventArgs e)
    {
        if (PermissionsList.SelectedItem is not PermissionRow row) return;

        var result = AppPermissionManager.Disable(row.Entry);
        StatusText.Text = result.Message;
        LoadPermissions();
    }

    private void RemovePermissionButton_Click(object sender, RoutedEventArgs e)
    {
        if (PermissionsList.SelectedItem is not PermissionRow row) return;

        if (!UserMessages.Confirm(Owner(), Loc.Get("Mgmt.ConfirmRemove", row.Entry.Name)))
            return;

        var result = AppPermissionManager.Remove(row.Entry);
        StatusText.Text = result.Message;
        LoadPermissions();
    }

    private void DisableServiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesList.SelectedItem is not WindowsService service) return;

        var result = ServiceManager.Disable(service);
        StatusText.Text = result.Message;
        LoadAll();
    }

    private void RemoveServiceButton_Click(object sender, RoutedEventArgs e)
    {
        if (ServicesList.SelectedItem is not WindowsService service) return;

        if (!UserMessages.Confirm(Owner(), Loc.Get("Mgmt.ConfirmRemove", service.DisplayName)))
            return;

        var result = ServiceManager.Delete(service);
        StatusText.Text = result.Message;
        LoadAll();
    }

    private void DisableTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TasksList.SelectedItem is not ScheduledTask task) return;

        var result = ScheduledTaskManager.Disable(task);
        StatusText.Text = result.Message;
        LoadAll();
    }

    private void RemoveTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TasksList.SelectedItem is not ScheduledTask task) return;

        if (!UserMessages.Confirm(Owner(), Loc.Get("Mgmt.ConfirmRemove", task.Name)))
            return;

        var result = ScheduledTaskManager.Delete(task);
        StatusText.Text = result.Message;
        LoadAll();
    }

    private void RemoveDriverButton_Click(object sender, RoutedEventArgs e)
    {
        if (DriversList.SelectedItem is not DriverPackage driver) return;

        if (!UserMessages.Confirm(Owner(), Loc.Get("Mgmt.ConfirmRemoveDriver", driver.PublishedName)))
            return;

        var result = DriverManager.Delete(driver);
        StatusText.Text = result.Message;
        LoadAll();
    }

    private void AnalyzeHealthButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProgramBox.SelectedItem is not InstalledProgram program)
        {
            StatusText.Text = Loc.Get("Mgmt.ChooseProgram");
            return;
        }

        StatusText.Text = Loc.Get("Status.Scanning");

        var report = SoftwareHealth.Analyze(new[] { program });

        _health.Clear();
        foreach (var finding in report.Findings)
        {
            _health.Add(new HealthRow
            {
                Finding = finding,
                CategoryText = CategoryText(finding.Category),
                SeverityText = SeverityText(finding.Severity),
                Title = finding.Title,
                Detail = finding.Detail,
            });
        }

        StatusText.Text = Loc.Get("Mgmt.HealthFound", _health.Count);
    }

    /// <summary>
    /// A janela que hospeda o painel.
    ///
    /// As confirmacoes sao caixas de dialogo do proprio Windows, e elas
    /// precisam de um dono — sem ele a caixa aparece atras da janela principal
    /// e o usuario nao ve qual pergunta esta esperando.
    /// </summary>
    private Window? Owner() => Window.GetWindow(this);
}
