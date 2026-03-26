namespace MLMConquerorGlobalEdition.AdminAPI.DTOs.TokenAdmin;

public record TokenCodeDto(
    long Id,
    string TokenCode,
    string TokenTypeName,
    bool IsGuestPass,
    string TransactionType,
    bool IsUsed,
    string? UsedByMemberId,
    DateTime? UsedAt,
    DateTime CreatedDate
);
