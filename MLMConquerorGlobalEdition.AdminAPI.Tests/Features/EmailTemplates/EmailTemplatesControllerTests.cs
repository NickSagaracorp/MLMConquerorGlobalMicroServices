using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Email;
using MLMConquerorGlobalEdition.SharedKernel;
using ICacheService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICacheService;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.EmailTemplates;

public class EmailTemplatesControllerTests
{
    private static Mock<ICacheService> CacheMock()
    {
        var m = new Mock<ICacheService>();
        m.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static EmailTemplatesController CreateController(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        Mock<ICacheService> cache)
    {
        var controller = new EmailTemplatesController(db, cache.Object);
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "test-admin") },
            "TestAuth"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private static async Task<EmailTemplate> SeedTemplate(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db,
        string name = "Welcome Email",
        string eventType = "MEMBER_WELCOME",
        string category = "Onboarding",
        bool isActive = true)
    {
        var template = new EmailTemplate
        {
            Name = name,
            EventType = eventType,
            Category = category,
            IsActive = isActive,
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        };
        await db.EmailTemplates.AddAsync(template);
        await db.SaveChangesAsync();
        return template;
    }

    // ── GetAll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenTemplatesExist_ReturnsPaged()
    {
        await using var db = InMemoryDbHelper.Create();
        await SeedTemplate(db, "Template A", "EVENT_A", "Cat1");
        await SeedTemplate(db, "Template B", "EVENT_B", "Cat2");
        var controller = CreateController(db, CacheMock());

        var result = await controller.GetAll(new PagedRequest { Page = 1, PageSize = 20 });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<PagedResult<EmailTemplatesController.EmailTemplateListDto>>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.TotalCount.Should().Be(2);
        response.Data.Items.Should().HaveCount(2);
    }

    // ── GetById ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());

        var result = await controller.GetById(999);

        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = (NotFoundObjectResult)result;
        var response = notFound.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.ErrorCode.Should().Be("EMAIL_TEMPLATE_NOT_FOUND");
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsDetailWithLocalizationsAndVariables()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        await db.EmailTemplateLocalizations.AddAsync(new EmailTemplateLocalization
        {
            EmailTemplateId = template.Id,
            LanguageCode = "en",
            Subject = "Welcome!",
            HtmlBody = "<p>Hello</p>",
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        });
        await db.EmailTemplateVariables.AddAsync(new EmailTemplateVariable
        {
            EmailTemplateId = template.Id,
            Name = "FirstName",
            IsRequired = true,
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, CacheMock());

        var result = await controller.GetById(template.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ApiResponse<EmailTemplatesController.EmailTemplateDetailDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data!.Localizations.Should().HaveCount(1);
        response.Data.Variables.Should().HaveCount(1);
        response.Data.Variables[0].Name.Should().Be("FirstName");
    }

    // ── Create ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithVariables_CreatesTemplateAndVariables()
    {
        await using var db = InMemoryDbHelper.Create();
        var cache = CacheMock();
        var controller = CreateController(db, cache);

        var dto = new EmailTemplatesController.CreateEmailTemplateDto(
            "Order Confirm",
            "order_confirm",
            "Orders",
            "Sent when order is confirmed",
            new List<EmailTemplatesController.VariableFormDto>
            {
                new("OrderId", "The order ID", true),
                new("CustomerName", null, false)
            });

        var result = await controller.Create(dto);

        result.Should().BeOfType<CreatedAtActionResult>();
        db.EmailTemplates.Should().HaveCount(1);
        db.EmailTemplateVariables.Should().HaveCount(2);

        var saved = db.EmailTemplates.First();
        saved.EventType.Should().Be("ORDER_CONFIRM"); // uppercased
        saved.IsActive.Should().BeTrue();
        saved.CreatedBy.Should().Be("test-admin");
    }

    // ── Update ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var controller = CreateController(db, CacheMock());

        var result = await controller.Update(999,
            new EmailTemplatesController.UpdateEmailTemplateDto("Name", "EVT", "Cat", null, true));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_WhenFound_UpdatesFields()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var controller = CreateController(db, CacheMock());

        var result = await controller.Update(template.Id,
            new EmailTemplatesController.UpdateEmailTemplateDto(
                "Updated Name", "NEW_EVENT", "NewCategory", "New description", false));

        result.Should().BeOfType<OkObjectResult>();
        var updated = db.EmailTemplates.First();
        updated.Name.Should().Be("Updated Name");
        updated.EventType.Should().Be("NEW_EVENT");
        updated.IsActive.Should().BeFalse();
        updated.LastUpdateBy.Should().Be("test-admin");
    }

    // ── Delete (deactivate) ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenFound_DeactivatesTemplate()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var controller = CreateController(db, CacheMock());

        var result = await controller.Delete(template.Id);

        result.Should().BeOfType<OkObjectResult>();
        db.EmailTemplates.First().IsActive.Should().BeFalse();
    }

    // ── UpsertLocalization ────────────────────────────────────────────────

    [Fact]
    public async Task UpsertLocalization_WhenNew_AddsLocalization()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var controller = CreateController(db, CacheMock());

        var result = await controller.UpsertLocalization(template.Id,
            new EmailTemplatesController.UpsertLocalizationDto("en", "Subject EN", "<p>Body</p>", "Plain body"));

        result.Should().BeOfType<OkObjectResult>();
        db.EmailTemplateLocalizations.Should().HaveCount(1);
        var loc = db.EmailTemplateLocalizations.First();
        loc.LanguageCode.Should().Be("en");
        loc.Subject.Should().Be("Subject EN");
    }

    [Fact]
    public async Task UpsertLocalization_WhenExisting_UpdatesLocalization()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        await db.EmailTemplateLocalizations.AddAsync(new EmailTemplateLocalization
        {
            EmailTemplateId = template.Id,
            LanguageCode = "en",
            Subject = "Old Subject",
            HtmlBody = "<p>Old</p>",
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, CacheMock());

        await controller.UpsertLocalization(template.Id,
            new EmailTemplatesController.UpsertLocalizationDto("en", "New Subject", "<p>New</p>", null));

        db.EmailTemplateLocalizations.Should().HaveCount(1); // still just one
        db.EmailTemplateLocalizations.First().Subject.Should().Be("New Subject");
    }

    // ── DeleteLocalization ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteLocalization_WhenNotFound_ReturnsNotFound()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var controller = CreateController(db, CacheMock());

        var result = await controller.DeleteLocalization(template.Id, 999);

        result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = (NotFoundObjectResult)result;
        var response = notFound.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.ErrorCode.Should().Be("LOCALIZATION_NOT_FOUND");
    }

    // ── AddVariable ───────────────────────────────────────────────────────

    [Fact]
    public async Task AddVariable_WhenDuplicate_ReturnsConflict()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        await db.EmailTemplateVariables.AddAsync(new EmailTemplateVariable
        {
            EmailTemplateId = template.Id,
            Name = "FirstName",
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, CacheMock());

        var result = await controller.AddVariable(template.Id,
            new EmailTemplatesController.VariableFormDto("FirstName", null, false));

        result.Should().BeOfType<ConflictObjectResult>();
        var conflict = (ConflictObjectResult)result;
        var response = conflict.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        response.ErrorCode.Should().Be("DUPLICATE_VARIABLE");
    }

    [Fact]
    public async Task AddVariable_WhenValid_AddsVariable()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var controller = CreateController(db, CacheMock());

        var result = await controller.AddVariable(template.Id,
            new EmailTemplatesController.VariableFormDto("OrderId", "The order identifier", true));

        result.Should().BeOfType<OkObjectResult>();
        db.EmailTemplateVariables.Should().HaveCount(1);
        var v = db.EmailTemplateVariables.First();
        v.Name.Should().Be("OrderId");
        v.IsRequired.Should().BeTrue();
    }

    // ── DeleteVariable ────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteVariable_WhenFound_RemovesVariable()
    {
        await using var db = InMemoryDbHelper.Create();
        var template = await SeedTemplate(db);
        var variable = new EmailTemplateVariable
        {
            EmailTemplateId = template.Id,
            Name = "Token",
            CreatedBy = "seed",
            CreationDate = DateTime.UtcNow
        };
        await db.EmailTemplateVariables.AddAsync(variable);
        await db.SaveChangesAsync();
        var controller = CreateController(db, CacheMock());

        var result = await controller.DeleteVariable(template.Id, variable.Id);

        result.Should().BeOfType<OkObjectResult>();
        db.EmailTemplateVariables.Should().BeEmpty();
    }
}
