namespace MLMConquerorGlobalEdition.BizCenter.Services;

// Local alias — extends SharedKernel interface, adds UtcNow for timezone-safe access
public interface IDateTimeProvider : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
{
    DateTime UtcNow { get; }
}
