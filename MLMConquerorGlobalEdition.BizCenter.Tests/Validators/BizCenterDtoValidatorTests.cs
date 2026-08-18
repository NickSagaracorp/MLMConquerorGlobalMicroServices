using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Billing.Validators;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Placement.Validators;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Profile.Validators;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tickets.Validators;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Tokens.Validators;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet;
using MLMConquerorGlobalEdition.BizCenter.DTOs.Wallet.Validators;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Validators;

public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdateProfileRequest
        {
            Country = "CA", Phone = "+1 555 555 5555", DefaultLanguage = "en", PayoutFrequency = "Daily",
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenPhoneInvalid_Fails()
        => _v.TestValidate(new UpdateProfileRequest { Phone = "abc" })
            .ShouldHaveValidationErrorFor(x => x.Phone);

    [Fact]
    public void Validate_WhenCountryNotIso2_Fails()
        => _v.TestValidate(new UpdateProfileRequest { Country = "Canada" })
            .ShouldHaveValidationErrorFor(x => x.Country);

    [Fact]
    public void Validate_WhenAddressContainsHtml_Fails()
        => _v.TestValidate(new UpdateProfileRequest { Address = "<script>" })
            .ShouldHaveValidationErrorFor(x => x.Address);

    [Fact]
    public void Validate_WhenPayoutFrequencyBogus_Fails()
        => _v.TestValidate(new UpdateProfileRequest { PayoutFrequency = "Fortnightly" })
            .ShouldHaveValidationErrorFor(x => x.PayoutFrequency);

    // Monthly se agregó en 2026-08 junto a Daily y Weekly. Antes era el valor que este
    // archivo usaba como "bogus", así que conviene cubrir explícitamente que ahora pasa.
    [Theory]
    [InlineData("Daily")]
    [InlineData("Weekly")]
    [InlineData("Monthly")]
    public void Validate_WhenPayoutFrequencySupported_Passes(string frequency)
        => _v.TestValidate(new UpdateProfileRequest { PayoutFrequency = frequency })
            .ShouldNotHaveValidationErrorFor(x => x.PayoutFrequency);

    [Fact]
    public void Validate_WhenLanguageMalformed_Fails()
        => _v.TestValidate(new UpdateProfileRequest { DefaultLanguage = "EN" })
            .ShouldHaveValidationErrorFor(x => x.DefaultLanguage);
}

public class UpdateReplicateSiteRequestValidatorTests
{
    private readonly UpdateReplicateSiteRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdateReplicateSiteRequest { Slug = "alice-doe" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenUppercase_Fails()
        => _v.TestValidate(new UpdateReplicateSiteRequest { Slug = "BadCase" })
            .ShouldHaveValidationErrorFor(x => x.Slug);

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _v.TestValidate(new UpdateReplicateSiteRequest { Slug = "" })
            .ShouldHaveValidationErrorFor(x => x.Slug);
}

public class UpdateEmailRequestValidatorTests
{
    private readonly UpdateEmailRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdateEmailRequest { NewEmail = "a@b.com" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenMalformed_Fails()
        => _v.TestValidate(new UpdateEmailRequest { NewEmail = "<x>" })
            .ShouldHaveValidationErrorFor(x => x.NewEmail);
}

public class UpdatePasswordRequestValidatorTests
{
    private readonly UpdatePasswordRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdatePasswordRequest { CurrentPassword = "old", NewPassword = "P@ssw0rd!" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNewWeak_Fails()
        => _v.TestValidate(new UpdatePasswordRequest { CurrentPassword = "old", NewPassword = "weak" })
            .ShouldHaveValidationErrorFor(x => x.NewPassword);
}

public class UpdatePhotoRequestValidatorTests
{
    private readonly UpdatePhotoRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdatePhotoRequest
        {
            Base64Image = "iVBORw0KGgo=",
            ContentType = "image/png",
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenContentTypeBogus_Fails()
        => _v.TestValidate(new UpdatePhotoRequest
        {
            Base64Image = "iVBORw0KGgo=",
            ContentType = "text/plain",
        }).ShouldHaveValidationErrorFor(x => x.ContentType);

    [Fact]
    public void Validate_WhenBase64Malformed_Fails()
        => _v.TestValidate(new UpdatePhotoRequest
        {
            Base64Image = "<not-base64>",
            ContentType = "image/png",
        }).ShouldHaveValidationErrorFor(x => x.Base64Image);
}

public class UpdateWalletRequestValidatorTests
{
    private readonly UpdateWalletRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdateWalletRequest { WalletType = WalletType.Paypal, AccountIdentifier = "user@example.com" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenAccountIdInjection_Fails()
        => _v.TestValidate(new UpdateWalletRequest { WalletType = WalletType.Paypal, AccountIdentifier = "<script>" })
            .ShouldHaveValidationErrorFor(x => x.AccountIdentifier);

    [Fact]
    public void Validate_WhenWalletTypeOutOfEnum_Fails()
        => _v.TestValidate(new UpdateWalletRequest { WalletType = (WalletType)999 })
            .ShouldHaveValidationErrorFor(x => x.WalletType);
}

public class AddCreditCardRequestValidatorTests
{
    private readonly AddCreditCardRequestValidator _v = new();

    private static AddCreditCardRequest Valid() => new()
    {
        CardNumber     = "4242424242424242",
        CardholderName = "Alice O'Brien",
        ExpiryMonth    = 12,
        ExpiryYear     = DateTime.UtcNow.Year + 2,
        Cvv            = "123",
    };

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenCardNumberHasDashes_Fails()
    {
        var r = Valid(); r.CardNumber = "4242-4242-4242-4242";
        _v.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Fact]
    public void Validate_WhenCardholderHasHtml_Fails()
    {
        var r = Valid(); r.CardholderName = "<x>";
        _v.TestValidate(r).ShouldHaveValidationErrorFor(x => x.CardholderName);
    }

    [Fact]
    public void Validate_WhenCvvWrongLength_Fails()
    {
        var r = Valid(); r.Cvv = "12";
        _v.TestValidate(r).ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Fact]
    public void Validate_WhenExpiryMonthOutOfRange_Fails()
    {
        var r = Valid(); r.ExpiryMonth = 13;
        _v.TestValidate(r).ShouldHaveValidationErrorFor(x => x.ExpiryMonth);
    }
}

public class ReorderCreditCardsRequestValidatorTests
{
    private readonly ReorderCreditCardsRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new ReorderCreditCardsRequest { OrderedCardIds = new() { Guid.NewGuid().ToString() } })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _v.TestValidate(new ReorderCreditCardsRequest { OrderedCardIds = new() })
            .ShouldHaveValidationErrorFor(x => x.OrderedCardIds);

    [Fact]
    public void Validate_WhenIdsMalformed_Fails()
        => _v.TestValidate(new ReorderCreditCardsRequest { OrderedCardIds = new() { "bad" } })
            .ShouldHaveValidationErrorFor("OrderedCardIds[0]");
}

public class PlaceMemberRequestValidatorTests
{
    private readonly PlaceMemberRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new PlaceMemberRequest
        {
            MemberToPlaceId = "AMB-001",
            TargetParentMemberId = "ROOT001",
            Side = "Right",
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenSideMiddle_Fails()
        => _v.TestValidate(new PlaceMemberRequest
        {
            MemberToPlaceId = "AMB-001",
            TargetParentMemberId = "AMB-002",
            Side = "Middle",
        }).ShouldHaveValidationErrorFor(x => x.Side);

    [Fact]
    public void Validate_WhenMemberIdInjection_Fails()
        => _v.TestValidate(new PlaceMemberRequest
        {
            MemberToPlaceId = "<script>",
            TargetParentMemberId = "AMB-002",
            Side = "Left",
        }).ShouldHaveValidationErrorFor(x => x.MemberToPlaceId);
}

public class CreateTicketRequestValidatorTests
{
    private readonly CreateTicketRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateTicketRequest
        {
            Subject = "Help", Body = "Need help", CategoryId = 1, Priority = TicketPriority.Normal,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenSubjectHasAngles_Fails()
        => _v.TestValidate(new CreateTicketRequest
        {
            Subject = "<x>", Body = "x", CategoryId = 1,
        }).ShouldHaveValidationErrorFor(x => x.Subject);

    [Fact]
    public void Validate_WhenCategoryIdZero_Fails()
        => _v.TestValidate(new CreateTicketRequest
        {
            Subject = "x", Body = "x", CategoryId = 0,
        }).ShouldHaveValidationErrorFor(x => x.CategoryId);
}

public class AddCommentRequestValidatorTests
{
    private readonly AddCommentRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new AddCommentRequest { Content = "OK" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _v.TestValidate(new AddCommentRequest { Content = "" })
            .ShouldHaveValidationErrorFor(x => x.Content);
}

public class DistributeTokenRequestValidatorTests
{
    private readonly DistributeTokenRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new DistributeTokenRequest
        {
            TokenTypeId = 1, RecipientMemberId = "AMB-001", Quantity = 5,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenQuantityZero_Fails()
        => _v.TestValidate(new DistributeTokenRequest
        {
            TokenTypeId = 1, RecipientMemberId = "AMB-001", Quantity = 0,
        }).ShouldHaveValidationErrorFor(x => x.Quantity);

    [Fact]
    public void Validate_WhenRecipientInjection_Fails()
        => _v.TestValidate(new DistributeTokenRequest
        {
            TokenTypeId = 1, RecipientMemberId = "<x>", Quantity = 1,
        }).ShouldHaveValidationErrorFor(x => x.RecipientMemberId);
}
