using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Tranal.Uninstaller.Core;
using Tranal.Uninstaller.Core.Management;

namespace Tranal.Uninstaller.Core.RegistryEditing;

/// <summary>
/// Um alvo de remoção no registro: uma chave, ou um valor dentro de uma chave.
/// </summary>
public sealed class RegistryTarget
{
    /// <summary>O hive da chave.</summary>
    public RegistryHive Hive { get; init; } = RegistryHive.LocalMachine;

    /// <summary>A visao do registro (32 ou 64 bits).</summary>
    public RegistryView View { get; init; } = RegistryView.Default;

    /// <summary>O caminho da chave dentro do hive.</summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// O nome do valor a apagar. Vazio quando o alvo e a chave inteira.
    /// </summary>
    public string ValueName { get; init; } = string.Empty;

    /// <summary>True quando o alvo e um valor, e nao a chave.</summary>
    public bool IsValue => ValueName.Length > 0;

    /// <summary>
    /// O caminho completo, como o Editor do Registro mostra.
    /// </summary>
    public string DisplayPath
    {
        get
        {
            var hive = RegistryPath.HiveName(Hive);
            var base_ = $"{hive}\\{RelativePath}";

            return IsValue ? $"{base_}\\{ValueName}" : base_;
        }
    }

    /// <summary>
    /// Monta um alvo a partir do caminho completo.
    ///
    /// O caminho aceito e o do Editor do Registro
    /// (<c>HKEY_LOCAL_MACHINE\SOFTWARE\...</c>) e tambem o caminho solto
    /// (<c>SOFTWARE\...</c>), que e o que o scanner grava.
    /// </summary>
    /// <param name="path">O caminho.</param>
    /// <param name="valueName">O nome do valor, quando for um valor.</param>
    public static RegistryTarget? FromPath(string? path, string? valueName = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var parsed = RegistryPath.Parse(path);
        if (parsed is null) return null;

        return new RegistryTarget
        {
            Hive = parsed.Value.Hive,
            RelativePath = parsed.Value.RelativePath,
            ValueName = (valueName ?? string.Empty).Trim(),
        };
    }
}

/// <summary>
/// Remoção em lote de entradas de registro.
///
/// O Geek Uninstaller mede "Batch Remove Entries": apagar várias entradas de
/// registro de uma vez, e nao uma por uma. E a operação que o "remover
/// entrada do registro" faz quando o usuario seleciona varios programas.
///
/// A REGRA mora em <see cref="SafetyRules.IsProtectedRegistryKey"/>: uma chave
/// protegida nunca e apagada em lote, e o item sai no relatorio como
/// protegido — e nao como falha.
/// </summary>
public static class RegistryBatchRemover
{
    /// <summary>
    /// Apaga os alvos informados em lote.
    ///
    /// </summary>
    /// <param name="targets">Os alvos.</param>
    public static RemovalReport Remove(IEnumerable<RegistryTarget>? targets)
    {
        var lista = targets?.Where(t => t is not null).ToList() ?? new List<RegistryTarget>();
        var resultados = new List<RemovalOutcome>();

        foreach (var alvo in lista)
        {
            resultados.Add(RemoveOne(alvo));
        }

        return new RemovalReport(resultados);
    }

    /// <summary>
    /// Apaga um alvo e devolve o resultado.
    /// </summary>
    private static RemovalOutcome RemoveOne(RegistryTarget alvo)
    {
        var item = new LeftoverItem
        {
            Kind = alvo.IsValue ? "Valor de Registro" : "Registro",
            Path = alvo.DisplayPath,
            Detail = alvo.IsValue ? "Valor de registro" : "Chave de registro",
            Selected = true,
            Confidence = LeftoverConfidence.High,
            Reason = LeftoverReason.OrphanedRegistryEntry,
        };

        // A regra de protecao e a mesma do remocao avulsa: nada de lote
        // pula o que a remocao avulsa respeitaria.
        if (!alvo.IsValue && SafetyRules.IsProtectedRegistryKey(alvo.DisplayPath))
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Protected,
                Message = "Chave de registro protegida.",
            };
        }

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(alvo.Hive, alvo.View);

            if (alvo.IsValue)
            {
                using var chave = baseKey.OpenSubKey(alvo.RelativePath, writable: true);
                if (chave is null)
                {
                    return new RemovalOutcome
                    {
                        Item = item,
                        Status = RemovalStatus.AlreadyAbsent,
                        Message = "A chave nao existe mais.",
                    };
                }

                var nomes = chave.GetValueNames();
                if (!nomes.Contains(alvo.ValueName, StringComparer.OrdinalIgnoreCase))
                {
                    return new RemovalOutcome
                    {
                        Item = item,
                        Status = RemovalStatus.AlreadyAbsent,
                        Message = "O valor nao existe mais.",
                    };
                }

                chave.DeleteValue(alvo.ValueName, throwOnMissingValue: false);

                return new RemovalOutcome
                {
                    Item = item,
                    Status = RemovalStatus.Removed,
                    Message = "Valor removido do registro.",
                };
            }

            using var chaveAlvo = baseKey.OpenSubKey(alvo.RelativePath, writable: true);
            if (chaveAlvo is null)
            {
                return new RemovalOutcome
                {
                    Item = item,
                    Status = RemovalStatus.AlreadyAbsent,
                    Message = "A chave nao existe mais.",
                };
            }

            chaveAlvo.DeleteSubKeyTree(string.Empty, throwOnMissingSubKey: false);

            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Removed,
                Message = "Chave removida do registro.",
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = $"Sem permissao para alterar o registro: {ex.Message}",
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
        catch (System.Security.SecurityException ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = $"Sem permissao para alterar o registro: {ex.Message}",
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
        catch (Exception ex)
        {
            return new RemovalOutcome
            {
                Item = item,
                Status = RemovalStatus.Failed,
                Message = ex.Message,
                ErrorCode = RemovalOutcome.ErrorCodeOf(ex),
            };
        }
    }
}
