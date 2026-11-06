using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Mapa autoritativo de tipos -> metodos -> literais de string de um assembly .NET,
// lido direto do metadata do PE (sem carregar nem executar o assembly).
//
// uso: dotnet run tools/DotnetMap/Program.cs -- <assembly.dll> <saida.txt>

if (args.Length < 2)
{
    Console.Error.WriteLine("uso: dotnet run Program.cs -- <assembly> <saida.txt>");
    return 1;
}

string path = args[0];
string outPath = args[1];

using FileStream fs = File.OpenRead(path);
using PEReader pe = new PEReader(fs);

if (!pe.HasMetadata)
{
    Console.Error.WriteLine("sem metadata CLR: " + path);
    return 2;
}

MetadataReader md = pe.GetMetadataReader();
var sb = new System.Text.StringBuilder();

sb.AppendLine($"# arquivo: {Path.GetFileName(path)}");
sb.AppendLine($"# caminho: {path}");
sb.AppendLine($"# mvid: {md.GetGuid(md.GetModuleDefinition().Mvid)}");
sb.AppendLine($"# tipos: {md.TypeDefinitions.Count} | metodos: {md.MethodDefinitions.Count} | tipos exportados: {md.ExportedTypes.Count} | resources: {md.ManifestResources.Count}");
sb.AppendLine("# formato: TYPE <nome> | METHOD <nome> | LIT <literal de string carregado pelo metodo>");
sb.AppendLine();

int metodosComLiteral = 0;
int totalLiterais = 0;

foreach (TypeDefinitionHandle th in md.TypeDefinitions)
{
    TypeDefinition td = md.GetTypeDefinition(th);
    string ns = md.GetString(td.Namespace);
    string nome = md.GetString(td.Name);
    string completo = ns.Length > 0 ? ns + "." + nome : nome;
    sb.AppendLine($"TYPE\t{completo}");

    foreach (MethodDefinitionHandle mh in td.GetMethods())
    {
        MethodDefinition m = md.GetMethodDefinition(mh);
        string mn = md.GetString(m.Name);

        var literals = new List<string>();
        if (m.RelativeVirtualAddress != 0)
        {
            try
            {
                MethodBodyBlock body = pe.GetMethodBody(m.RelativeVirtualAddress);
                byte[]? il = body.GetILBytes();
                if (il != null)
                {
                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        // ldstr = 0x72 seguido de token de UserString
                        if (il[i] == 0x72)
                        {
                            int token = BitConverter.ToInt32(il, i + 1);
                            try
                            {
                                string lit = md.GetUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF));
                                if (!string.IsNullOrEmpty(lit)) literals.Add(lit);
                            }
                            catch (BadImageFormatException) { }
                        }
                    }
                }
            }
            catch (BadImageFormatException) { }
        }

        if (literals.Count > 0) metodosComLiteral++;
        totalLiterais += literals.Count;

        sb.AppendLine($"METHOD\t{mn}\t{literals.Count}");
        foreach (string lit in literals)
        {
            string limpo = lit.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            sb.AppendLine($"LIT\t{limpo}");
        }
    }
    sb.AppendLine();
}

sb.AppendLine($"# metodos com literal de string: {metodosComLiteral}");
sb.AppendLine($"# total de literais: {totalLiterais}");

File.WriteAllText(outPath, sb.ToString(), new System.Text.UTF8Encoding(false));
Console.WriteLine($"gravado: {outPath} | tipos={md.TypeDefinitions.Count} metodos={md.MethodDefinitions.Count} literais={totalLiterais}");
return 0;
