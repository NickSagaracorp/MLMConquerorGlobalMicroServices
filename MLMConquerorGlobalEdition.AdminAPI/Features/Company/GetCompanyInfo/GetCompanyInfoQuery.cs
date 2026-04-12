using MediatR;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Company.GetCompanyInfo;

public record GetCompanyInfoQuery : IRequest<Result<CompanyInfoDto>>;
