namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;

public record CompanyInfoDto(
    int      Id,
    string   CompanyName,
    string?  CompanyLegalId,
    string?  Address,
    string?  Phone,
    string   SupportEmail,
    string?  PresidentName,
    string?  WebsiteUrl,
    string?  LogoUrl,
    /// <summary>Default <c>PayoutFrequency</c> ("Daily" | "Weekly") seeded onto every new ambassador at signup.</summary>
    string   DefaultPayoutFrequency,
    DateTime CreationDate,
    DateTime? LastUpdateDate);
