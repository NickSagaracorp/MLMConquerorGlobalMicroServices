using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel;
using MLMConquerorGlobalEdition.SignupAPI.DTOs.Auth;

namespace MLMConquerorGlobalEdition.SignupAPI.Features.Auth.Queries.PersonalData;

/// <summary>
/// Los datos que el sistema guarda de la cuenta del usuario autenticado, para consultarlos o
/// descargarlos.
/// </summary>
/// <remarks>
/// <para>
/// La respuesta se construye <b>campo a campo</b>, con una lista explícita escrita a mano. No se
/// serializa <c>ApplicationUser</c> ni <c>MemberProfile</c> para después quitar lo que sobra: con
/// lista de exclusión, la columna sensible que alguien añada mañana a cualquiera de esas dos
/// entidades saldría sola por aquí sin que nadie tocara este archivo. Con lista explícita no sale
/// hasta que alguien la escriba abajo a conciencia.
/// </para>
/// <para>
/// Fuera, y adrede: el hash de la contraseña, el token de refresco y su caducidad, la clave del
/// autenticador (vive en <c>AspNetUserTokens</c> y este handler ni la consulta), el teléfono
/// cifrado, el <c>SecurityStamp</c>, el <c>ConcurrencyStamp</c> y las columnas cifradas de
/// identidad fiscal del perfil. Nada de eso es dato del usuario: es material con el que se
/// suplanta la cuenta, y saldría por un camino autenticado que no levanta ninguna sospecha.
/// </para>
/// </remarks>
public class GetPersonalDataHandler : IRequestHandler<GetPersonalDataQuery, Result<PersonalDataResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext                 _db;

    public GetPersonalDataHandler(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db          = db;
    }

    public async Task<Result<PersonalDataResponse>> Handle(GetPersonalDataQuery query, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(query.UserId);
        if (user is null || !user.IsActive)
            return Result<PersonalDataResponse>.Failure("USER_NOT_FOUND", "User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        var response = new PersonalDataResponse
        {
            UserId         = user.Id,
            UserName       = user.UserName,
            Email          = user.Email,
            EmailConfirmed = user.EmailConfirmed,

            // Solo los cuatro últimos dígitos. El número entero está cifrado y no se descifra:
            // es un factor de autenticación, no un dato de contacto más.
            MaskedTwoFactorPhone    = AccountMasking.MaskPhoneFromLast4(user.TwoFactorPhoneLast4),
            TwoFactorPhoneConfirmed = user.TwoFactorPhoneConfirmed,

            TwoFactorEnabled          = user.TwoFactorEnabled,
            PreferredTwoFactorChannel = user.PreferredTwoFactorChannel,
            TwoFactorEnrolledAt       = user.TwoFactorEnrolledAt,

            IsActive        = user.IsActive,
            CreationDate    = user.CreationDate,
            CreatedBy       = user.CreatedBy,
            LastLoginAt     = user.LastLoginAt,
            MemberProfileId = user.MemberProfileId,

            Roles = roles.ToList()
        };

        // El personal interno no tiene perfil de miembro: MemberProfileId es null y la respuesta
        // se queda solo con los datos de la cuenta.
        if (!string.IsNullOrEmpty(user.MemberProfileId))
        {
            var profile = await _db.MemberProfiles.AsNoTracking()
                .FirstOrDefaultAsync(m => m.MemberId == user.MemberProfileId, ct);

            if (profile is not null)
                response.MemberProfile = new PersonalDataMemberProfile
                {
                    MemberId    = profile.MemberId,
                    Email       = profile.Email,
                    FirstName   = profile.FirstName,
                    LastName    = profile.LastName,
                    DateOfBirth = profile.DateOfBirth,

                    Phone    = profile.Phone,
                    WhatsApp = profile.WhatsApp,

                    Country = profile.Country,
                    State   = profile.State,
                    City    = profile.City,
                    Address = profile.Address,
                    ZipCode = profile.ZipCode,

                    BusinessName     = profile.BusinessName,
                    ShowBusinessName = profile.ShowBusinessName,

                    MemberType        = profile.MemberType,
                    Status            = profile.Status,
                    EnrollDate        = profile.EnrollDate,
                    SponsorMemberId   = profile.SponsorMemberId,
                    ReplicateSiteSlug = profile.ReplicateSiteSlug,
                    ProfilePhotoUrl   = profile.ProfilePhotoUrl,
                    DefaultLanguage   = profile.DefaultLanguage,
                    PayoutFrequency   = profile.PayoutFrequency,

                    IsNamePublic  = profile.IsNamePublic,
                    IsEmailPublic = profile.IsEmailPublic,
                    IsPhonePublic = profile.IsPhonePublic,

                    CreationDate   = profile.CreationDate,
                    LastUpdateDate = profile.LastUpdateDate

                    // SsnEncrypted / EinEncrypted quedan fuera a propósito: la identidad fiscal se
                    // guarda cifrada y en el resto del sistema solo se descifran sus cuatro últimos
                    // dígitos. Devolverla entera aquí sería justo la exposición que ese cifrado
                    // evita. RowVersion tampoco va: es el testigo de concurrencia de la fila.
                };
        }

        return Result<PersonalDataResponse>.Success(response);
    }
}
