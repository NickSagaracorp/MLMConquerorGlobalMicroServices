using Microsoft.AspNetCore.Identity;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;

public static class UserManagerHelper
{
    public static Mock<UserManager<ApplicationUser>> Create()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var hasher = new Mock<IPasswordHasher<ApplicationUser>>();

        var manager = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null,   // IOptions<IdentityOptions>
            hasher.Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            null,   // ILookupNormalizer
            null,   // IdentityErrorDescriber
            null,   // IServiceProvider
            null);  // ILogger

        return manager;
    }
}
