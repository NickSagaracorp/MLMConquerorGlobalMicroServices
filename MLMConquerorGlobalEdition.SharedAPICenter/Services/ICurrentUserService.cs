using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.SharedAPICenter.Services;

/// <summary>
/// Local re-export alias so DI registration stays within the project namespace.
/// The actual contract lives in SharedKernel.
/// </summary>
public interface ICurrentUserService : MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService
{
}
