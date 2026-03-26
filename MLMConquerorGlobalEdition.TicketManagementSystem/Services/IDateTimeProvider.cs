using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

// Re-export the SharedKernel interface so local code can reference it from this namespace
public interface IDateTimeProvider : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
{
}
