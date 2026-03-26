namespace MLMConquerorGlobalEdition.Domain.Interfaces;

public interface IAuditable
{
    string CreatedBy { get; set; }
    string? LastUpdateBy { get; set; }
}
