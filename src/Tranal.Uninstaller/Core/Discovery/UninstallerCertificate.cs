using System.Formats.Asn1;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Tranal.Uninstaller.Core.Discovery;

/// <summary>
/// O estado da assinatura digital do desinstalador.
/// </summary>
public enum SignatureTrustStatus
{
    /// <summary>A cadeia confia no Windows (autoridade confiável valida).</summary>
    Valid,

    /// <summary>
    /// A assinatura existe, mas a raiz nao esta nas autoridades
    /// confiáveis do Windows. E o caso tipico de certificado de teste
    /// autossinado — e informacao a mostrar ao usuario, nao defeito.
    /// </summary>
    UntrustedRoot,

    /// <summary>O certificado esta fora do periodo de validade.</summary>
    Expired,

    /// <summary>O certificado foi revogado.</summary>
    Revoked,

    /// <summary>O arquivo nao tem assinatura digital.</summary>
    NotSigned,

    /// <summary>A assinatura existe, mas o Windows nao a aceitou.</summary>
    Invalid,

    /// <summary>O Windows devolveu um estado que o Tranal nao conhece.</summary>
    UnknownError,
}

/// <summary>
/// O resultado da verificacao de assinatura de um executavel.
/// </summary>
public sealed class UninstallerSignature
{
    /// <summary>True quando o arquivo tem assinatura digital.</summary>
    public bool IsSigned { get; init; }

    /// <summary>Estado da confianca da assinatura.</summary>
    public SignatureTrustStatus Status { get; init; }

    /// <summary>Quem assinou (nome do titular do certificado).</summary>
    public string SignerSubject { get; init; } = string.Empty;

    /// <summary>Quem emitiu o certificado (autoridade emissora).</summary>
    public string SignerIssuer { get; init; } = string.Empty;

    /// <summary>Inicio da validade do certificado.</summary>
    public DateTime? NotBefore { get; init; }

    /// <summary>Fim da validade do certificado.</summary>
    public DateTime? NotAfter { get; init; }

    /// <summary>A cadeia de certificados, do signatario a raiz.</summary>
    public IReadOnlyList<string> ChainSubjects { get; init; } = Array.Empty<string>();

    /// <summary>Os problemas que a construcao da cadeia reportou.</summary>
    public IReadOnlyList<string> ChainProblems { get; init; } = Array.Empty<string>();

    /// <summary>O codigo devolvido pelo <c>WinVerifyTrust</c> (0 = confia).</summary>
    public int WinVerifyTrustCode { get; init; }

    /// <summary>Explicacao do estado, em portugues.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>True quando o Windows confia na assinatura.</summary>
    public bool IsTrusted => Status == SignatureTrustStatus.Valid;
}

/// <summary>
/// Verificacao da assinatura digital do desinstalador (item #16 do backlog).
///
/// A verificacao tem duas metades, e elas nao dizem a mesma coisa:
///
/// <list type="bullet">
/// <item>a <b>cadeia</b> (o certificado do arquivo e o
/// <see cref="X509Chain"/>) descreve quem assinou e por qual autoridade —
/// o .NET diz que a cadeia e "valida" mesmo quando a raiz nao esta ancorada
/// em nenhuma autoridade confiavel;</item>
/// <item>a <b>confianca real</b> (o <c>WinVerifyTrust</c> do Windows) e a
/// unica que o Windows respeita de verdade.</item>
/// </list>
///
/// Por isso os dois sao lidos: a cadeia conta a historia e o
/// <c>WinVerifyTrust</c> da o veredito.
///
/// Observacao de implementacao: o <c>SignedCms</c> mora no assembly
/// <c>System.Security.Cryptography.Pkcs</c>, que **nao** faz parte do
/// framework do .NET 10 (conferido no pacote de referencia) e exigiria um
/// <c>PackageReference</c> novo — proibido pelas regras do projeto. A
/// mesma informacao (os certificados embutidos na assinatura) e lida
/// diretamente do PKCS#7 do executavel com o <see cref="AsnReader"/>, que
/// faz parte do framework.
/// </summary>
public static class UninstallerCertificate
{
    /// <summary>Acao de verificacao generica do Windows (GUID do WinVerifyTrust).</summary>
    private static readonly Guid VerifyAction =
        new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    /// <summary>
    /// Verifica a assinatura do executavel. **Nunca lanca**: um arquivo sem
    /// assinatura (ou invalido) devolve um estado, nao uma excecao.
    /// </summary>
    public static UninstallerSignature Verify(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new UninstallerSignature
            {
                IsSigned = false,
                Status = SignatureTrustStatus.NotSigned,
                Message = "Nenhum arquivo informado.",
            };
        }

        if (!File.Exists(path))
        {
            return new UninstallerSignature
            {
                IsSigned = false,
                Status = SignatureTrustStatus.NotSigned,
                Message = "Arquivo nao encontrado.",
            };
        }

        X509Certificate2? signer = null;
        try
        {
            // O certificado do signatario vem da assinatura Authenticode do
            // proprio executavel. Nao ha substituto no framework para esta
            // leitura (o carregador do PKCS#7 exigiria o pacote Pkcs, que o
            // projeto nao usa), e por isso a API obsoleta fica aqui com a
            // supressao explicita e justificada.
#pragma warning disable SYSLIB0057
            var certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
            signer = new X509Certificate2(certificate);
        }
        catch (CryptographicException)
        {
            // Arquivo sem assinatura (ou que nao e um executavel): e um
            // estado legitimo, nao uma falha.
            return new UninstallerSignature
            {
                IsSigned = false,
                Status = SignatureTrustStatus.NotSigned,
                Message = "O arquivo nao tem assinatura digital.",
            };
        }
        catch (Exception ex)
        {
            return new UninstallerSignature
            {
                IsSigned = false,
                Status = SignatureTrustStatus.Invalid,
                Message = $"Nao foi possivel ler a assinatura: {ex.Message}",
            };
        }

        using (signer)
        {
            var chainSubjects = new List<string>();
            var chainProblems = new List<string>();
            var chainStatus = SignatureTrustStatus.UnknownError;

            try
            {
                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;

                chain.Build(signer);

                foreach (var element in chain.ChainElements)
                    chainSubjects.Add(element.Certificate.Subject);

                foreach (var item in chain.ChainStatus)
                {
                    var text = item.StatusInformation?.Trim() ?? string.Empty;
                    chainProblems.Add(text.Length > 0 ? text : item.Status.ToString());
                }

                chainStatus = StatusFromChainFlags(chain.ChainStatus);
            }
            catch (Exception ex)
            {
                chainProblems.Add($"A cadeia nao pode ser montada: {ex.Message}");
            }

            // Os certificados embutidos no PKCS#7 completam a cadeia quando o
            // Windows nao tem o certificado intermediario instalado.
            foreach (var subject in ReadEmbeddedCertificateSubjects(path))
            {
                if (!chainSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase))
                    chainSubjects.Add(subject);
            }

            var trustCode = VerifyWithWindows(path);
            var trustStatus = StatusFromTrustCode(trustCode);

            // O veredito do Windows manda; a cadeia so explica.
            if (trustStatus == SignatureTrustStatus.UnknownError
                && chainStatus != SignatureTrustStatus.UnknownError)
            {
                trustStatus = chainStatus;
            }

            return new UninstallerSignature
            {
                IsSigned = true,
                Status = trustStatus,
                SignerSubject = signer.Subject,
                SignerIssuer = signer.Issuer,
                NotBefore = signer.NotBefore,
                NotAfter = signer.NotAfter,
                ChainSubjects = chainSubjects,
                ChainProblems = chainProblems,
                WinVerifyTrustCode = trustCode,
                Message = Describe(trustStatus),
            };
        }
    }

    /// <summary>
    /// Traduz o codigo devolvido pelo <c>WinVerifyTrust</c> em estado.
    /// </summary>
    public static SignatureTrustStatus StatusFromTrustCode(int trustCode)
    {
        var code = unchecked((uint)trustCode);

        return code switch
        {
            0x00000000 => SignatureTrustStatus.Valid,          // S_OK
            0x800B0100 => SignatureTrustStatus.UntrustedRoot,  // CERT_E_UNTRUSTEDROOT
            0x800B0004 => SignatureTrustStatus.UntrustedRoot,  // TRUST_E_SUBJECT_NOT_TRUSTED
            0x800B0101 => SignatureTrustStatus.Invalid,         // CERT_E_CN_NO_MATCH
            0x800B0109 => SignatureTrustStatus.Expired,         // CERT_E_EXPIRED
            0x80092010 => SignatureTrustStatus.Revoked,         // CRYPT_E_REVOKED
            0x80096010 => SignatureTrustStatus.NotSigned,       // TRUST_E_NOSIGNATURE
            0x80096011 => SignatureTrustStatus.NotSigned,       // TRUST_E_SUBJECT_FORM_UNKNOWN
            _ => SignatureTrustStatus.UnknownError,
        };
    }

    /// <summary>
    /// Traduz os problemas da cadeia em estado.
    /// </summary>
    private static SignatureTrustStatus StatusFromChainFlags(X509ChainStatus[] statuses)
    {
        var result = SignatureTrustStatus.UnknownError;

        foreach (var item in statuses)
        {
            var flags = item.Status;

            if ((flags & X509ChainStatusFlags.NotTimeValid) != 0) return SignatureTrustStatus.Expired;
            if ((flags & X509ChainStatusFlags.Revoked) != 0) return SignatureTrustStatus.Revoked;
            if ((flags & X509ChainStatusFlags.UntrustedRoot) != 0) result = SignatureTrustStatus.UntrustedRoot;
            else if ((flags & X509ChainStatusFlags.PartialChain) != 0) result = SignatureTrustStatus.UntrustedRoot;
            else if ((flags & X509ChainStatusFlags.InvalidBasicConstraints) != 0) result = SignatureTrustStatus.Invalid;
        }

        return result;
    }

    /// <summary>
    /// A frase que explica o estado ao usuario.
    /// </summary>
    private static string Describe(SignatureTrustStatus status) => status switch
    {
        SignatureTrustStatus.Valid =>
            "A assinatura e valida: o Windows confia na cadeia.",
        SignatureTrustStatus.UntrustedRoot =>
            "A assinatura existe, mas a raiz do certificado nao esta nas autoridades "
            + "confiaveis deste Windows. Isso e comum em certificado de teste "
            + "autossinado e nao significa que o arquivo esteja alterado.",
        SignatureTrustStatus.Expired =>
            "O certificado do signatario esta fora do periodo de validade.",
        SignatureTrustStatus.Revoked =>
            "O certificado do signatario foi revogado.",
        SignatureTrustStatus.NotSigned =>
            "O arquivo nao tem assinatura digital.",
        SignatureTrustStatus.Invalid =>
            "O Windows nao aceitou a assinatura do arquivo.",
        _ =>
            "O Windows devolveu um estado de assinatura que o Tranal nao conhece.",
    };

    /// <summary>
    /// Le os certificados embutidos na assinatura do executavel (o PKCS#7 do
    /// diretorio de seguranca do PE).
    ///
    /// Devolve lista vazia quando o arquivo nao tem assinatura ou quando o
    /// formato nao pode ser lido — a cadeia mostrada vem do Windows.
    /// </summary>
    private static IReadOnlyList<string> ReadEmbeddedCertificateSubjects(string path)
    {
        var subjects = new List<string>();

        try
        {
            var blob = ReadEmbeddedPkcs7(path);
            if (blob is null || blob.Length == 0) return subjects;

            var reader = new AsnReader(blob, AsnEncodingRules.BER);
            var contentInfo = reader.ReadSequence();

            // 1.2.840.113549.1.7.2 = signedData.
            if (contentInfo.ReadObjectIdentifier() != "1.2.840.113549.1.7.2") return subjects;

            var signedData = contentInfo.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            signedData.ReadInteger();          // versao
            signedData.ReadSetOf();            // algoritmos de resumo
            signedData.ReadSequence();         // encapsulatedContentInfo

            while (signedData.HasData)
            {
                var tag = signedData.PeekTag();

                if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 0)
                {
                    // certificates [0] IMPLICIT SET OF Certificate
                    var certificates = signedData.ReadSetOf(
                        skipSortOrderValidation: true,
                        expectedTag: new Asn1Tag(TagClass.ContextSpecific, 0, true));

                    while (certificates.HasData)
                    {
                        var raw = certificates.ReadEncodedValue().ToArray();

                        // SYSLIB0057: o certificado embutido no PKCS#7 vem
                        // como DER cru; o carregamento por construtor e o
                        // caminho para DER nesta versao do framework.
using var embedded = X509CertificateLoader.LoadCertificate(raw);
                        subjects.Add(embedded.Subject);
                    }

                    continue;
                }

                if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 1)
                {
                    // crls [1] IMPLICIT
                    signedData.ReadSetOf(
                        skipSortOrderValidation: true,
                        expectedTag: new Asn1Tag(TagClass.ContextSpecific, 1, true));
                    continue;
                }

                if (tag.TagClass == TagClass.Universal && tag.TagValue == 16)
                {
                    // signerInfos
                    signedData.ReadSetOf();
                    continue;
                }

                break;
            }
        }
        catch
        {
            // Formato inesperado: a cadeia do Windows continua valendo.
        }

        return subjects;
    }

    /// <summary>
    /// Extrai o PKCS#7 do diretorio de seguranca do executavel PE.
    /// </summary>
    private static byte[]? ReadEmbeddedPkcs7(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 0x40) return null;

            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 24 > stream.Length) return null;

            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550) return null;   // "PE\0\0"

            reader.ReadUInt16();      // Machine
            reader.ReadUInt16();      // NumberOfSections
            reader.ReadUInt32();      // TimeDateStamp
            reader.ReadUInt32();      // PointerToSymbolTable
            reader.ReadUInt32();      // NumberOfSymbols
            reader.ReadUInt16();      // SizeOfOptionalHeader
            reader.ReadUInt16();      // Characteristics

            var optionalHeaderStart = stream.Position;
            var magic = reader.ReadUInt16();
            var isPe32Plus = magic == 0x20B;

            // O diretorio de seguranca e o quinto (indice 4).
            var securityEntry = optionalHeaderStart + (isPe32Plus ? 112 : 96) + 4 * 8;
            if (securityEntry + 8 > stream.Length) return null;

            stream.Position = securityEntry;
            var certificateOffset = reader.ReadUInt32();
            var certificateSize = reader.ReadUInt32();

            if (certificateOffset == 0 || certificateSize == 0) return null;
            if (certificateOffset + certificateSize > stream.Length) return null;

            stream.Position = certificateOffset;
            var length = reader.ReadUInt32();
            reader.ReadUInt16();      // wRevision
            reader.ReadUInt16();      // wCertificateType

            var blobLength = (int)length - 8;
            if (blobLength <= 0) return null;

            return reader.ReadBytes(blobLength);
        }
        catch
        {
            // Arquivo que nao e PE valido: nao ha PKCS#7 embutido.
            return null;
        }
    }

    /// <summary>
    /// Pergunta ao Windows se ele confia na assinatura do arquivo.
    /// </summary>
    private static int VerifyWithWindows(string path)
    {
        var fileInfo = new WinTrustFileInfo
        {
            cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
            pcwszFilePath = Marshal.StringToHGlobalUni(path),
            hFile = IntPtr.Zero,
            pgKnownSubject = IntPtr.Zero,
        };

        var data = new WinTrustData
        {
            cbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
            pwszURL = IntPtr.Zero,
            dwUIChoice = 2,          // WTD_UI_NONE
            fdwRevocationChecks = 0, // WTD_REVOKE_NONE
            dwUnionChoice = 1,       // WTD_CHOICE_FILE
            pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>()),
            dwStateAction = 1,       // WTD_STATEACTION_VERIFY
            hWVTStateData = IntPtr.Zero,
            pwszURLReference = IntPtr.Zero,
            dwProvFlags = 0,
            dwUIContext = 0,
        };

        try
        {
            Marshal.StructureToPtr(fileInfo, data.pFile, false);

            var result = WinVerifyTrust(IntPtr.Zero, VerifyAction, ref data);

            // Fecha o estado aberto pela verificacao.
            data.dwStateAction = 2;  // WTD_STATEACTION_CLOSE
            WinVerifyTrust(IntPtr.Zero, VerifyAction, ref data);

            return result;
        }
        catch (Exception)
        {
            // A API de confianca nao respondeu: o estado fica desconhecido.
            return unchecked((int)0x800B0100);
        }
        finally
        {
            Marshal.FreeHGlobal(data.pFile);
            Marshal.FreeHGlobal(fileInfo.pcwszFilePath);
        }
    }

    /// <summary>
    /// A informacao do arquivo lida pelo WinVerifyTrust.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        public IntPtr pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    /// <summary>
    /// A estrutura de dados da verificacao de confianca.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pwszURL;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    /// <summary>
    /// A API de confianca do Windows.
    /// </summary>
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(
        IntPtr window,
        [MarshalAs(UnmanagedType.LPStruct)] Guid actionId,
        ref WinTrustData data);
}
