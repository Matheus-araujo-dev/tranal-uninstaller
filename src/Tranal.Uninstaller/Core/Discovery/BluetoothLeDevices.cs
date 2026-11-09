using System.Runtime.InteropServices;
using System.Text;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// Um dispositivo Bluetooth.
/// </summary>
/// <param name="Name">O nome do dispositivo.</param>
/// <param name="InstanceId">O identificador da instancia.</param>
/// <param name="HardwareId">O identificador de hardware.</param>
/// <param name="IsLowEnergy">O dispositivo e de baixa energia (Bluetooth LE)?</param>
public sealed record BluetoothDevice(
    string Name,
    string InstanceId,
    string HardwareId,
    bool IsLowEnergy);

/// <summary>
/// A descoberta dos dispositivos Bluetooth LE.
///
/// A lista de dispositivos vem da propria API de dispositivos do Windows
/// (<c>SetupDi</c>) — a mesma que o Gerenciador de dispositivos usa. Nao ha
/// radio "de verdade" no Tranal: o que ele descobre sao os dispositivos que o
/// Windows ja viu.
///
/// <para><b>Como se sabe que um dispositivo e LE.</b> Pelos servicos que ele
/// anuncia: um servico Bluetooth LE usa os identificadores de 16 bits da
/// Bluetooth SIG (0x1800 e companhia), que aparecem no identificador de
/// hardware como <c>000018xx-0000-1000-8000-00805f9b34fb</c>. E esse
/// identificador que o modulo procura — nao o nome do dispositivo, que
/// poderia ter qualquer palavra.</para>
///
/// A regra esta em um lugar so: este modulo sabe o que e um dispositivo
/// Bluetooth e como saber se ele e de baixa energia.
/// </summary>
public static class BluetoothLeDevices
{
    /// <summary>
    /// A classe de dispositivo do Bluetooth no Windows.
    /// </summary>
    public static readonly Guid BluetoothClassId =
        new("e0cbf06c-cd8b-4647-bbdb-39d0eac0d3b0");

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [StructLayout(LayoutKind.Explicit)]
    private struct SP_DEVINFO_DATA
    {
        [FieldOffset(0)] public int cbSize;
        [FieldOffset(8)] public Guid ClassGuid;
        [FieldOffset(24)] public uint DevInst;
        [FieldOffset(32)] public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(
        IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property,
        out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
        StringBuilder? deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    /// <summary>
    /// Os dispositivos Bluetooth que o Windows viu, e quais sao de baixa
    /// energia.
    /// </summary>
    public static IReadOnlyList<BluetoothDevice> List()
    {
        var dispositivos = new List<BluetoothDevice>();

        var classId = BluetoothClassId;
        var set = SetupDiGetClassDevs(ref classId, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (set == IntPtr.Zero || set == new IntPtr(-1)) return dispositivos;
        if (Marshal.SizeOf<SP_DEVINFO_DATA>() != 40) return dispositivos;

        try
        {
            var dados = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };

            for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref dados); i++)
            {
                var hardwareId = ReadProperty(set, ref dados, SPDRP_HARDWAREID);
                var nome = ReadProperty(set, ref dados, SPDRP_FRIENDLYNAME);
                var instancia = ReadInstanceId(set, ref dados);

                if (nome.Length == 0 && hardwareId.Length == 0) continue;

                dispositivos.Add(new BluetoothDevice(
                    nome,
                    instancia,
                    hardwareId,
                    IsLowEnergy(hardwareId)));
            }
        }
        catch
        {
            // Sem permissao ou sem dispositivos: a lista fica vazia.
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }

        return dispositivos;
    }

    /// <summary>
    /// O dispositivo e de baixa energia?
    ///
    /// O sinal e o servico anunciado: os servicos LE da Bluetooth SIG usam o
    /// UUID base de 32 bits, que aparece no identificador de hardware.
    /// </summary>
    /// <param name="hardwareId">O identificador de hardware.</param>
    public static bool IsLowEnergy(string? hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId)) return false;

        var texto = hardwareId.ToLowerInvariant();

        // O padrao de servico: 0000XXXX-0000-1000-8000-00805f9b34fb, em que
        // XXXX e o identificador de 16 bits.
        var achados = System.Text.RegularExpressions.Regex.Matches(
            texto,
            "0000([0-9a-f]{4})-0000-1000-8000-00805f9b34fb");

        foreach (System.Text.RegularExpressions.Match achado in achados)
        {
            if (!int.TryParse(achado.Groups[1].Value, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var servico))
            {
                continue;
            }

            // A faixa de servicos da Bluetooth SIG (0x1800 em diante) e a do
            // Bluetooth LE.
            if (servico >= 0x1800 && servico <= 0x1FFF) return true;
        }

        return false;
    }

    /// <summary>
    /// Os dispositivos de baixa energia.
    /// </summary>
    public static IReadOnlyList<BluetoothDevice> LowEnergy() =>
        List().Where(d => d.IsLowEnergy).ToList();

    /// <summary>
    /// O texto que explica o que foi encontrado.
    /// </summary>
    public static string Describe()
    {
        var todos = List();
        var le = todos.Count(d => d.IsLowEnergy);

        return $"{todos.Count} dispositivo(s) Bluetooth, {le} de baixa energia.";
    }

    private static string ReadProperty(IntPtr set, ref SP_DEVINFO_DATA dados, uint property)
    {
        try
        {
            uint tipo = 0;
            uint tamanho = 0;

            if (!SetupDiGetDeviceRegistryPropertyW(set, ref dados, property, out tipo, null, 0, out tamanho))
            {
                return string.Empty;
            }

            if (tamanho == 0) return string.Empty;

            var buffer = new byte[tamanho];
            if (!SetupDiGetDeviceRegistryPropertyW(set, ref dados, property, out tipo, buffer, tamanho, out tamanho))
            {
                return string.Empty;
            }

            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadInstanceId(IntPtr set, ref SP_DEVINFO_DATA dados)
    {
        try
        {
            uint tamanho = 0;
            SetupDiGetDeviceInstanceId(set, ref dados, null, 0, out tamanho);
            if (tamanho == 0) return string.Empty;

            var buffer = new StringBuilder((int)tamanho);
            SetupDiGetDeviceInstanceId(set, ref dados, buffer, tamanho, out tamanho);

            return buffer.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
