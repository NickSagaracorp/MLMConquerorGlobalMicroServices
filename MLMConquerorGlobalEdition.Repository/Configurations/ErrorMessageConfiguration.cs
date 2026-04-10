using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class ErrorMessageConfiguration : IEntityTypeConfiguration<ErrorMessage>
{
    private static readonly DateTime SeedDate = new(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ErrorMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ErrorCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Language).IsRequired().HasMaxLength(10);
        builder.Property(x => x.UserFriendlyMessage).IsRequired().HasMaxLength(500);
        builder.HasIndex(x => new { x.ErrorCode, x.Language }).IsUnique();

        builder.HasData(

            new ErrorMessage { Id = 1,  ErrorCode = "DEFAULT",                       Language = "en", UserFriendlyMessage = "Something went wrong. Please try again or contact support.",                        IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 2,  ErrorCode = "DEFAULT",                       Language = "es", UserFriendlyMessage = "Algo salió mal. Intente de nuevo o comuníquese con soporte.",                    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 3,  ErrorCode = "INTERNAL_ERROR",                Language = "en", UserFriendlyMessage = "An unexpected error occurred. Please try again later.",                          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 4,  ErrorCode = "INTERNAL_ERROR",                Language = "es", UserFriendlyMessage = "Ocurrió un error inesperado. Por favor intente más tarde.",                      IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 5,  ErrorCode = "ORDER_NOT_FOUND",               Language = "en", UserFriendlyMessage = "The requested order could not be found.",                                        IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 6,  ErrorCode = "ORDER_NOT_FOUND",               Language = "es", UserFriendlyMessage = "No se encontró la orden solicitada.",                                            IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 7,  ErrorCode = "MEMBER_NOT_FOUND",              Language = "en", UserFriendlyMessage = "The member account could not be found.",                                         IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 8,  ErrorCode = "MEMBER_NOT_FOUND",              Language = "es", UserFriendlyMessage = "No se encontró la cuenta del miembro.",                                          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 9,  ErrorCode = "MEMBER_ALREADY_EXISTS",         Language = "en", UserFriendlyMessage = "An account with this information already exists.",                               IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 10, ErrorCode = "MEMBER_ALREADY_EXISTS",         Language = "es", UserFriendlyMessage = "Ya existe una cuenta con esta información.",                                     IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 11, ErrorCode = "SPONSOR_NOT_FOUND",             Language = "en", UserFriendlyMessage = "The sponsor ID you entered could not be found. Please verify and try again.",   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 12, ErrorCode = "SPONSOR_NOT_FOUND",             Language = "es", UserFriendlyMessage = "El ID de patrocinador ingresado no fue encontrado. Verifíquelo e intente de nuevo.", IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 13, ErrorCode = "DUPLICATE_REPLICATE_SITE",      Language = "en", UserFriendlyMessage = "This website address is already taken. Please choose a different one.",          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 14, ErrorCode = "DUPLICATE_REPLICATE_SITE",      Language = "es", UserFriendlyMessage = "Esta dirección de sitio web ya está en uso. Por favor elija una diferente.",    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 15, ErrorCode = "MEMBERSHIP_LEVEL_NOT_FOUND",    Language = "en", UserFriendlyMessage = "The selected membership plan is not available.",                                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 16, ErrorCode = "MEMBERSHIP_LEVEL_NOT_FOUND",    Language = "es", UserFriendlyMessage = "El plan de membresía seleccionado no está disponible.",                          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 17, ErrorCode = "PRODUCT_NOT_FOUND",             Language = "en", UserFriendlyMessage = "One or more of the selected products are not available.",                        IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 18, ErrorCode = "PRODUCT_NOT_FOUND",             Language = "es", UserFriendlyMessage = "Uno o más de los productos seleccionados no están disponibles.",                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 19, ErrorCode = "MINIMUM_AGE_REQUIRED",          Language = "en", UserFriendlyMessage = "You must be at least 18 years old to register.",                                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 20, ErrorCode = "MINIMUM_AGE_REQUIRED",          Language = "es", UserFriendlyMessage = "Debes tener al menos 18 años para registrarte.",                                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 21, ErrorCode = "PLACEMENT_WINDOW_EXPIRED",      Language = "en", UserFriendlyMessage = "The 30-day placement window has expired for this member.",                       IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 22, ErrorCode = "PLACEMENT_WINDOW_EXPIRED",      Language = "es", UserFriendlyMessage = "El período de 30 días para colocar a este miembro ha expirado.",                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 23, ErrorCode = "UNPLACEMENT_LIMIT_EXCEEDED",    Language = "en", UserFriendlyMessage = "The maximum number of placement changes for this member has been reached.",      IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 24, ErrorCode = "UNPLACEMENT_LIMIT_EXCEEDED",    Language = "es", UserFriendlyMessage = "Se alcanzó el límite máximo de cambios de posición para este miembro.",          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 25, ErrorCode = "UNPLACEMENT_WINDOW_EXPIRED",    Language = "en", UserFriendlyMessage = "The 72-hour unplacement window has expired.",                                    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 26, ErrorCode = "UNPLACEMENT_WINDOW_EXPIRED",    Language = "es", UserFriendlyMessage = "El período de 72 horas para retirar la posición ha expirado.",                   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 27, ErrorCode = "MEMBERSHIP_CHANGE_NOT_ALLOWED", Language = "en", UserFriendlyMessage = "This membership change is not permitted at this time.",                          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 28, ErrorCode = "MEMBERSHIP_CHANGE_NOT_ALLOWED", Language = "es", UserFriendlyMessage = "Este cambio de membresía no está permitido en este momento.",                    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 29, ErrorCode = "MEMBERSHIP_NOT_FOUND",          Language = "en", UserFriendlyMessage = "No active membership was found for this account.",                               IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 30, ErrorCode = "MEMBERSHIP_NOT_FOUND",          Language = "es", UserFriendlyMessage = "No se encontró una membresía activa para esta cuenta.",                          IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 31, ErrorCode = "NO_SPONSOR_BONUS_TYPE",         Language = "en", UserFriendlyMessage = "The system could not process the bonus at this time. Please contact support.",   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 32, ErrorCode = "NO_SPONSOR_BONUS_TYPE",         Language = "es", UserFriendlyMessage = "El sistema no pudo procesar el bono en este momento. Contacte soporte.",         IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 33, ErrorCode = "NO_REVERSE_TYPE",               Language = "en", UserFriendlyMessage = "The reversal could not be processed. Please contact support.",                   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 34, ErrorCode = "NO_REVERSE_TYPE",               Language = "es", UserFriendlyMessage = "No se pudo procesar el reverso. Por favor contacte soporte.",                    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 35, ErrorCode = "REVERSE_TYPE_NOT_FOUND",        Language = "en", UserFriendlyMessage = "The reversal could not be processed. Please contact support.",                   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 36, ErrorCode = "REVERSE_TYPE_NOT_FOUND",        Language = "es", UserFriendlyMessage = "No se pudo procesar el reverso. Por favor contacte soporte.",                    IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 37, ErrorCode = "COMMISSION_PERIOD_NOT_FOUND",   Language = "en", UserFriendlyMessage = "Commission data for the requested period could not be found.",                   IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 38, ErrorCode = "COMMISSION_PERIOD_NOT_FOUND",   Language = "es", UserFriendlyMessage = "No se encontraron datos de comisión para el período solicitado.",                IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 39, ErrorCode = "INSUFFICIENT_TOKEN_BALANCE",    Language = "en", UserFriendlyMessage = "You do not have enough tokens to complete this action.",                         IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 40, ErrorCode = "INSUFFICIENT_TOKEN_BALANCE",    Language = "es", UserFriendlyMessage = "No tienes suficientes tokens para completar esta acción.",                       IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 41, ErrorCode = "WALLET_NOT_FOUND",              Language = "en", UserFriendlyMessage = "No payment method was found for this account.",                                  IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 42, ErrorCode = "WALLET_NOT_FOUND",              Language = "es", UserFriendlyMessage = "No se encontró un método de pago para esta cuenta.",                             IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 43, ErrorCode = "WALLET_PASSWORD_NOT_ENCRYPTED", Language = "en", UserFriendlyMessage = "A security error occurred. Please contact support immediately.",                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 44, ErrorCode = "WALLET_PASSWORD_NOT_ENCRYPTED", Language = "es", UserFriendlyMessage = "Ocurrió un error de seguridad. Contacte soporte inmediatamente.",                IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 45, ErrorCode = "RANK_NOT_FOUND",                Language = "en", UserFriendlyMessage = "The requested rank information could not be found.",                             IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 46, ErrorCode = "RANK_NOT_FOUND",                Language = "es", UserFriendlyMessage = "No se encontró la información del rango solicitado.",                            IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 47, ErrorCode = "PAYMENT_FAILED",                Language = "en", UserFriendlyMessage = "Your payment could not be processed. Please verify your payment details.",      IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 48, ErrorCode = "PAYMENT_FAILED",                Language = "es", UserFriendlyMessage = "No se pudo procesar tu pago. Por favor verifica tus datos de pago.",            IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 49, ErrorCode = "REFUND_FAILED",                 Language = "en", UserFriendlyMessage = "The refund could not be processed at this time. Please contact support.",       IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 50, ErrorCode = "REFUND_FAILED",                 Language = "es", UserFriendlyMessage = "No se pudo procesar el reembolso en este momento. Contacte soporte.",           IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },

            new ErrorMessage { Id = 51, ErrorCode = "UNAUTHORIZED",                  Language = "en", UserFriendlyMessage = "You are not authorized to perform this action.",                                 IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 52, ErrorCode = "UNAUTHORIZED",                  Language = "es", UserFriendlyMessage = "No tienes autorización para realizar esta acción.",                              IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 53, ErrorCode = "VALIDATION_ERROR",              Language = "en", UserFriendlyMessage = "The information you provided is invalid. Please review and try again.",         IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" },
            new ErrorMessage { Id = 54, ErrorCode = "VALIDATION_ERROR",              Language = "es", UserFriendlyMessage = "La información proporcionada no es válida. Por favor revísela e intente de nuevo.", IsActive = true, CreationDate = SeedDate, CreatedBy = "seed" }
        );
    }
}
