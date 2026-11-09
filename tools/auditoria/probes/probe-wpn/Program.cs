// Probe de auditoria — o que este programa mede esta no cabecalho.
// Ele NAO faz parte do Tranal: e a ferramenta que mede a Central de
// Acoes do Windows (IOBIT T7#34). A medicao e a que esta em
// docs/auditoria/EVIDENCIA-CENTRAL-ACoes.md.
//
using System.Runtime.InteropServices;

// Le o banco de notificacoes do Windows e mostra as tabelas e as
// notificacoes mais recentes. Serve para medir se um balao da bandeja
// (Shell_NotifyIcon com NIF_INFO) chega na Central de Acoes.

var caminho = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Microsoft", "Windows", "Notifications", "wpndatabase.db");

Console.WriteLine($"banco: {caminho} (existe: {File.Exists(caminho)})");

if (!File.Exists(caminho)) return;

var db = IntPtr.Zero;
if (sqlite3_open_v2(caminho, out db, 1, IntPtr.Zero) != 0)
{
    Console.WriteLine("nao deu para abrir");
    return;
}

Console.WriteLine("--- tabelas ---");
Executar(db, "SELECT name FROM sqlite_master WHERE type='table'", coluna =>
{
    var nome = sqlite3_column_text(coluna, 0);
    Console.WriteLine("  " + Marshal.PtrToStringAnsi(nome));
});

Console.WriteLine("--- contagem ---");
Executar(db, "SELECT COUNT(*), MAX([Order]) FROM Notification", coluna =>
{
    var total = sqlite3_column_text(coluna, 0);
    var maximo = sqlite3_column_text(coluna, 1);
    Console.WriteLine($"  notificacoes={Marshal.PtrToStringAnsi(total)} maiorOrder={Marshal.PtrToStringAnsi(maximo)}");
});

Console.WriteLine("--- colunas de Notification ---");
Executar(db, "PRAGMA table_info(Notification)", coluna =>
{
    var nome = sqlite3_column_text(coluna, 1);
    Console.WriteLine("  " + Marshal.PtrToStringAnsi(nome));
});

Console.WriteLine("--- notificacoes recentes ---");
Executar(db, "SELECT * FROM Notification LIMIT 4", coluna =>
{
    var total = sqlite3_column_count(coluna);
    Console.WriteLine($"  [linha com {total} colunas]");
    for (var i = 0; i < total; i++)
    {
        var valor = sqlite3_column_text(coluna, i);
        var texto = valor == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(valor) ?? "(null)";
        if (texto.Length > 300) texto = texto[..300];
        Console.WriteLine($"    {i}: {texto}");
    }
});

sqlite3_close(db);
return;

static void Executar(IntPtr db, string sql, Action<IntPtr> aoLer)
{
    var stmt = IntPtr.Zero;
    if (sqlite3_prepare_v2(db, sql, -1, out stmt, out _) != 0)
    {
        Console.WriteLine($"  consulta falhou: {Marshal.PtrToStringAnsi(sqlite3_errmsg(db))}");
        return;
    }

    while (sqlite3_step(stmt) == 100)
    {
        aoLer(stmt);
    }

    sqlite3_finalize(stmt);
}

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_open_v2(string arquivo, out IntPtr db, int flags, IntPtr vfs);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_close(IntPtr db);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_prepare_v2(IntPtr db, string sql, int nByte, out IntPtr stmt, out IntPtr tail);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_step(IntPtr stmt);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern IntPtr sqlite3_column_text(IntPtr stmt, int coluna);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_column_count(IntPtr stmt);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern int sqlite3_finalize(IntPtr stmt);

[DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
static extern IntPtr sqlite3_errmsg(IntPtr db);
