using Microsoft.Win32;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O que a maquina tem de geolocalizacao.
/// </summary>
/// <param name="ApiAvailable">A API de geolocalizacao do Windows existe nesta maquina?</param>
/// <param name="ServicePresent">O servico de localizacao do Windows existe?</param>
/// <param name="ServiceDisabled">O servico de localizacao esta desligado?</param>
/// <param name="Detail">A frase que explica o estado.</param>
public sealed record GeolocationState(
    bool ApiAvailable,
    bool ServicePresent,
    bool ServiceDisabled,
    string Detail);

/// <summary>
/// A deteccao de geolocalizacao.
///
/// A pergunta do competidor e "esta maquina tem geolocalizacao?". A resposta
/// honesta vem de dois sinais **medidos** no proprio Windows:
///
/// 1. a API <c>Windows.Devices.Geolocation.Geolocator</c> esta registrada
///    como classe ativavel (o Windows so registra a classe quando a API
///    existe);
/// 2. o servico "Localizador de usuarios" (<c>lfsvc</c>) existe e o que
///    o Windows diz sobre ele.
///
/// Nada aqui e chute: se a API nao estiver registrada, o Tranal diz que
/// nao ha.
///
/// A regra esta em um lugar so: este modulo sabe o que e "ter
/// geolocalizacao". A tela e a linha de comando usam o mesmo veredito.
/// </summary>
public static class GeolocationProbe
{
    /// <summary>
    /// A chave das classes ativaveis do Windows Runtime.
    /// </summary>
    public const string ActivatableClassRoot = @"SOFTWARE\Microsoft\WindowsRuntime\ActivatableClassId";

    /// <summary>A classe da API de geolocalizacao.</summary>
    public const string GeolocatorClass = "Windows.Devices.Geolocation.Geolocator";

    /// <summary>
    /// O servico de localizacao do Windows.
    /// </summary>
    public const string LocationServiceKey = @"SYSTEM\CurrentControlSet\Services\lfsvc";

    /// <summary>
    /// O estado da geolocalizacao nesta maquina.
    /// </summary>
    public static GeolocationState State()
    {
        var api = HasActivatableClass(GeolocatorClass);
        var (presente, desativado) = LocationService();

        var detalhe = !api
            ? "A API de geolocalizacao do Windows nao existe nesta maquina."
            : !presente
                ? "A API de geolocalizacao existe, mas o servico de localizacao nao foi encontrado."
                : desativado
                    ? "A geolocalizacao existe, mas o servico de localizacao esta desativado."
                    : "A maquina tem geolocalizacao disponivel.";

        return new GeolocationState(api, presente, desativado, detalhe);
    }

    /// <summary>
    /// A classe ativavel existe? O Windows registra a classe quando a API
    /// existe — e e assim que se mede a presenca da API.
    /// </summary>
    /// <param name="className">O nome da classe.</param>
    public static bool HasActivatableClass(string className)
    {
        try
        {
            using var hive = Registry.LocalMachine;
            using var key = hive.OpenSubKey(ActivatableClassRoot);
            if (key is null) return false;

            return key.GetSubKeyNames()
                .Any(n => string.Equals(n, className, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// O servico de localizacao existe? E esta desativado?
    /// </summary>
    private static (bool Present, bool Disabled) LocationService()
    {
        try
        {
            using var hive = Registry.LocalMachine;
            using var key = hive.OpenSubKey(LocationServiceKey);
            if (key is null) return (false, false);

            var start = key.GetValue("Start");
            var desativado = start is int i && i == 4;

            return (true, desativado);
        }
        catch
        {
            return (false, false);
        }
    }
}
