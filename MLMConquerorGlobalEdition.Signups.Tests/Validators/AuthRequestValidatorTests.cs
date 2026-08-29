using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new LoginRequest { Email = "a@b.com", Password = "Whatever" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmailMalformed_Fails()
        => _validator.TestValidate(new LoginRequest { Email = "not-an-email", Password = "x" })
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Validate_WhenPasswordEmpty_Fails()
        => _validator.TestValidate(new LoginRequest { Email = "a@b.com", Password = "" })
            .ShouldHaveValidationErrorFor(x => x.Password);

    [Fact]
    public void Validate_WhenPasswordOversize_Fails()
        => _validator.TestValidate(new LoginRequest { Email = "a@b.com", Password = new string('a', 200) })
            .ShouldHaveValidationErrorFor(x => x.Password);
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "P@ssw0rd!" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNewPasswordWeak_Fails()
        => _validator.TestValidate(new ChangePasswordRequest { CurrentPassword = "old", NewPassword = "weak" })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);

    [Fact]
    public void Validate_WhenCurrentPasswordEmpty_Fails()
        => _validator.TestValidate(new ChangePasswordRequest { CurrentPassword = "", NewPassword = "P@ssw0rd!" })
            .ShouldHaveValidationErrorFor(x => x.CurrentPassword);
}

public class ForgotPasswordRequestValidatorTests
{
    private readonly ForgotPasswordRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ForgotPasswordRequest { Email = "a@b.com" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmailMalformed_Fails()
        => _validator.TestValidate(new ForgotPasswordRequest { Email = "<x>" })
            .ShouldHaveValidationErrorFor(x => x.Email);
}

public class RefreshTokenRequestValidatorTests
{
    private readonly RefreshTokenRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new RefreshTokenRequest { RefreshToken = "abc.def-ghi_jkl=" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _validator.TestValidate(new RefreshTokenRequest { RefreshToken = "" })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);

    [Fact]
    public void Validate_WhenContainsAngleBrackets_Fails()
        => _validator.TestValidate(new RefreshTokenRequest { RefreshToken = "<script>" })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);
}

public class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            Email = "a@b.com",
            Token = "abc.def_ghi",
            NewPassword = "P@ssw0rd!"
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNewPasswordWeak_Fails()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            Email = "a@b.com",
            Token = "abc",
            NewPassword = "weak"
        }).ShouldHaveValidationErrorFor(x => x.NewPassword);

    [Fact]
    public void Validate_WhenTokenContainsAngleBrackets_Fails()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            Email = "a@b.com",
            Token = "<script>",
            NewPassword = "P@ssw0rd!"
        }).ShouldHaveValidationErrorFor(x => x.Token);
}

public class VerifyTwoFactorRequestValidatorTests
{
    private readonly VerifyTwoFactorRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new VerifyTwoFactorRequest { ChallengeToken = "abc.def", Code = "123456" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenCodeWrongLength_Fails()
        => _validator.TestValidate(new VerifyTwoFactorRequest { ChallengeToken = "abc.def", Code = "12" })
            .ShouldHaveValidationErrorFor(x => x.Code);

    [Fact]
    public void Validate_WhenCodeContainsLetters_Fails()
        => _validator.TestValidate(new VerifyTwoFactorRequest { ChallengeToken = "abc.def", Code = "12345a" })
            .ShouldHaveValidationErrorFor(x => x.Code);
}

public class ResendTwoFactorRequestValidatorTests
{
    private readonly ResendTwoFactorRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ResendTwoFactorRequest { ChallengeToken = "abc.def" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _validator.TestValidate(new ResendTwoFactorRequest { ChallengeToken = "" })
            .ShouldHaveValidationErrorFor(x => x.ChallengeToken);
}

/// <summary>
/// El DTO de recuperación acepta dos identificadores porque hay dos clientes con dos contratos:
/// el componente de SharedComponents postea UserId y la pantalla de BizCenterWeb postea Email.
/// El validador tiene que dejar pasar cualquiera de los dos y cortar si no viene ninguno.
/// </summary>
public class ResetPasswordRequestIdentifierValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenOnlyUserIdIsPresent_Passes()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            UserId = "8f14e45f-ceea-467a-9575-28bd3f1b1234",
            Token = "abc.def_ghi",
            NewPassword = "P@ssw0rd!"
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenBothIdentifiersArePresent_Passes()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            UserId = "8f14e45f-ceea-467a-9575-28bd3f1b1234",
            Email = "a@b.com",
            Token = "abc.def_ghi",
            NewPassword = "P@ssw0rd!"
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNeitherIdentifierIsPresent_Fails()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            Token = "abc.def_ghi",
            NewPassword = "P@ssw0rd!"
        }).ShouldHaveValidationErrorFor("UserId");

    [Fact]
    public void Validate_WhenUserIdContainsAngleBrackets_Fails()
        => _validator.TestValidate(new ResetPasswordRequest
        {
            UserId = "<script>",
            Token = "abc.def_ghi",
            NewPassword = "P@ssw0rd!"
        }).ShouldHaveValidationErrorFor(x => x.UserId);
}

public class SetTwoFactorChannelRequestValidatorTests
{
    private readonly SetTwoFactorChannelRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenChannelIsKnown_Passes()
        => _validator.TestValidate(new SetTwoFactorChannelRequest
        {
            Channel = MLMConquerorGlobalEdition.Domain.Entities.Security.TwoFactorChannel.Sms
        }).ShouldNotHaveAnyValidationErrors();

    /// <summary>
    /// El cuerpo es JSON: nadie obliga a quien llama a mandar uno de los tres valores. Que el
    /// canal tenga destino para esta cuenta lo decide el handler; aquí solo se corta el entero
    /// que no es ningún canal.
    /// </summary>
    [Fact]
    public void Validate_WhenChannelIsNotAValidEnumValue_Fails()
        => _validator.TestValidate(new SetTwoFactorChannelRequest
        {
            Channel = (MLMConquerorGlobalEdition.Domain.Entities.Security.TwoFactorChannel)99
        }).ShouldHaveValidationErrorFor(x => x.Channel);
}

public class ConfirmAccountEnrollmentRequestValidatorTests
{
    private readonly ConfirmAccountEnrollmentRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _validator.TestValidate(new ConfirmAccountEnrollmentRequest { Code = "123456" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenCodeWrongLength_Fails()
        => _validator.TestValidate(new ConfirmAccountEnrollmentRequest { Code = "12" })
            .ShouldHaveValidationErrorFor(x => x.Code);
}
