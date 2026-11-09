using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// As informacoes do usuario e da estacao de rede.
/// </summary>
/// <param name="UserName">O nome de quem esta usando a sessao.</param>
/// <param name="DomainName">O dominio (ou o grupo de trabalho).</param>
/// <param name="ComputerName">O nome do computador.</param>
/// <param name="IsWorkgroup">A estacao esta em um grupo de trabalho?</param>
/// <param name="MacAddress">O endereco fisico da placa de rede.</param>
public sealed record NetworkStationDetails(
    string UserName,
    string DomainName,
    string ComputerName,
    bool IsWorkgroup,
    string MacAddress);

/// <summary>
/// As informacoes de quem esta usando a maquina e da estacao de rede.
///
/// O Windows sabe o nome do usuario da sessao, o dominio e o nome do
/// computador. O endereco fisico da placa de rede vem da propria placa.
///
/// A regra esta em um lugar so: este modulo sabe o que e a estacao de rede.
/// A tela e a linha de comando consultam o mesmo dado.
/// </summary>
public static class NetworkStationInfo
{
    /// <summary>
    /// As informacoes da estacao.
    /// </summary>
    public static NetworkStationDetails Current()
    {
        var usuario = Environment.UserName;
        var dominio = Environment.UserDomainName;
        var computador = Environment.MachineName;

        var trabalho = string.IsNullOrWhiteSpace(dominio)
            || string.Equals(dominio, computador, StringComparison.OrdinalIgnoreCase);

        return new NetworkStationDetails(
            usuario,
            dominio,
            computador,
            trabalho,
            MacAddress());
    }

    /// <summary>
    /// O endereco fisico da placa de rede ativa.
    /// </summary>
    /// <returns>O endereco, ou uma string vazia quando nao ha.</returns>
    public static string MacAddress()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var mac = adapter.GetPhysicalAddress();
                var bytes = mac.GetAddressBytes();
                if (bytes.Length == 0) continue;

                return string.Join(":", bytes.Select(b => b.ToString("X2")));
            }
        }
        catch
        {
            // Sem placa de rede legivel.
        }

        return string.Empty;
    }

    /// <summary>
    /// O endereco IP da maquina na rede.
    /// </summary>
    public static string IPv4Address()
    {
        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var endereco in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (endereco.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (endereco.Address.ToString().StartsWith("127.", StringComparison.Ordinal)) continue;

                    return endereco.Address.ToString();
                }
            }
        }
        catch
        {
            // Sem endereco legivel.
        }

        return string.Empty;
    }
}
