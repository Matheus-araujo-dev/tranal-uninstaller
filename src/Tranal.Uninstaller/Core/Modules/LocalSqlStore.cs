using System.IO;
using System.Runtime.InteropServices;

namespace Tranal.Uninstaller.Core.Modules;

/// <summary>
/// O banco local embutido do Tranal.
///
/// O Windows traz o motor do SQLite na propria maquina: a biblioteca
/// <c>winsqlite3.dll</c>, em <c>System32</c>. E o mesmo motor do SQLite —
/// a API e a API — e nao precisa de instalacao nenhuma.
///
/// E nele que o Tranal guarda o log das desinstalacoes: um log que cresce
/// durante meses precisa de consulta, e um arquivo de texto nao da conta.
///
/// <para>A regra esta em um lugar so: este modulo e o unico que fala com o
/// banco. Quem guarda log chama <see cref="Append"/>, quem consulta chama
/// <see cref="Query"/>.</para>
/// </summary>
public static class LocalSqlStore
{
    private const int SQLITE_OK = 0;
    private const int SQLITE_ROW = 100;
    private const int SQLITE_DONE = 101;

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_open_v2(string filename, out IntPtr db, int flags, IntPtr vfs);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_close(IntPtr db);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_exec(IntPtr db, string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_errmsg(IntPtr db);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_prepare_v2(IntPtr db, string sql, int nByte, out IntPtr stmt, out IntPtr tail);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_step(IntPtr stmt);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr sqlite3_column_text(IntPtr stmt, int column);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_column_count(IntPtr stmt);

    [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_finalize(IntPtr stmt);

    /// <summary>
    /// Substitui a pasta de dados (so a suite de testes). Nulo em uso normal.
    /// </summary>
    internal static string? FolderOverride { get; set; }

    /// <summary>A pasta onde fica o banco.</summary>
    public static string Folder =>
        FolderOverride ?? Path.Combine(RegistryBackup.DataDirectory, "db");

    /// <summary>O arquivo do banco.</summary>
    public static string DatabasePath => Path.Combine(Folder, "tranal.db");

    /// <summary>
    /// A biblioteca do SQLite do Windows existe nesta maquina?
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                return File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32", "winsqlite3.dll"));
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Abre o banco (criando a pasta e o arquivo quando preciso).
    /// </summary>
    /// <param name="db">A handle do banco.</param>
    /// <returns>Verdadeiro quando o banco abriu.</returns>
    public static bool TryOpen(out IntPtr db)
    {
        db = IntPtr.Zero;

        try
        {
            if (!IsAvailable) return false;

            Directory.CreateDirectory(Folder);

            // O flag 6 = OPEN_READWRITE | OPEN_CREATE.
            if (sqlite3_open_v2(DatabasePath, out db, 6, IntPtr.Zero) != SQLITE_OK)
            {
                if (db != IntPtr.Zero) sqlite3_close(db);
                db = IntPtr.Zero;
                return false;
            }

            return true;
        }
        catch
        {
            db = IntPtr.Zero;
            return false;
        }
    }

    /// <summary>
    /// Executa um comando (sem retorno de linhas).
    /// </summary>
    /// <param name="sql">O comando.</param>
    /// <returns>Verdadeiro quando o comando rodou.</returns>
    public static bool Execute(string sql)
    {
        if (!TryOpen(out var db)) return false;

        try
        {
            return sqlite3_exec(db, sql, IntPtr.Zero, IntPtr.Zero, out _) == SQLITE_OK;
        }
        finally
        {
            sqlite3_close(db);
        }
    }

    /// <summary>
    /// Executa um comando que devolve linhas, e devolve cada linha como uma
    /// lista de colunas.
    /// </summary>
    /// <param name="sql">O comando.</param>
    public static IReadOnlyList<IReadOnlyList<string>> Query(string sql)
    {
        var linhas = new List<IReadOnlyList<string>>();

        if (!TryOpen(out var db)) return linhas;

        try
        {
            if (sqlite3_prepare_v2(db, sql, -1, out var stmt, out _) != SQLITE_OK) return linhas;

            try
            {
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    var linha = new List<string>();
                    var colunas = sqlite3_column_count(stmt);

                    for (var i = 0; i < colunas; i++)
                    {
                        var texto = sqlite3_column_text(stmt, i);
                        linha.Add(texto == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(texto) ?? string.Empty);
                    }

                    linhas.Add(linha);
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }
        finally
        {
            sqlite3_close(db);
        }

        return linhas;
    }

    /// <summary>
    /// Cria a tabela do log de remocoes, se ela ainda nao existe.
    /// </summary>
    public static bool EnsureSchema() => Execute(
        "CREATE TABLE IF NOT EXISTS remocoes ("
        + "id INTEGER PRIMARY KEY AUTOINCREMENT, "
        + "criado_em TEXT NOT NULL, "
        + "programa TEXT NOT NULL, "
        + "modo TEXT, "
        + "resultado TEXT, "
        + "detalhe TEXT);");

    /// <summary>
    /// Acrescenta uma linha ao log.
    /// </summary>
    /// <param name="programName">O programa removido.</param>
    /// <param name="mode">O modo da remocao.</param>
    /// <param name="result">O resultado.</param>
    /// <param name="detail">O detalhe.</param>
    public static bool Append(string programName, string mode, string result, string detail = "")
    {
        if (string.IsNullOrWhiteSpace(programName)) return false;
        if (!EnsureSchema()) return false;

        var sql = "INSERT INTO remocoes (criado_em, programa, modo, resultado, detalhe) VALUES ('"
            + Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "','"
            + Escape(programName) + "','"
            + Escape(mode) + "','"
            + Escape(result) + "','"
            + Escape(detail) + "');";

        return Execute(sql);
    }

    /// <summary>
    /// As ultimas remocoes registradas.
    /// </summary>
    /// <param name="limit">Quantas linhas.</param>
    public static IReadOnlyList<IReadOnlyList<string>> Recent(int limit = 20)
    {
        if (limit <= 0) limit = 20;

        return Query("SELECT criado_em, programa, modo, resultado FROM remocoes ORDER BY id DESC LIMIT " + limit + ";");
    }

    private static string Escape(string? value) => (value ?? string.Empty).Replace("'", "''");
}
