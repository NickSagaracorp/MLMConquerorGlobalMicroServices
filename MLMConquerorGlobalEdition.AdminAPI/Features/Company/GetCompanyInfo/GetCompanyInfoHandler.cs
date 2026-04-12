using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Company.GetCompanyInfo;

public class GetCompanyInfoHandler : IRequestHandler<GetCompanyInfoQuery, Result<CompanyInfoDto>>
{
    private readonly AppDbContext _db;

    public GetCompanyInfoHandler(AppDbContext db) => _db = db;

    public async Task<Result<CompanyInfoDto>> Handle(GetCompanyInfoQuery request, CancellationToken ct)
    {
        var company = await _db.CompanyInfo
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (company is null)
            return Result<CompanyInfoDto>.Failure(
                "COMPANY_INFO_NOT_FOUND",
                "Company information has not been configured yet.");

        return Result<CompanyInfoDto>.Success(new CompanyInfoDto(
            company.Id,
            company.CompanyName,
            company.CompanyLegalId,
            company.Address,
            company.Phone,
            company.SupportEmail,
            company.PresidentName,
            company.WebsiteUrl,
            company.LogoUrl,
            company.CreationDate,
            company.LastUpdateDate));
    }
}
