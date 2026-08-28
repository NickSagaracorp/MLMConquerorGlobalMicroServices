using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Security;
using MLMConquerorGlobalEdition.Repository.Identity;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Security;

public class AuthSecurityEventConfiguration : IEntityTypeConfiguration<AuthSecurityEvent>
{
    public void Configure(EntityTypeBuilder<AuthSecurityEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(100);

        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.UserEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.OperationKey).HasMaxLength(64);
        builder.Property(x => x.FailureReason).HasMaxLength(128);
        builder.Property(x => x.IpAddress).HasMaxLength(45);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.RequestPath).HasMaxLength(256);
        builder.Property(x => x.ChallengeJti).HasMaxLength(64);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.CreationDate });
        builder.HasIndex(x => new { x.OperationKey, x.CreationDate });
        builder.HasIndex(x => new { x.EventType, x.CreationDate });
    }
}
