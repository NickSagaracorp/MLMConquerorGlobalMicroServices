using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SharedKernel.Security;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

/// <summary>
/// Los validadores son la primera puerta del step-up: lo que dejan pasar llega al servicio de 2FA,
/// que firma y cuenta intentos. Un código que no son seis dígitos no debe gastar un intento.
/// </summary>
public class StepUpRequestValidatorTests
{
    private readonly StepUpChallengeRequestValidator _challenge = new();
    private readonly StepUpVerifyRequestValidator    _verify    = new();

    private static StepUpVerifyRequest ValidVerify() => new()
    {
        OperationKey   = StepUpOperations.PayoutMethodChange,
        ChallengeToken = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ1In0.firma-_",
        Code           = "123456"
    };

    [Fact]
    public void Reto_valido_pasa() =>
        _challenge.TestValidate(new StepUpChallengeRequest { OperationKey = StepUpOperations.PayoutMethodChange })
                  .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Reto_sin_operacion_no_pasa() =>
        _challenge.TestValidate(new StepUpChallengeRequest { OperationKey = "" })
                  .ShouldHaveValidationErrorFor(x => x.OperationKey);

    [Fact]
    public void Reto_con_operacion_demasiado_larga_no_pasa() =>
        _challenge.TestValidate(new StepUpChallengeRequest { OperationKey = new string('a', 65) })
                  .ShouldHaveValidationErrorFor(x => x.OperationKey);

    [Fact]
    public void Canje_valido_pasa() =>
        _verify.TestValidate(ValidVerify()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 123456")]
    [InlineData("123456\n")]                               // "$" lo dejaba pasar
    [InlineData("١٢٣٤٥٦")] // dígitos arábigo-índicos: "\d" los aceptaba
    public void Canje_con_codigo_que_no_son_seis_digitos_no_pasa(string code)
    {
        var r = ValidVerify(); r.Code = code;
        _verify.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("token con espacios")]
    [InlineData("token<script>")]
    public void Canje_con_reto_vacio_o_con_caracteres_ajenos_a_un_JWT_no_pasa(string token)
    {
        var r = ValidVerify(); r.ChallengeToken = token;
        _verify.TestValidate(r).ShouldHaveValidationErrorFor(x => x.ChallengeToken);
    }

    [Fact]
    public void Canje_con_reto_desmesurado_no_pasa()
    {
        var r = ValidVerify(); r.ChallengeToken = new string('a', 4097);
        _verify.TestValidate(r).ShouldHaveValidationErrorFor(x => x.ChallengeToken);
    }

    [Fact]
    public void Canje_sin_operacion_no_pasa()
    {
        var r = ValidVerify(); r.OperationKey = "";
        _verify.TestValidate(r).ShouldHaveValidationErrorFor(x => x.OperationKey);
    }
}
