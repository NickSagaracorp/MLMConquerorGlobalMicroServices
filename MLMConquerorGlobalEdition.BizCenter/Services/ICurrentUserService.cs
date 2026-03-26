using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.BizCenter.Services;

// Re-export the SharedKernel interface so local code can reference it from this namespace
public interface ICurrentUserService : MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService
{
}
