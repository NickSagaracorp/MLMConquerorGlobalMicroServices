using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Validators;

namespace MLMConquerorGlobalEdition.Signups.Tests.Validators;

public class CompleteSignupRequestValidatorTests
{
    private readonly CompleteSignupRequestValidator _validator = new();

    private static CreditCardInfoDto ValidCard() => new()
    {
        GatewayToken = "nonce_abc123",
        CardToken    = "tok_def456",
        Last4        = "4242",
        First6       = "424242",
        CardBrand    = "Visa",
        ExpiryMonth  = 12,
        ExpiryYear   = DateTime.UtcNow.Year + 2,
        Gateway      = "stripe",
    };

    [Fact]
    public void Validate_CreditCardPath_WhenComplete_PassesValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard,
            CreditCard = ValidCard(),
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_CreditCardPath_WhenCardNull_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.CreditCard,
            CreditCard = null,
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CreditCard);
    }

    [Fact]
    public void Validate_CryptoPath_WhenCurrencyMissing_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Crypto,
            CheckoutScreenshotContentType = "image/png",
            CryptoCurrency = null,
            CryptoTransactionId = "tx_abc",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CryptoCurrency);
    }

    [Fact]
    public void Validate_CryptoPath_WhenTxIdHasSpecialChars_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Crypto,
            CheckoutScreenshotContentType = "image/png",
            CryptoCurrency = "BTC",
            CryptoTransactionId = "tx_<script>",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CryptoTransactionId);
    }

    [Fact]
    public void Validate_TokenPath_WhenCodeMalformed_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Token,
            TokenCode = "lower-case",
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.TokenCode);
    }

    [Fact]
    public void Validate_TokenPath_WhenCodeValid_Passes()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Token,
            TokenCode = "X4P2A9N",
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldNotHaveValidationErrorFor(x => x.TokenCode);
    }

    [Fact]
    public void Validate_DiscountCodePath_WhenMissing_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.DiscountCode,
            DiscountCode = "",
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.DiscountCode);
    }

    [Fact]
    public void Validate_ScreenshotContentType_WhenNotImage_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Token,
            TokenCode = "ABCDEF",
            CheckoutScreenshotContentType = "text/html",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CheckoutScreenshotContentType);
    }

    [Fact]
    public void Validate_ScreenshotBase64_WhenInvalidCharset_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = PaymentMethodType.Token,
            TokenCode = "ABCDEF",
            CheckoutScreenshotContentType = "image/png",
            CheckoutScreenshotBase64 = "not<base64>",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CheckoutScreenshotBase64);
    }

    [Fact]
    public void Validate_PaymentMethod_WhenOutOfRange_FailsValidation()
    {
        var r = new CompleteSignupRequest
        {
            PaymentMethod = (PaymentMethodType)999,
            CheckoutScreenshotContentType = "image/png",
        };
        _validator.TestValidate(r).ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }
}
