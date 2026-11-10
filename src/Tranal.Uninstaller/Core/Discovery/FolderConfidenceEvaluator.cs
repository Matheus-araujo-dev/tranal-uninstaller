using System;
using System.Collections.Generic;
using System.IO;
using Tranal.Uninstaller.Core;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O nivel de confianca de uma pasta: o quanto o Tranal acredita que a pasta
/// e sobra do programa, e nao conteudo do usuario.
/// </summary>
public enum FolderConfidenceLevel
{
    /// <summary>Alta: os criterios apontam claramente para sobra de programa.</summary>
    High,

    /// <summary>Media: a pasta e de programa, mas com sinais fracos ou mistos.</summary>
    Medium,

    /// <summary>
    /// Baixa: a pasta pode conter dado do usuario, ou nenhum criterio
    /// sustenta a ligacao com o programa.
    /// </summary>
    Low,
}

/// <summary>
/// Um criterio avaliado na pasta, com a explicacao em texto.
///
/// O criterio e o que torna a confianca auditavel: um "baixa confianca" sem
/// explicacao nao diz nada ao usuario.
/// </summary>
/// <param name="Id">O identificador estavel do criterio (para traducao).</param>
/// <param name="Description">A explicacao do que foi observado na pasta.</param>
/// <param name="Positive">True quando o criterio fortalece a confianca.</param>
public sealed record FolderConfidenceCriterion(string Id, string Description, bool Positive);

/// <summary>
/// O resultado da avaliacao de uma pasta: o nivel, os criterios e os sinais
/// que a tela precisa mostrar.
/// </summary>
public sealed class FolderConfidenceResult
{
    /// <summary>A pasta avaliada, canonica.</summary>
    public string FolderPath { get; init; } = string.Empty;

    /// <summary>O nivel de confianca da pasta.</summary>
    public FolderConfidenceLevel Level { get; init; } = FolderConfidenceLevel.Low;

    /// <summary>Os criterios avaliados, na ordem fixa.</summary>
    public IReadOnlyList<FolderConfidenceCriterion> Criteria { get; init; } =
        Array.Empty<FolderConfidenceCriterion>();

    /// <summary>
    /// True quando a pasta pode conter documentos ou dados do usuario.
    /// E o sinal do aviso "as pastas abaixo podem conter dados do usuario".
    /// </summary>
    public bool MayContainUserData { get; init; }

    /// <summary>True quando a pasta contem executaveis.</summary>
    public bool HasExecutables { get; init; }

    /// <summary>True quando a pasta esta vazia.</summary>
    public bool IsEmpty { get; init; }

    /// <summary>
    /// True quando a pasta existe e pode ser inspecionada. Quando e false, a
    /// confianca pelo nome continua valendo: a pasta nao esta mais la.
    /// </summary>
    public bool Usable { get; init; } = true;

    /// <summary>A soma dos pontos dos criterios (o numero que da o nivel).</summary>
    public int Score { get; init; }

    /// <summary>A explicacao em texto, Criteria unido.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>As explicacoes dos criterios, em lista.</summary>
    public IReadOnlyList<string> Descriptions =>
        Criteria.Select(c => c.Description).ToList();
}

/// <summary>
/// Avalia a confianca de uma pasta por criterios reais.
///
/// A confianca de pasta nao vem so do nome: uma pasta vazia, uma pasta com
/// executaveis e uma pasta dentro de "Program Files" dizem coisas diferentes
/// de uma pasta cheia de documentos do usuario. O Bulk Crap Uninstaller usa
/// exatamente esta ideia — cada nivel vem com o criterio que o produziu — e
/// e ela que o Tranal adota.
///
/// A regra mora aqui, em um lugar so: quem mostra a confianca da pasta
/// (assistente, relatorio, CLI) consome esta avaliacao e nunca calcula a
/// sua.
/// </summary>
public static class FolderConfidenceEvaluator
{
    /// <summary>Criterio: a pasta esta vazia.</summary>
    public const string EmptyFolderCriterion = "pasta-vazia";

    /// <summary>Criterio: a pasta contem executaveis.</summary>
    public const string ExecutablesCriterion = "executaveis";

    /// <summary>Criterio: a pasta contem documentos/dados do usuario.</summary>
    public const string UserDataCriterion = "dados-do-usuario";

    /// <summary>Criterio: o editor do programa e reconhecido.</summary>
    public const string KnownEditorCriterion = "editor-reconhecido";

    /// <summary>Criterio: o nome da pasta coincide com o nome do programa.</summary>
    public const string NameMatchCriterion = "nome-do-programa";

    /// <summary>Criterio: a pasta esta dentro de uma pasta de programas.</summary>
    public const string ProgramsFolderCriterion = "pasta-de-programas";

    /// <summary>Criterio: a pasta esta dentro da pasta do usuario.</summary>
    public const string UserFolderCriterion = "pasta-do-usuario";

    /// <summary>Criterio: a pasta tem muitos arquivos.</summary>
    public const string ManyFilesCriterion = "muitos-arquivos";

    /// <summary>Criterio: a pasta nao tem subdiretorios.</summary>
    public const string NoSubdirectoriesCriterion = "sem-subdiretorios";

    /// <summary>Quantos arquivos a pasta precisa ter para contar como "muitos".</summary>
    public const int ManyFilesThreshold = 50;

    /// <summary>Quantos itens do primeiro nivel sao inspecionados no maximo.</summary>
    private const int MaxEntries = 400;

    /// <summary>Extensao de executavel (bibliotecas e drivers contam).</summary>
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".ocx", ".cpl", ".drv", ".scr", ".msi", ".ax",
    };

    /// <summary>
    /// Extensao de documento do usuario: o que a pessoa cria, e nao o que o
    /// programa instala. Uma pasta so com isto e conteudo dela, e nao cache.
    /// </summary>
    private static readonly HashSet<string> UserDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc", ".docx", ".odt", ".rtf", ".txt", ".md", ".pdf", ".xls", ".xlsx",
        ".ods", ".csv", ".ppt", ".pptx", ".odp", ".jpg", ".jpeg", ".png", ".gif",
        ".bmp", ".webp", ".svg", ".psd", ".mp3", ".wav", ".flac", ".mp4", ".mkv",
        ".avi", ".mov", ".zip", ".7z", ".rar", ".mobi", ".epub",
    };

    /// <summary>
    /// Editores reconhecidos. Uma pasta de um editor conhecido e de programa
    /// com muito mais certeza do que uma pasta com nome qualquer.
    /// </summary>
    private static readonly string[] KnownEditors =
    {
        "Microsoft", "Adobe", "Google", "Mozilla", "Valve", "Oracle", "IBM",
        "JetBrains", "Eclipse", "Node.js", "Python Software Foundation",
        "Nvidia", "AMD", "Intel", "Samsung", "Sony", "Ubisoft", "Electronic Arts",
        "Epic Games", "Discord", "Spotify", "Dropbox", "TeamViewer", "VMware",
        "Docker", "Avast", "AVG", "Kaspersky", "McAfee", "Norton", "WinZip",
        "7-Zip", "Notepad++", "Sublime", "Unity", "Autodesk", "Blender",
        "CyberLink", "Corel", "Tencent", "ByteDance", "Oculus", "Meta",
    };

    /// <summary>
    /// Avalia a pasta e devolve o nivel de confianca com os criterios.
    ///
    /// A avaliacao nunca lanca: uma pasta inacessivel devolve um resultado
    /// sem informacao (<see cref="FolderConfidenceResult.Usable"/> falso), e
    /// quem chama mantem a confianca que ja tinha.
    /// </summary>
    /// <param name="folderPath">A pasta a avaliar.</param>
    /// <param name="programName">O nome do programa (para comparar com o nome da pasta).</param>
    /// <param name="publisher">O editor do programa (para reconhecer o editor).</param>
    public static FolderConfidenceResult Evaluate(
        string? folderPath, string? programName = null, string? publisher = null)
    {
        var full = PathUtil.Normalize(folderPath);

        if (full.Length == 0)
        {
            return new FolderConfidenceResult
            {
                FolderPath = string.Empty,
                Level = FolderConfidenceLevel.Low,
                Usable = false,
                Summary = "Pasta não informada.",
            };
        }

        var criteria = new List<FolderConfidenceCriterion>();
        var score = 0;

        var content = Inspect(full);
        if (!content.Accessible)
        {
            return new FolderConfidenceResult
            {
                FolderPath = full,
                Level = FolderConfidenceLevel.Low,
                Usable = false,
                Summary = "A pasta não pôde ser inspecionada.",
            };
        }

        // 1) Pasta vazia: sobra de programa quase sempre e uma pasta vazia.
        criteria.Add(new FolderConfidenceCriterion(
            EmptyFolderCriterion,
            content.EntryCount == 0 ? "A pasta está vazia." : "A pasta não está vazia.",
            content.EntryCount == 0));

        if (content.EntryCount == 0) score += 2;

        // 2) Executaveis: o conteudo de um programa instalado.
        criteria.Add(new FolderConfidenceCriterion(
            ExecutablesCriterion,
            content.HasExecutables ? "A pasta contém executáveis." : "A pasta não contém executáveis.",
            content.HasExecutables));

        if (content.HasExecutables) score += 2;

        // 3) Dados do usuario: o sinal que baixa a confianca e aciona o aviso.
        var userDataText = content.HasUserDocuments
            ? (content.HasExecutables
                ? "A pasta contém documentos do usuário junto com executáveis."
                : "A pasta contém apenas documentos e dados do usuário.")
            : "A pasta não contém documentos do usuário.";

        criteria.Add(new FolderConfidenceCriterion(
            UserDataCriterion, userDataText, !content.HasUserDocuments));

        if (content.HasUserDocuments) score -= content.HasExecutables ? 2 : 3;

        // 4) Editor reconhecido.
        var editorKnown = HasKnownEditor(full, publisher);
        criteria.Add(new FolderConfidenceCriterion(
            KnownEditorCriterion,
            editorKnown ? "O editor do programa é reconhecido." : "O editor do programa não é reconhecido.",
            editorKnown));

        if (editorKnown) score += 1;

        // 5) O nome da pasta coincide com o nome do programa.
        var nameMatches = NameMatches(full, programName);
        criteria.Add(new FolderConfidenceCriterion(
            NameMatchCriterion,
            nameMatches
                ? "O nome da pasta coincide com o nome do programa."
                : "O nome da pasta não coincide com o nome do programa.",
            nameMatches));

        if (nameMatches) score += 2;

        // 6) Dentro de uma pasta de programas do sistema.
        var inProgramsFolder = IsUnderProgramsFolder(full);
        criteria.Add(new FolderConfidenceCriterion(
            ProgramsFolderCriterion,
            inProgramsFolder
                ? "A pasta está dentro de uma pasta de programas do sistema."
                : "A pasta não está dentro de uma pasta de programas do sistema.",
            inProgramsFolder));

        if (inProgramsFolder) score += 1;

        // 7) Dentro da pasta do usuario: onde o programa guarda dado, e onde
        //    o documento do usuario tambem mora.
        var inUserFolder = IsUnderUserFolder(full);
        criteria.Add(new FolderConfidenceCriterion(
            UserFolderCriterion,
            inUserFolder
                ? "A pasta está dentro da pasta do usuário."
                : "A pasta não está dentro da pasta do usuário.",
            !inUserFolder));

        if (inUserFolder) score -= 1;

        // 8) Muitos arquivos: a pasta de um programa instalado tem dezenas de
        //    arquivos, e uma pasta de documento do usuario nao tem.
        var manyFiles = content.FileCount >= ManyFilesThreshold;
        criteria.Add(new FolderConfidenceCriterion(
            ManyFilesCriterion,
            manyFiles
                ? "A pasta tem muitos arquivos."
                : "A pasta não tem muitos arquivos.",
            manyFiles));

        if (manyFiles) score += 1;

        // 9) Sem subdiretorios: a pasta e plana, e nao o albamaro de um
        //    programa que guarda varias coisas dentro dela.
        var noSubdirectories = !content.HasSubdirectories;
        criteria.Add(new FolderConfidenceCriterion(
            NoSubdirectoriesCriterion,
            noSubdirectories
                ? "A pasta não tem subdiretórios."
                : "A pasta tem subdiretórios.",
            noSubdirectories));

        if (noSubdirectories) score += 1;

        var level = score >= 3 ? FolderConfidenceLevel.High
            : score >= 1 ? FolderConfidenceLevel.Medium
            : FolderConfidenceLevel.Low;

        var mayContainUserData = content.HasUserDocuments || IsUserContentFolder(full);

        return new FolderConfidenceResult
        {
            FolderPath = full,
            Level = level,
            Criteria = criteria,
            MayContainUserData = mayContainUserData,
            HasExecutables = content.HasExecutables,
            IsEmpty = content.EntryCount == 0,
            Usable = true,
            Score = score,
            Summary = string.Join(" ", criteria.Select(c => c.Description)),
        };
    }

    /// <summary>
    /// True quando a pasta e uma pasta de conteudo do usuario (Documentos,
    /// Area de Trabalho, Imagens...). Uma pasta assim nunca e cache de
    /// programa.
    /// </summary>
    /// <param name="folderPath">O caminho da pasta.</param>
    public static bool IsUserContentFolder(string? folderPath)
    {
        var full = PathUtil.Normalize(folderPath);
        if (full.Length == 0) return false;

        var parts = full.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;

        return UserContentFolders.Contains(parts[3]);
    }

    /// <summary>
    /// As pastas de conteudo do usuario. "AppData" NAO entra: ali mora dado
    /// de programa, que e justamente o que a desinstalacao deve apagar.
    /// </summary>
    private static readonly HashSet<string> UserContentFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Documents", "Desktop", "Pictures", "Music", "Videos", "Saved Games",
        "Favorites", "3D Objects", "Contacts", "Links", "Searches", "Downloads",
        "Documentos", "Area de Trabalho", "Imagens", "Musica", "Videos", "Downloads",
    };

    /// <summary>O que a leitura do primeiro nivel da pasta encontrou.</summary>
    private sealed record FolderContent(
        bool Accessible,
        int EntryCount,
        bool HasExecutables,
        bool HasUserDocuments,
        int FileCount,
        bool HasSubdirectories);

    /// <summary>Le o primeiro nivel da pasta, sem seguir link e sem estourar tempo.</summary>
    private static FolderContent Inspect(string folder)
    {
        var count = 0;
        var executables = false;
        var documents = false;
        var files = 0;
        var hasSubdirectories = false;

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                count++;

                var isDirectory = false;
                try { isDirectory = Directory.Exists(entry); }
                catch { /* item inacessivel: segue como arquivo */ }

                if (isDirectory)
                {
                    hasSubdirectories = true;
                }
                else
                {
                    files++;

                    var extension = Path.GetExtension(entry);
                    if (ExecutableExtensions.Contains(extension)) executables = true;
                    else if (UserDocumentExtensions.Contains(extension)) documents = true;
                }

                if (count >= MaxEntries) break;
            }
        }
        catch
        {
            return new FolderContent(false, count, executables, documents, files, hasSubdirectories);
        }

        return new FolderContent(true, count, executables, documents, files, hasSubdirectories);
    }

    /// <summary>
    /// True quando o nome da pasta coincide com o nome do programa: a pasta
    /// "Notepad++" e do programa "Notepad++".
    /// </summary>
    private static bool NameMatches(string folder, string? programName)
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;

        string leaf;
        try { leaf = Path.GetFileName(folder.TrimEnd('\\', '/')); }
        catch { return false; }

        if (leaf.Length == 0) return false;

        var name = programName.Trim();

        return NameMatch.StartsWithWord(leaf, name)
            || NameMatch.ContainsWord(leaf, name)
            || NameMatch.StartsWithWord(name, leaf);
    }

    /// <summary>True quando o editor do programa (ou da pasta) e conhecido.</summary>
    private static bool HasKnownEditor(string folder, string? publisher)
    {
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            foreach (var editor in KnownEditors)
            {
                if (publisher.Contains(editor, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        var parts = folder.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            foreach (var editor in KnownEditors)
            {
                if (part.Equals(editor, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    /// <summary>True quando a pasta esta dentro de "Program Files".</summary>
    private static bool IsUnderProgramsFolder(string folder) =>
        IsUnder(folder, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
        || IsUnder(folder, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

    /// <summary>True quando a pasta esta dentro do perfil do usuario.</summary>
    private static bool IsUnderUserFolder(string folder) =>
        IsUnder(folder, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
        || IsUnder(folder, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
        || IsUnder(folder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>True quando o caminho esta dentro da raiz informada.</summary>
    private static bool IsUnder(string folder, string root)
    {
        var full = PathUtil.Normalize(root);
        if (full.Length == 0) return false;

        return folder.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
