using MediatR;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Company;
using MLMConquerorGlobalEdition.Domain.Entities.General;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;
using ICurrentUserService = MLMConquerorGlobalEdition.SharedKernel.Interfaces.ICurrentUserService;
using IDateTimeProvider   = MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider;

namespace MLMConquerorGlobalEdition.AdminAPI.Features.Company.UpdateCompanyInfo;

public class UpdateCompanyInfoHandler : IRequestHandler<UpdateCompanyInfoCommand, Result<CompanyInfoDto>>
{
    private readonly AppDbContext        _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider   _dateTime;

    public UpdateCompanyInfoHandler(
        AppDbContext db,
        ICurrentUserService currentUser,
        IDateTimeProvider dateTime)
    {
        _db          = db;
        _currentUser = currentUser;
        _dateTime    = dateTime;
    }

    public async Task<Result<CompanyInfoDto>> Handle(UpdateCompanyInfoCommand cmd, CancellationToken ct)
    {
        var now     = _dateTime.Now;
        var company = await _db.CompanyInfo.FirstOrDefaultAsync(ct);

        if (company is null)
        {
            // First-time setup — create the singleton row
            company = new CompanyInfo
            {
                CompanyName    = cmd.CompanyName,
                CompanyLegalId = cmd.CompanyLegalId,
                Address        = cmd.Address,
                Phone          = cmd.Phone,
                SupportEmail   = cmd.SupportEmail,
                PresidentName  = cmd.PresidentName,
                WebsiteUrl     = cmd.WebsiteUrl,
                LogoUrl        = cmd.LogoUrl,
                CreationDate   = now,
                CreatedBy      = _currentUser.UserId,
                LastUpdateDate = now,
                LastUpdateBy   = _currentUser.UserId
            };
            await _db.CompanyInfo.AddAsync(company, ct);
        }
        else
        {
            company.CompanyName    = cmd.CompanyName;
            company.CompanyLegalId = cmd.CompanyLegalId;
            company.Address        = cmd.Address;
            company.Phone          = cmd.Phone;
            company.SupportEmail   = cmd.SupportEmail;
            company.PresidentName  = cmd.PresidentName;
            company.WebsiteUrl     = cmd.WebsiteUrl;
            company.LogoUrl        = cmd.LogoUrl;
            company.LastUpdateDate = now;
            company.LastUpdateBy   = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(ct);

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
