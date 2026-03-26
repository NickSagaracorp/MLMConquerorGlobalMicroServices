using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.AdminAPI.Features.Impersonation.Commands.StopImpersonation;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Impersonation;

public class StopImpersonationHandlerTests
{
    private static Mock<ICurrentUserService> CurrentUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.Setup(u => u.UserId).Returns("admin-001");
        return m;
    }

    [Fact]
    public async Task Handle_AlwaysReturnsSuccess()
    {
        var handler = new StopImpersonationHandler(
            CurrentUser().Object,
            new Mock<ILogger<StopImpersonationHandler>>().Object);

        var result = await handler.Handle(
            new StopImpersonationCommand("admin-001"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IsStateless_DoesNotThrowForAnyAdminUserId()
    {
        var handler = new StopImpersonationHandler(
            CurrentUser().Object,
            new Mock<ILogger<StopImpersonationHandler>>().Object);

        var act = async () => await handler.Handle(
            new StopImpersonationCommand("some-other-admin"),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
