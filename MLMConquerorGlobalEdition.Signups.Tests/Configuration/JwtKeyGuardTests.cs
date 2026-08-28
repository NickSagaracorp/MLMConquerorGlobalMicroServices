using MLMConquerorGlobalEdition.SharedKernel.Configuration;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Configuration;

public class JwtKeyGuardTests
{
    /// <summary>
    /// Llave privada que estuvo commiteada en texto plano en
    /// MLMConquerorGlobalEdition.SignupAPI/appsettings.json (y también en
    /// MLMConquerorGlobalEdition.AdminAPI/appsettings.json) hasta el 2026-08-27.
    /// Se copia aquí a propósito, literal, para verificar que <see cref="JwtKeyGuard"/>
    /// la rechaza por huella. Esta llave sigue en el historial de git para siempre,
    /// así que se considera comprometida de forma permanente.
    /// </summary>
    private const string RevokedKeySample =
        "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQC4bmDlbzdUGJJPYtJf4o7XCayVP0ml8D6GUaWp8DYWHp2qGnRSKK/W3SpQOk527sR3n0lorN/pDuWn0McP9AaZcwJNr69C0haT59VnzhMf8pjT1FH8aFZMyEMStKBF8eQx8BMsMXBm7Ks04t3NzOpiBywR3drnBU/USJjQ0S9m+CrHod/Wqpc39X37NXF4bA2LI/pd6SLhwe1fsN+IU7SVuJCbn0G3+URCswKCZEL21lIYeZgXPk1cgc91TVJ33T4JDBe9CQIlFkRqCBwKxfvplaiT8VM3j9g5DBPs9ODZKUPgev/ZaaIXGQxLBLEC+z1nWWvgs+UeziL/jMF/Rf9hAgMBAAECggEAE+Pzl0rzUKKFxQIHZkfs18w3TLFSpA7Q73OGxlkdvCz5HAtWWDYDIM0hbx2asMD3d186b4uVanEs23hsv4+11n7M1MwJvs8hmDn8jgFvlpZ3XQEdBnfKNuWyNiY80s5PqgMWTkHWuYL0w/NjsHpVHNVhL1wZ66hq/54EvSCSWCxkhD81HOkGzyd8nlubv2kDOtP+MT+L+/gdu/7XcBItKs6qpZNPq36qBKvFogWsecnXGJc0roT+WsgOnY1kcE1+cUL80Nnj4lhE1NkE1FIGIH3+bGDOJeLZ6vI79TCqfa/zXr05nUusHqy1LHEFcS0rDUb6IwWDUQBzA6mPXY7+cQKBgQDtLwHUi3EZ7AaA/WfjAm1ekul2azz3hBOovwZiRC+XJ4Xbw+E1nzKBQM/vpuGbM8ilo7Dg0Jk6TTmuXXaX43hZwAcTSAv6dxVInNyWVmI1CL0dgBVAvqOTunWbCFeDJeweu0powpS3sxa/yyVhqo/yxsw1wKFQISADEHYx/hTghwKBgQDHEAPQ51NuzV8h3lsr1ZZGhKTRs+CSGinutfE14Wuj14fXmtKTKqgjOwyR/uLDmRmFim6zTT+gwkOWYuKufk13htSym0h8J0Lu3eisMZstN1V3uQyD0InB5N3KxSdNPmDkSYhfp4S/IcUFUpc8uKsssZLiBgHWGFilXOKVnxii1wKBgC1tO2SB8H+OfRBneGY6KMhcSuqrT1n4qes/6vEXLiY9I54bvh6PBxVKXIkB5WPcXymaWF42laJc+Bc1P2mH350Q8kn3GpQ2CpWFtZn1oYmWyuHDjk6ANMQuiifPSTONJ9Qa+v1lhyZH3quNNPOnvzo4aRRCeKLoNUFg/cJFb6oxAoGAfWlYjh9/T/pxafwVwnsQ7yKYWEmUPVfgfTUBX6nCT/n06l/vNKqWPYPxOnWz1fk5vAloDuynHpflTvTDzZ1jHt/CVzg/pYByydzivsGN+yG2ZfQer1kNwSt4lEw7o42eK5UsJt96YZRb9SuAfs/1f/XsDRwtwf2as6veUNdoBQcCgYBEAahv2JzK6Rt3E/3/R4ujdIxz92cwATJK3q18NjCFS0J7Rpm9ObFFlC3n2tIY+HwT+Dh9yglGLoZOsvadSvWOyGRcZ4jTgJbJomfQB+s1gsLm5uK6BGYPSAA50udUr2OQQvxzgXEHy4/lfjg8PeC7cU9DqV1jsPYc2RzoWLLOmw==";

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
        Action act = () => JwtKeyGuard.ValidatePrivateKey(RevokedKeySample);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenRevokedKeyHasSurroundingWhitespace_StillThrows()
    {
        var keyWithWhitespace = "  \n" + RevokedKeySample + "\n  ";

        Action act = () => JwtKeyGuard.ValidatePrivateKey(keyWithWhitespace);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*llave revocada*");
    }

    [Fact]
    public void ValidatePrivateKey_WhenValidKey_ReturnsIt()
    {
        var validKey = Convert.ToBase64String(System.Security.Cryptography.RSA.Create(2048).ExportPkcs8PrivateKey());

        var result = JwtKeyGuard.ValidatePrivateKey(validKey);

        result.Should().Be(validKey);
    }

    [Fact]
    public void ValidatePrivateKey_UsesGivenConfigKeyInMessage()
    {
        Action act = () => JwtKeyGuard.ValidatePrivateKey(null, configKey: "Jwt:OtraLlave");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:OtraLlave*");
    }
}
