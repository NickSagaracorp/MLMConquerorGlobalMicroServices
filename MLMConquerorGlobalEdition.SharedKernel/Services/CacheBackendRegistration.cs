namespace MLMConquerorGlobalEdition.SharedKernel.Services;

/// <summary>
/// Reports which IDistributedCache backend the API selected at startup.
/// Registered as a singleton during the Redis probe so the /health/cache
/// endpoint and any diagnostic surface can introspect it without re-probing
/// Redis on every health check.
/// </summary>
public class CacheBackendInfo
{
    /// <summary>"Redis" or "Memory".</summary>
    public string Backend { get; init; } = "Memory";

    /// <summary>For Redis, the connection string. For memory, "in-process".</summary>
    public string ConnectionHint { get; init; } = "in-process";

    /// <summary>"Required" or "Optional" — what was requested via Cache:Mode config.</summary>
    public string Mode { get; init; } = "Optional";

    /// <summary>True if the active backend is in-process memory.</summary>
    public bool IsMemoryFallback => Backend == "Memory";
}
