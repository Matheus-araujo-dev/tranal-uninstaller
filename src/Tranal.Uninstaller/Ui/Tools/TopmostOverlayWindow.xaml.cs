using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Localization;
using Tranal.Uninstaller.Core.Modules;
using Tranal.Uninstaller.Core.Topmost;
using Tranal.Uninstaller.Core.Tools;

namespace Tranal.Uninstaller.Ui.Tools;

/// <summary>
/// Tamanhos disponiveis para a janela principal flutuante
/// (equivalentes a "Small", "Middle" e "Large" do Revo).
/// </summary>
public enum TopmostOverlaySize
{
    /// <summary>Tamanho pequeno.</summary>
    Small,

    /// <summary>Tamanho medio (padrao).</summary>
    Middle,

    /// <summary>Tamanho grande.</summary>
    Large,
}

/// <summary>
/// Janela principal flutuante do Tranal: pequena, sem moldura, sempre no topo
/// (configuravel) e transparente (configuravel).
///
/// O usuario arrasta um executavel, um atalho, um icone ou uma janela do
/// Windows sobre ela; o alvo e resolvido por
/// <see cref="TopmostTargetResolver"/> e as acoes validas aparecem conforme o
/// alvo (desinstalar so existe quando ha programa instalado, encerrar processo
/// so quando ha processo em execucao).
///
/// A janela tambem pode ser dirigida por codigo, sem interacao:
/// <see cref="SetTargetFromFile(string)"/> e <see cref="SetTargetFromHwnd(IntPtr)"/>.
/// </summary>
public partial class TopmostOverlayWindow : Window
{
    /// <summary>Largura da janela no tamanho pequeno.</summary>
    private const double SmallWidth = 250;

    /// <summary>Largura da janela no tamanho medio.</summary>
    private const double MiddleWidth = 330;

    /// <summary>Largura da janela no tamanho grande.</summary>
    private const double LargeWidth = 440;

    private TopmostTarget? _target;

    public TopmostOverlayWindow()
    {
        InitializeComponent();

        // Window.Title nao e DependencyProperty: o texto vai por codigo.
        Title = Loc.Get("Topmost.Title");

        SizeCombo.SelectedIndex = 1; // medio
        ApplySize(TopmostOverlaySize.Middle);

        StatusText.Text = Loc.Get("Topmost.Hint");
        UpdateButtons(Array.Empty<TopmostAction>());
    }

    /// <summary>Alvo atualmente resolvido (null quando nada foi resolvido).</summary>
    public TopmostTarget? CurrentTarget => _target;

    /// <summary>
    /// Resolve o alvo pelo caminho de um arquivo (executavel ou atalho) e
    /// atualiza a janela.
    /// </summary>
    public void SetTargetFromFile(string path)
    {
        ApplyTarget(TopmostTargetResolver.ResolveFromFile(path));
    }

    /// <summary>
    /// Resolve o alvo a partir dos arquivos arrastados sobre a janela. Mais de
    /// um arquivo e uma pasta sao recusados com a mensagem correspondente.
    /// </summary>
    public void SetTargetFromFiles(IReadOnlyList<string> paths)
    {
        ApplyTarget(TopmostTargetResolver.ResolveFromFiles(paths));
    }

    /// <summary>
    /// Resolve o alvo pela janela do Windows (HWND) e atualiza a janela.
    /// </summary>
    public void SetTargetFromHwnd(IntPtr hwnd)
    {
        ApplyTarget(TopmostTargetResolver.ResolveFromHwnd(hwnd));
    }

    // ---------------------------------------------------------------------
    // Aplicacao do alvo
    // ---------------------------------------------------------------------

    private void ApplyTarget(TopmostTarget target)
    {
        _target = target;

        if (target is null || !target.Resolved)
        {
            TargetIcon.Source = null;
            TargetNameText.Text = target?.Message is { Length: > 0 } message
                ? message
                : Loc.Get("Topmost.Unresolved");
            TargetPathText.Text = string.Empty;
            StatusText.Text = Loc.Get("Topmost.Hint");
            UpdateButtons(Array.Empty<TopmostAction>());
            return;
        }

        TargetNameText.Text = target.DisplayName;
        TargetPathText.Text = target.ExecutablePath;
        TargetIcon.Source = LoadIcon(target.IconPath);

        var actions = TopmostTargetResolver.AvailableActions(target);
        UpdateButtons(actions);

        StatusText.Text = target.Program is not null
            ? $"{target.Program.DisplayName} ({target.Program.VersionOrNoVersion})"
            : Loc.Get("Topmost.NoPackage");
    }

    private void UpdateButtons(IReadOnlyList<TopmostAction> actions)
    {
        UninstallButton.Visibility = Visible(TopmostAction.Uninstall, actions);
        StopAutoStartButton.Visibility = Visible(TopmostAction.StopAutoStart, actions);
        KillProcessButton.Visibility = Visible(TopmostAction.KillProcess, actions);
        KillAndDeleteButton.Visibility = Visible(TopmostAction.KillAndDelete, actions);
        OpenFolderButton.Visibility = Visible(TopmostAction.OpenFolder, actions);
        PropertiesButton.Visibility = Visible(TopmostAction.Properties, actions);
        InfoButton.Visibility = Visible(TopmostAction.Info, actions);
        HunterModeButton.Visibility = Visible(TopmostAction.Info, actions);
    }

    private static Visibility Visible(TopmostAction action, IReadOnlyList<TopmostAction> actions) =>
        actions.Contains(action) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Carrega o icone do alvo (silenciosamente, quando nao houver).</summary>
    private static ImageSource? LoadIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            if (!File.Exists(path)) return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // Opcoes da janela
    // ---------------------------------------------------------------------

    private void SizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SizeCombo.SelectedItem is not ComboBoxItem item) return;

        ApplySize((item.Tag as string) switch
        {
            "Small" => TopmostOverlaySize.Small,
            "Large" => TopmostOverlaySize.Large,
            _ => TopmostOverlaySize.Middle,
        });
    }

    /// <summary>Aplica o tamanho escolhido (largura e corpo do texto).</summary>
    private void ApplySize(TopmostOverlaySize size)
    {
        Width = size switch
        {
            TopmostOverlaySize.Small => SmallWidth,
            TopmostOverlaySize.Large => LargeWidth,
            _ => MiddleWidth,
        };

        FontSize = size switch
        {
            TopmostOverlaySize.Small => 10,
            TopmostOverlaySize.Large => 12,
            _ => 11,
        };
    }

    private void AlwaysOnTopCheck_Changed(object sender, RoutedEventArgs e)
    {
        Topmost = AlwaysOnTopCheck.IsChecked == true;
    }

    private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        try { Opacity = Math.Clamp(e.NewValue, 0.3, 1.0); }
        catch { /* valor fora da faixa: ignorado */ }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // O botao de fechar fica no cabecalho: ele nao arrasta a janela.
        if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) is not null) return;

        try { DragMove(); }
        catch { /* arraste invalido */ }
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T typed) return typed;
            element = System.Windows.Media.VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    // ---------------------------------------------------------------------
    // Acoes
    // ---------------------------------------------------------------------

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();

    private void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target?.Program is null)
        {
            ShowMessage(Loc.Get("Topmost.NoPackage"));
            StatusText.Text = Loc.Get("Topmost.NoPackage");
            return;
        }

        var main = MainWindowInstance();
        if (main is null)
        {
            ShowMessage(Loc.Get("Topmost.CannotRunUninstall"));
            StatusText.Text = Loc.Get("Topmost.CannotRunUninstall");
            return;
        }

        main.Show();
        main.Activate();
        main.StartUninstallFor(_target.Program);
    }

    private void StopAutoStartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target?.Program is null)
        {
            StatusText.Text = Loc.Get("Topmost.NoPackage");
            return;
        }

        var items = TopmostTargetResolver.FindAutoStartItems(_target.Program);
        if (items.Count == 0)
        {
            StatusText.Text = Loc.Get("Topmost.NotInStartup");
            return;
        }

        var disabled = 0;
        foreach (var item in items)
        {
            try
            {
                if (AutorunManager.SetEnabled(item, enabled: false)) disabled++;
            }
            catch (Exception ex)
            {
                ToolLog.Error($"Falha ao desabilitar a inicializacao automatica de '{item.Name}': {ex.Message}");
            }
        }

        StatusText.Text = disabled > 0
            ? Loc.Get("Topmost.AutoStartDisabled")
            : Loc.Get("Topmost.NotInStartup");
    }

    private void KillProcessButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null || _target.ProcessId <= 0)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        try
        {
            using var process = Process.GetProcessById(_target.ProcessId);
            process.Kill();
            StatusText.Text = $"Processo encerrado: {_target.ProcessName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void KillAndDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null || _target.ProcessId <= 0)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        try
        {
            using (var process = Process.GetProcessById(_target.ProcessId))
            {
                process.Kill();
            }
        }
        catch (Exception ex)
        {
            ToolLog.Error($"Falha ao encerrar o processo {_target.ProcessId}: {ex.Message}");
        }

        // A remocao do executavel passa pelo motor canonico (regras de seguranca,
        // Lixeira e backup antes da exclusao).
        var items = new List<LeftoverItem>
        {
            new()
            {
                Kind = "Arquivo",
                Path = _target.ExecutablePath,
                Detail = "Encerrar e excluir processo",
                Selected = true,
                Confidence = LeftoverConfidence.High,
                Reason = LeftoverReason.ProcessTermination,
            },
        };

        var report = UninstallEngine.RemoveLeftovers(items);
        StatusText.Text = report.RemovedCount > 0
            ? $"Arquivo removido: {_target.ExecutablePath}"
            : $"O arquivo nao pode ser removido: {_target.ExecutablePath}";
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _target?.ExecutablePath ?? string.Empty;
        if (path.Length == 0)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true,
            };
            using var process = Process.Start(startInfo);
            if (process is null) StatusText.Text = Loc.Get("Topmost.OpenFolder");
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void PropertiesButton_Click(object sender, RoutedEventArgs e)
    {
        var path = _target?.ExecutablePath ?? string.Empty;
        if (path.Length == 0)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "properties",
            };
            using var process = Process.Start(startInfo);
            if (process is not null) return;
        }
        catch
        {
            // O verb "properties" nao existe em todos os sistemas: mostra as
            // propriedades do arquivo em uma mensagem.
        }

        ShowMessage(DescribeFileProperties(path));
    }

    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null || !_target.Resolved)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        ShowMessage(DescribeTarget(_target));
    }

    private void HunterModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_target is null || !_target.Resolved)
        {
            StatusText.Text = Loc.Get("Topmost.Unresolved");
            return;
        }

        // O Modo Cacador do Tranal e o proprio resoletor de alvo: reaproveita a
        // identificacao canonica para confirmar o programa do alvo.
        try
        {
            var resolution = HunterMode.Resolve(_target.ExecutablePath);
            StatusText.Text = resolution.Found
                ? $"{resolution.DisplayName} — {resolution.Message}"
                : resolution.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void OpenMainWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var main = MainWindowInstance();
        if (main is null)
        {
            StatusText.Text = Loc.Get("Topmost.OpenMainWindow");
            return;
        }

        main.Show();
        main.Activate();
    }

    // ---------------------------------------------------------------------
    // Auxiliares
    // ---------------------------------------------------------------------

    /// <summary>Janela principal do aplicativo (quando existe).</summary>
    private static MainWindow? MainWindowInstance() => Application.Current?.MainWindow as MainWindow;

    private void ShowMessage(string text)
    {
        try
        {
            UserMessages.Information(this, text, Loc.Get("Topmost.Title"));
        }
        catch
        {
            // Sem janela para receber a mensagem: o status ja foi atualizado.
        }
    }

    /// <summary>Descricao textual do alvo resolvido.</summary>
    private static string DescribeTarget(TopmostTarget target)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(target.DisplayName);

        if (target.Program is not null)
        {
            sb.AppendLine($"Programa: {target.Program.DisplayName} ({target.Program.VersionOrNoVersion})");
            if (target.Program.Publisher.Length > 0) sb.AppendLine($"Editora: {target.Program.Publisher}");
            if (target.Program.InstallLocation.Length > 0) sb.AppendLine($"Local: {target.Program.InstallLocation}");
        }

        if (target.ProcessId > 0) sb.AppendLine($"Processo: {target.ProcessName} (PID {target.ProcessId})");
        if (target.ExecutablePath.Length > 0) sb.AppendLine($"Executavel: {target.ExecutablePath}");

        return sb.ToString().TrimEnd();
    }

    /// <summary>Propriedades do arquivo (usado quando o shell nao abre a dialogo).</summary>
    private static string DescribeFileProperties(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(info.FullName);
            sb.AppendLine($"Tamanho: {info.Length} bytes");
            sb.AppendLine($"Criado em: {info.CreationTime:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Modificado em: {info.LastWriteTime:dd/MM/yyyy HH:mm}");
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    // ---------------------------------------------------------------------
    // Arrastar e soltar
    // ---------------------------------------------------------------------

    private void Overlay_Drop(object sender, DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
            if (paths.Length == 0) return;

            SetTargetFromFiles(paths);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Overlay_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }
        catch
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }
}
