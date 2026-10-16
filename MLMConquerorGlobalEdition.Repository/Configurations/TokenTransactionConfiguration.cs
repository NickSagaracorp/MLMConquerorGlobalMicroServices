using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Tokens;
using MLMConquerorGlobalEdition.Domain.Enums;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class TokenTransactionConfiguration : IEntityTypeConfiguration<TokenTransaction>
{
    public void Configure(EntityTypeBuilder<TokenTransaction> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MemberId, x.CreationDate });

        // Dueño y estado son tokens de concurrencia: un UPDATE de un código lleva en el WHERE el dueño
        // y el estado que se leyeron. Si otro proceso lo canjeó o lo movió entre la lectura y el
        // guardado, el UPDATE no toca ninguna fila y EF lanza DbUpdateConcurrencyException en vez de
        // pisarlo (canje en el alta frente a distribuir: el código no se gasta dos veces ni resucita).
        // No cambia el esquema: solo el SQL que genera EF.
        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(36).IsConcurrencyToken();
        builder.Property(x => x.DistributedToMemberId).HasMaxLength(36);
        builder.Property(x => x.UsedByMemberId).HasMaxLength(36);
        builder.Property(x => x.OriginalOwnerMemberId).HasMaxLength(36);
        builder.Property(x => x.PreviousOwnerMemberId).HasMaxLength(36);
        builder.Property(x => x.UsedOnOrderId).HasMaxLength(36);
        builder.Property(x => x.ReferenceId).HasMaxLength(20);

        builder.Property(x => x.Status)
               .HasConversion<int>()
               .IsConcurrencyToken()
               .HasDefaultValue(TokenInstanceStatus.Issued);

        // Each redeemable instance has a unique ReferenceId (TokenCode).
        // Filtered unique index — only enforces uniqueness on rows that actually carry a code.
        builder.HasIndex(x => x.ReferenceId)
               .IsUnique()
               .HasFilter("[ReferenceId] IS NOT NULL");

        // Fast lookups for "what tokens does this member currently hold (redeemable)?"
        builder.HasIndex(x => new { x.MemberId, x.Status, x.ReferenceId });

        builder.Property(x => x.TokenPurchaseId).HasMaxLength(36);
        builder.HasIndex(x => x.TokenPurchaseId).HasFilter("[TokenPurchaseId] IS NOT NULL");

        // El tope de 50 al mes cuenta por dueño original, tipo de movimiento y fecha.
        builder.HasIndex(x => new { x.OriginalOwnerMemberId, x.TransactionType, x.CreationDate });
    }
}
