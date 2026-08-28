using System.Security.Cryptography;
using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Configuration;

public class JwtKeyGuardTests
{
    /// <summary>
    /// Llave privada que sigue commiteada en texto plano hoy en
    /// MLMConquerorGlobalEdition.SignupAPI/appsettings.json (y también en
    /// MLMConquerorGlobalEdition.AdminAPI/appsettings.json). Se copia aquí a propósito,
    /// literal, para verificar que <see cref="JwtKeyGuard"/> la rechaza por huella SPKI.
    /// Esta llave está en el historial de git para siempre, así que se considera
    /// comprometida de forma permanente hasta que se rote (Tarea 3 del Plan A).
    /// </summary>
    private const string RevokedPrivateKeySample =
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC4bmDlbzdUGJJPYtJf4o7XCayVP0ml8D6GUaWp8DYWHp2qGnRSKK/W3SpQOk527sR3n0lorN/pDuWn0McP9AaZcwJNr69C0haT59VnzhMf8pjT1FH8aFZMyEMStKBF8eQx8BMsMXBm7Ks04t3NzOpiBywR3drnBU/USJjQ0S9m+CrHod/Wqpc39X37NXF4bA2LI/pd6SLhwe1fsN+IU7SVuJCbn0G3+URCswKCZEL21lIYeZgXPk1cgc91TVJ33T4JDBe9CQIlFkRqCBwKxfvplaiT8VM3j9g5DBPs9ODZKUPgev/ZaaIXGQxLBLEC+z1nWWvgs+UeziL/jMF/Rf9hAgMBAAECggEAE+Pzl0rzUKKFxQIHZkfs18w3TLFSpA7Q73OGxlkdvCz5HAtWWDYDIM0hbx2asMD3d186b4uVanEs23hsv4+11n7M1MwJvs8hmDn8jgFvlpZ3XQEdBnfKNuWyNiY80s5PqgMWTkHWuYL0w/NjsHpVHNVhL1wZ66hq/54EvSCSWCxkhD81HOkGzyd8nlubv2kDOtP+MT+L+/gdu/7XcBItKs6qpZNPq36qBKvFogWsecnXGJc0roT+WsgOnY1kcE1+cUL80Nnj4lhE1NkE1FIGIH3+bGDOJeLZ6vI79TCqfa/zXr05nUusHqy1LHEFcS0rDUb6IwWDUQBzA6mPXY7+cQKBgQDtLwHUi3EZ7AaA/WfjAm1ekul2azz3hBOovwZiRC+XJ4Xbw+E1nzKBQM/vpuGbM8ilo7Dg0Jk6TTmuXXaX43hZwAcTSAv6dxVInNyWVmI1CL0dgBVAvqOTunWbCFeDJeweu0powpS3sxa/yyVhqo/yxsw1wKFQISADEHYx/hTghwKBgQDHEAPQ51NuzV8h3lsr1ZZGhKTRs+CSGinutfE14Wuj14fXmtKTKqgjOwyR/uLDmRmFim6zTT+gwkOWYuKufk13htSym0h8J0Lu3eisMZstN1V3uQyD0InB5N3KxSdNPmDkSYhfp4S/IcUFUpc8uKsssZLiBgHWGFilXOKVnxii1wKBgC1tO2SB8H+OfRBneGY6KMhcSuqrT1n4qes/6vEXLiY9I54bvh6PBxVKXIkB5WPcXymaWF42laJc+Bc1P2mH350Q8kn3GpQ2CpWFtZn1oYmWyuHDjk6ANMQuiifPSTONJ9Qa+v1lhyZH3quNNPOnvzo4aRRCeKLoNUFg/cJFb6oxAoGAfWlYjh9/T/pxafwVwnsQ7yKYWEmUPVfgfTUBX6nCT/n06l/vNKqWPYPxOnWz1fk5vAloDuynHpflTvTDzZ1jHt/CVzg/pYByydzivsGN+yG2ZfQer1kNwSt4lEw7o42eK5UsJt96YZRb9SuAfs/1f/XsDRwtwf2as6veUNdoBQcCgYBEAahv2JzK6Rt3E/3/R4ujdIxz92cwATJK3q18NjCFS0J7Rpm9ObFFlC3n2tIY+HwT+Dh9yglGLoZOsvadSvWOyGRcZ4jTgJbJomfQB+s1gsLm5uK6BGYPSAA50udUr2OQQvxzgXEHy4/lfjg8PeC7cU9DqV1jsPYc2RzoWLLOmw==";

    /// <summary>
    /// Llave pública del mismo par revocado, copiada literal de
    /// MLMConquerorGlobalEdition.SignupAPI/appsettings.json. El SPKI de esta llave y el
    /// derivado de <see cref="RevokedPrivateKeySample"/> son el mismo par, así que ambas
    /// deben producir la misma huella y ser rechazadas por <see cref="JwtKeyGuard"/>.
    /// </summary>
    private const string RevokedPublicKeySample =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAuG5g5W83VBiST2LSX+KO1wmslT9JpfA+hlGlqfA2Fh6dqhp0Uiiv1t0qUDpOdu7Ed59JaKzf6Q7lp9DHD/QGmXMCTa+vQtIWk+fVZ84TH/KY09RR/GhWTMhDErSgRfHkMfATLDFwZuyrNOLdzczqYgcsEd3a5wVP1EiY0NEvZvgqx6Hf1qqXN/V9+zVxeGwNiyP6Xeki4cHtX7DfiFO0lbiQm59Bt/lEQrMCgmRC9tZSGHmYFz5NXIHPdU1Sd90+CQwXvQkCJRZEaggcCsX76ZWok/FTN4/YOQwT7PTg2SlD4Hr/2WmiFxkMSwSxAvs9Z1lr4LPlHs4i/4zBf0X/YQIDAQAB";

    /// <summary>Envuelve un base64 a 64 columnas, como sale de un .pem, un bloque YAML de K8s o un backup de vault.</summary>
    private static string WrapAt64Columns(string base64)
    {
        var lines = new List<string>();
        for (var i = 0; i < base64.Length; i += 64)
            lines.Add(base64.Substring(i, Math.Min(64, base64.Length - i)));
        return string.Join('\n', lines);
    }

    [Fact]
    public void ValidatePrivateKey_WhenNull_Throws()
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey(null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:PrivateKeyBase64*no está configurada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenWhitespace_Throws()
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey("   ");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no está configurada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenRevokedKey_Throws()
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey(RevokedPrivateKeySample);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenRevokedKeyHasSurroundingWhitespace_StillThrows()
    {
        var keyWithWhitespace = "  \n" + RevokedPrivateKeySample + "\n  ";

        Action act = () => JwtKeyGuard.ValidatePrivateKey(keyWithWhitespace);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    /// <summary>
    /// Regresión de C1: la huella se calcula sobre el SPKI derivado de la llave, no sobre el
    /// string de configuración. La misma llave revocada envuelta a 64 columnas (como sale de
    /// cualquier .pem, backup de vault o secreto de Kubernetes) debe seguir siendo rechazada;
    /// antes del fix, huellear el string crudo dejaba pasar esta variante.
    /// </summary>
    [Fact]
    public void ValidatePrivateKey_WhenRevokedKeyWrappedAt64Columns_StillThrows()
    {
        var wrapped = WrapAt64Columns(RevokedPrivateKeySample);

        Action act = () => JwtKeyGuard.ValidatePrivateKey(wrapped);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    /// <summary>
    /// M-2: el caso feliz debe probar la normalización, no solo el "no lanza". Se pasa la
    /// llave envuelta a 64 columnas y con espacios alrededor, y se compara contra el base64
    /// canónico de una sola línea que sale de re-codificar el DER (M-1). Con un input ya
    /// sin espacios, esta prueba pasaría igual sin que el guarda normalizara nada.
    /// </summary>
    [Fact]
    public void ValidatePrivateKey_WhenValidKeyHasWhitespace_ReturnsCanonicalBase64()
    {
        using var rsa = RSA.Create(2048);
        var der = rsa.ExportPkcs8PrivateKey();
        var canonical = Convert.ToBase64String(der);
        var wrapped = "  \n" + WrapAt64Columns(canonical) + "\n  ";

        var result = JwtKeyGuard.ValidatePrivateKey(wrapped);

        result.Should().Be(canonical);
    }

    [Fact]
    public void ValidatePrivateKey_WhenNotAValidRsaKey_ThrowsNamingConfigKey()
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey("hola-mundo", configKey: "Jwt:OtraLlave");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:OtraLlave*no es una llave RSA válida*");
    }

    /// <summary>I-B: una RSA por debajo de 2048 bits es factorizable, tan inutilizable como la revocada.</summary>
    [Fact]
    public void ValidatePrivateKey_WhenKeyTooWeak_ThrowsMentioningMinimum()
    {
        using var rsa = RSA.Create(1024);
        var weakKey = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());

        Action act = () => JwtKeyGuard.ValidatePrivateKey(weakKey);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*1024*2048*");
    }

    /// <summary>
    /// I-C: openssl genrsa produce PKCS#1 por defecto. El guarda no lo acepta, pero el mensaje
    /// debe nombrar el caso y decir cómo convertirlo, en vez de un "no es una llave RSA válida"
    /// genérico y confuso para una llave que en realidad sí es válida, solo en otro formato.
    /// </summary>
    [Fact]
    public void ValidatePrivateKey_WhenPkcs1_ThrowsWithConversionHint()
    {
        using var rsa = RSA.Create(2048);
        var pkcs1Key = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

        Action act = () => JwtKeyGuard.ValidatePrivateKey(pkcs1Key);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PKCS#1*openssl pkcs8 -topk8*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RevokedPrivateKeySample)]
    public void ValidatePrivateKey_UsesGivenConfigKeyInMessage(string? input)
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey(input, configKey: "Jwt:OtraLlave");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:OtraLlave*");
    }

    [Fact]
    public void ValidatePublicKey_WhenNull_Throws()
    {
        Action act = () => JwtKeyGuard.ValidatePublicKey(null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:PublicKeyBase64*no está configurada*");
    }

    [Fact]
    public void ValidatePublicKey_WhenRevokedKey_Throws()
    {
        Action act = () => JwtKeyGuard.ValidatePublicKey(RevokedPublicKeySample);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    /// <summary>M-2/M-3 aplicado también al lado público: prueba la normalización, no solo el "no lanza".</summary>
    [Fact]
    public void ValidatePublicKey_WhenValidKeyHasWhitespace_ReturnsCanonicalBase64()
    {
        using var rsa = RSA.Create(2048);
        var der = rsa.ExportSubjectPublicKeyInfo();
        var canonical = Convert.ToBase64String(der);
        var wrapped = "  \n" + WrapAt64Columns(canonical) + "\n  ";

        var result = JwtKeyGuard.ValidatePublicKey(wrapped);

        result.Should().Be(canonical);
    }
}
