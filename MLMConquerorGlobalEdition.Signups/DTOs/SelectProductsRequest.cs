namespace MLMConquerorGlobalEdition.Signups.DTOs;

/// <summary>Phase 2 of the signup wizard — product selection.</summary>
public class SelectProductsRequest
{
    public List<string> ProductIds { get; set; } = new();
}
