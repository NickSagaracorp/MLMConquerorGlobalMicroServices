namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;

public record CompanyInfoDto(
    int     Id,
    string  CompanyName,
    string? CompanyLegalId,
    string? Address,
    string? Phone,
    string  SupportEmail,
    string? PresidentName,
    string? WebsiteUrl,
    string? LogoUrl,
    DateTime CreationDate,
    DateTime? LastUpdateDate);
