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
