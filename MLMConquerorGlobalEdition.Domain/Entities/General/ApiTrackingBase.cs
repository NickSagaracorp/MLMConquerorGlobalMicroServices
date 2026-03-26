namespace MLMConquerorGlobalEdition.Domain.Entities.General;

public abstract class ApiTrackingBase
{
    public long Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? RequestPayload { get; set; }
    public string? ResponsePayload { get; set; }
    public int HttpStatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreationDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
