using System.Security.Cryptography;
using System.Text;
using MLMConquerorGlobalEdition.Repository.Services.Entrax;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// La firma de Entrax según su documentación: canónica METHOD\nPATH\nTIMESTAMP\nNONCE\nbase64(sha256(body))
/// firmada con HMAC-SHA256 y en Base64; el callback, HMAC-SHA256 del cuerpo crudo en X-Signature.
/// Los valores esperados se calculan aquí aparte, sin pasar por la clase que se prueba.
/// </summary>
public class EntraxSignatureTests
{
    private const string Secreto = "secreto-de-pruebas";

    private static string HmacBase64(string secreto, byte[] datos)
        => Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), datos));

    [Fact]
    public void El_hash_de_un_cuerpo_vacio_es_el_de_sha256_vacio()
        => EntraxSignature.BodyHash(string.Empty).Should().Be("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=");

    [Fact]
    public void La_canonica_sigue_el_formato_de_la_documentacion()
    {
        var body = "{\"amount\":100.50}";
        var esperado = "POST\n/api/v1/cash-register/qr\n1640995200\nabc\n"
                     + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

        EntraxSignature.Canonical("POST", "/api/v1/cash-register/qr", 1640995200, "abc", body).Should().Be(esperado);
    }

    [Fact]
    public void La_firma_es_HMAC_SHA256_en_Base64_de_la_canonica()
    {
        var canonica = "GET\n/api/v1/cash-register/payment/x\n1\nn\n47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=";

        EntraxSignature.Sign(canonica, Secreto).Should().Be(HmacBase64(Secreto, Encoding.UTF8.GetBytes(canonica)));
    }

    [Fact]
    public void La_cabecera_lleva_las_cuatro_partes()
        => EntraxSignature.AuthorizationHeader("key-1", 1640995200, "abc", "c2ln")
            .Should().Be("HMAC-SHA256 KeyId=key-1,Timestamp=1640995200,Nonce=abc,Signature=c2ln");

    [Fact]
    public void Un_callback_firmado_en_Base64_se_acepta()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\"}");
        EntraxSignature.VerifyCallback(cuerpo, HmacBase64(Secreto, cuerpo), Secreto).Should().BeTrue();
    }

    [Fact]
    public void Un_callback_firmado_en_hexadecimal_tambien_se_acepta()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"paymentId\":\"x\"}");
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secreto), cuerpo)).ToLowerInvariant();

        EntraxSignature.VerifyCallback(cuerpo, hex, Secreto).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-base64!!")]
    [InlineData("AAAA")]
    public void Sin_firma_o_con_firma_mala_se_rechaza(string? firma)
        => EntraxSignature.VerifyCallback(Encoding.UTF8.GetBytes("{}"), firma, Secreto).Should().BeFalse();

    [Fact]
    public void Un_byte_cambiado_en_el_cuerpo_invalida_la_firma()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"status\":\"SUCCESS\"}");
        var firma = HmacBase64(Secreto, cuerpo);
        cuerpo[^2] = (byte)'X';

        EntraxSignature.VerifyCallback(cuerpo, firma, Secreto).Should().BeFalse();
    }

    [Fact]
    public void Sin_secreto_nada_es_valido()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{}");
        EntraxSignature.VerifyCallback(cuerpo, HmacBase64(string.Empty, cuerpo), string.Empty).Should().BeFalse();
    }
}
