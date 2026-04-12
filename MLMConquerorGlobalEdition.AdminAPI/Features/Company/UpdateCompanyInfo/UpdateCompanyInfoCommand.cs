using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Company.UpdateCompanyInfo;

public record UpdateCompanyInfoCommand(
    string  CompanyName,
    string? CompanyLegalId,
    string? Address,
    string? Phone,
    string  SupportEmail,
    string? PresidentName,
    string? WebsiteUrl,
    string? LogoUrl) : IRequest<Result<CompanyInfoDto>>;
