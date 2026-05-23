using FluentValidation.TestHelper;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Controllers.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Commissions.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporateEvents.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.CorporatePromos.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.GhostPoints.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.MembershipLevels.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Placement.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.ProductCommissions.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Products;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Products.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Ranks.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TicketAdmin.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenTypeCommissions.Validators;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Tokens.Validators;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Validators;

public class AdminLoginRequestValidatorTests
{
    private readonly AdminLoginRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new AuthController.LoginRequest("a@b.com", "anything"))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmailMalformed_Fails()
        => _v.TestValidate(new AuthController.LoginRequest("nope", "x"))
            .ShouldHaveValidationErrorFor(x => x.Email);

    [Fact]
    public void Validate_WhenPasswordEmpty_Fails()
        => _v.TestValidate(new AuthController.LoginRequest("a@b.com", ""))
            .ShouldHaveValidationErrorFor(x => x.Password);
}

public class CreateCommissionRequestValidatorTests
{
    private readonly CreateCommissionRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateCommissionRequest
        {
            BeneficiaryMemberId = "AMB-000001",
            CommissionTypeId = 1,
            Amount = 100.50m,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenMemberIdMalformed_Fails()
        => _v.TestValidate(new CreateCommissionRequest
        {
            BeneficiaryMemberId = "<script>",
            CommissionTypeId = 1,
            Amount = 100m,
        }).ShouldHaveValidationErrorFor(x => x.BeneficiaryMemberId);

    [Fact]
    public void Validate_WhenAmountNegative_Fails()
        => _v.TestValidate(new CreateCommissionRequest
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId = 1,
            Amount = -1m,
        }).ShouldHaveValidationErrorFor(x => x.Amount);

    [Fact]
    public void Validate_WhenNotesContainsInjection_Fails()
        => _v.TestValidate(new CreateCommissionRequest
        {
            BeneficiaryMemberId = "AMB-001",
            CommissionTypeId = 1,
            Amount = 100m,
            Notes = "DROP TABLE;",
        }).ShouldHaveValidationErrorFor(x => x.Notes);
}

public class PayCommissionsRequestValidatorTests
{
    private readonly PayCommissionsRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new PayCommissionsRequest { CommissionIds = new() { System.Guid.NewGuid().ToString() } })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEmpty_Fails()
        => _v.TestValidate(new PayCommissionsRequest { CommissionIds = new() })
            .ShouldHaveValidationErrorFor(x => x.CommissionIds);

    [Fact]
    public void Validate_WhenIdMalformed_Fails()
        => _v.TestValidate(new PayCommissionsRequest { CommissionIds = new() { "bad" } })
            .ShouldHaveValidationErrorFor("CommissionIds[0]");
}

public class CreateCommissionCategoryDtoValidatorTests
{
    private readonly CreateCommissionCategoryDtoValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateCommissionCategoryDto { Name = "FastStart" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNameEmpty_Fails()
        => _v.TestValidate(new CreateCommissionCategoryDto { Name = "" })
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Validate_WhenDescriptionContainsInjection_Fails()
        => _v.TestValidate(new CreateCommissionCategoryDto { Name = "X", Description = "<script>" })
            .ShouldHaveValidationErrorFor(x => x.Description);
}

public class CorporateEventValidatorTests
{
    [Fact]
    public void CreateCorporateEvent_WhenValid_Passes()
        => new CreateCorporateEventRequestValidator().TestValidate(new CreateCorporateEventRequest
        {
            Title = "Annual Convention",
            EventDate = DateTime.UtcNow.AddDays(30),
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CreateCorporateEvent_WhenTitleEmpty_Fails()
        => new CreateCorporateEventRequestValidator().TestValidate(new CreateCorporateEventRequest
        {
            Title = "",
            EventDate = DateTime.UtcNow,
        }).ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void CreateCorporateEvent_WhenImageUrlMalformed_Fails()
        => new CreateCorporateEventRequestValidator().TestValidate(new CreateCorporateEventRequest
        {
            Title = "X",
            EventDate = DateTime.UtcNow,
            ImageUrl = "javascript:alert(1)",
        }).ShouldHaveValidationErrorFor(x => x.ImageUrl);
}

public class CreateCorporatePromoRequestValidatorTests
{
    private readonly CreateCorporatePromoRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateCorporatePromoRequest
        {
            Title = "Spring Promo",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenEndBeforeStart_Fails()
        => _v.TestValidate(new CreateCorporatePromoRequest
        {
            Title = "Bad",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1),
        }).ShouldHaveValidationErrorFor(x => x.EndDate);
}

public class UpsertPromoProductCommissionRequestValidatorTests
{
    private readonly UpsertPromoProductCommissionRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpsertPromoProductCommissionRequest { ProductId = System.Guid.NewGuid().ToString() })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenProductIdMalformed_Fails()
        => _v.TestValidate(new UpsertPromoProductCommissionRequest { ProductId = "bad" })
            .ShouldHaveValidationErrorFor(x => x.ProductId);
}

public class CreateGhostPointRequestValidatorTests
{
    private readonly CreateGhostPointRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateGhostPointRequest
        {
            MemberId = "AMB-001",
            LegMemberId = "AMB-002",
            Points = 10,
            Side = TreeSide.Left,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenPointsZero_Fails()
        => _v.TestValidate(new CreateGhostPointRequest
        {
            MemberId = "AMB-001",
            LegMemberId = "AMB-002",
            Points = 0,
            Side = TreeSide.Left,
        }).ShouldHaveValidationErrorFor(x => x.Points);

    [Fact]
    public void Validate_WhenMemberIdInjection_Fails()
        => _v.TestValidate(new CreateGhostPointRequest
        {
            MemberId = "AMB-001'; DROP",
            LegMemberId = "AMB-002",
            Points = 1,
            Side = TreeSide.Left,
        }).ShouldHaveValidationErrorFor(x => x.MemberId);
}

public class UpdateMemberStatusRequestValidatorTests
{
    private readonly UpdateMemberStatusRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new UpdateMemberStatusRequest { Status = MLMConquerorGlobalEdition.Domain.Entities.Member.MemberAccountStatus.Active })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenStatusOutOfRange_Fails()
        => _v.TestValidate(new UpdateMemberStatusRequest { Status = (MLMConquerorGlobalEdition.Domain.Entities.Member.MemberAccountStatus)999 })
            .ShouldHaveValidationErrorFor(x => x.Status);

    [Fact]
    public void Validate_WhenReasonContainsInjection_Fails()
        => _v.TestValidate(new UpdateMemberStatusRequest
        {
            Status = MLMConquerorGlobalEdition.Domain.Entities.Member.MemberAccountStatus.Active,
            Reason = "<script>",
        }).ShouldHaveValidationErrorFor(x => x.Reason);
}

public class CreateMembershipLevelDtoValidatorTests
{
    private readonly CreateMembershipLevelDtoValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateMembershipLevelDto
        {
            Name = "Gold", Price = 99.99m, RenewalPrice = 79.99m, SortOrder = 5,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenPriceNegative_Fails()
        => _v.TestValidate(new CreateMembershipLevelDto { Name = "X", Price = -1m })
            .ShouldHaveValidationErrorFor(x => x.Price);

    [Fact]
    public void Validate_WhenNameTooLong_Fails()
        => _v.TestValidate(new CreateMembershipLevelDto { Name = new string('a', 150) })
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public class AdminPlaceMemberRequestValidatorTests
{
    private readonly AdminPlaceMemberRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new AdminPlaceMemberRequest
        {
            MemberToPlaceId = "AMB-001",
            TargetParentMemberId = "ROOT001",
            Side = "Left",
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenSideMiddle_Fails()
        => _v.TestValidate(new AdminPlaceMemberRequest
        {
            MemberToPlaceId = "AMB-001",
            TargetParentMemberId = "AMB-002",
            Side = "Middle",
        }).ShouldHaveValidationErrorFor(x => x.Side);
}

public class CreateProductCommissionRequestValidatorTests
{
    private readonly CreateProductCommissionRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateProductCommissionRequest { ProductId = System.Guid.NewGuid().ToString() })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenProductIdMalformed_Fails()
        => _v.TestValidate(new CreateProductCommissionRequest { ProductId = "bad" })
            .ShouldHaveValidationErrorFor(x => x.ProductId);
}

public class CreateProductDtoValidatorTests
{
    private readonly CreateProductDtoValidator _v = new();

    private static CreateProductDto Valid() => new()
    {
        Name = "Premium",
        Description = "Premium product description.",
        ImageUrl = "https://cdn.example.com/img.png",
        MonthlyFee = 9.99m,
        SetupFee = 0m,
        Price90Days = 25m,
        Price180Days = 49m,
        AnnualPrice = 99m,
        MonthlyFeePromo = 4.99m,
        SetupFeePromo = 0m,
    };

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenImageUrlMalformed_Fails()
    {
        var p = Valid(); p.ImageUrl = "ftp://nope";
        _v.TestValidate(p).ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public void Validate_WhenNameContainsInjection_Fails()
    {
        var p = Valid(); p.Name = "<x>";
        _v.TestValidate(p).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenMonthlyFeeNegative_Fails()
    {
        var p = Valid(); p.MonthlyFee = -1m;
        _v.TestValidate(p).ShouldHaveValidationErrorFor(x => x.MonthlyFee);
    }
}

public class CreateRankDefinitionDtoValidatorTests
{
    private readonly CreateRankDefinitionDtoValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateRankDefinitionDto { Name = "Bronze" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNameContainsInjection_Fails()
        => _v.TestValidate(new CreateRankDefinitionDto { Name = "Bronze<x>" })
            .ShouldHaveValidationErrorFor(x => x.Name);
}

public class UpdateRankDefinitionDtoValidatorTests
{
    private readonly UpdateRankDefinitionDtoValidator _v = new();

    [Fact]
    public void Validate_WhenStatusOutOfWhitelist_Fails()
        => _v.TestValidate(new UpdateRankDefinitionDto { Name = "X", Status = "Bogus" })
            .ShouldHaveValidationErrorFor(x => x.Status);

    [Fact]
    public void Validate_WhenStatusActive_Passes()
        => _v.TestValidate(new UpdateRankDefinitionDto { Name = "X", Status = "Active" })
            .ShouldNotHaveValidationErrorFor(x => x.Status);
}

public class CreateRankRequirementDtoValidatorTests
{
    private readonly CreateRankRequirementDtoValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateRankRequirementDto
        {
            RankDefinitionId = 1,
            RankDescription = "x",
            CurrentRankDescription = "y",
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenRankDefinitionIdZero_Fails()
        => _v.TestValidate(new CreateRankRequirementDto
        {
            RankDefinitionId = 0,
            RankDescription = "x",
            CurrentRankDescription = "y",
        }).ShouldHaveValidationErrorFor(x => x.RankDefinitionId);

    [Fact]
    public void Validate_WhenMaxTeamPointsPerBranchAbove1_Fails()
        => _v.TestValidate(new CreateRankRequirementDto
        {
            RankDefinitionId = 1,
            RankDescription = "x",
            CurrentRankDescription = "y",
            MaxTeamPointsPerBranch = 1.5d,
        }).ShouldHaveValidationErrorFor(x => x.MaxTeamPointsPerBranch);
}

public class AdminTicketValidatorTests
{
    [Fact]
    public void AddComment_WhenValid_Passes()
        => new AdminAddCommentRequestValidator().TestValidate(
            new AdminAddCommentRequest { Content = "Looking into this." })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void AddComment_WhenContentEmpty_Fails()
        => new AdminAddCommentRequestValidator().TestValidate(
            new AdminAddCommentRequest { Content = "" })
            .ShouldHaveValidationErrorFor(x => x.Content);

    [Fact]
    public void Assign_WhenUserIdInjection_Fails()
        => new AdminAssignTicketRequestValidator().TestValidate(
            new AdminAssignTicketRequest { AssignedToUserId = "<x>" })
            .ShouldHaveValidationErrorFor(x => x.AssignedToUserId);

    [Fact]
    public void Create_WhenValid_Passes()
        => new AdminCreateTicketRequestValidator().TestValidate(
            new AdminCreateTicketRequest
            {
                MemberId = "AMB-001",
                Subject = "Issue",
                Body = "Details",
                Priority = TicketPriority.High,
            }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Create_WhenSubjectHasAngles_Fails()
        => new AdminCreateTicketRequestValidator().TestValidate(
            new AdminCreateTicketRequest
            {
                MemberId = "AMB-001",
                Subject = "<script>",
                Body = "Details",
            }).ShouldHaveValidationErrorFor(x => x.Subject);

    [Fact]
    public void Resolve_WhenEmpty_Passes()
        => new AdminResolveTicketRequestValidator().TestValidate(
            new AdminResolveTicketRequest { ResolutionNotes = null })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Update_WhenPriorityOutOfEnum_Fails()
        => new AdminUpdateTicketRequestValidator().TestValidate(
            new AdminUpdateTicketRequest { Priority = (TicketPriority)999 })
            .ShouldHaveValidationErrorFor(x => x.Priority);
}

public class AdminGrantTokenRequestValidatorTests
{
    private readonly AdminGrantTokenRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new AdminGrantTokenRequest
        {
            MemberId = "AMB-001", TokenTypeId = 1, Quantity = 5,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenQuantityZero_Fails()
        => _v.TestValidate(new AdminGrantTokenRequest
        {
            MemberId = "AMB-001", TokenTypeId = 1, Quantity = 0,
        }).ShouldHaveValidationErrorFor(x => x.Quantity);

    [Fact]
    public void Validate_WhenQuantityTooLarge_Fails()
        => _v.TestValidate(new AdminGrantTokenRequest
        {
            MemberId = "AMB-001", TokenTypeId = 1, Quantity = 1_000_000,
        }).ShouldHaveValidationErrorFor(x => x.Quantity);
}

public class AdminUpdateTokenBalanceRequestValidatorTests
{
    private readonly AdminUpdateTokenBalanceRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new AdminUpdateTokenBalanceRequest { Balance = 100 })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenNegative_Fails()
        => _v.TestValidate(new AdminUpdateTokenBalanceRequest { Balance = -1 })
            .ShouldHaveValidationErrorFor(x => x.Balance);
}

public class CreateTokenTypeCommissionRequestValidatorTests
{
    private readonly CreateTokenTypeCommissionRequestValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateTokenTypeCommissionRequest
        {
            TokenTypeId = 1, CommissionTypeId = 1, CommissionPerToken = 1.5m,
        }).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenTokenTypeIdZero_Fails()
        => _v.TestValidate(new CreateTokenTypeCommissionRequest
        {
            TokenTypeId = 0, CommissionTypeId = 1, CommissionPerToken = 1m,
        }).ShouldHaveValidationErrorFor(x => x.TokenTypeId);
}

public class CreateTokenTypeDtoValidatorTests
{
    private readonly CreateTokenTypeDtoValidator _v = new();

    [Fact]
    public void Validate_WhenValid_Passes()
        => _v.TestValidate(new CreateTokenTypeDto { Name = "GuestPass" })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Validate_WhenTemplateUrlMalformed_Fails()
        => _v.TestValidate(new CreateTokenTypeDto { Name = "X", TemplateUrl = "javascript:alert(1)" })
            .ShouldHaveValidationErrorFor(x => x.TemplateUrl);

    [Fact]
    public void Validate_WhenCategoryOutOfEnum_Fails()
        => _v.TestValidate(new CreateTokenTypeDto { Name = "X", Category = (TokenCategory)999 })
            .ShouldHaveValidationErrorFor(x => x.Category);
}
