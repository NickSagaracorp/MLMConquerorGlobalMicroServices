using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.General;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class SignupRiskFingerprintConfiguration : IEntityTypeConfiguration<SignupRiskFingerprint>
{
    public void Configure(EntityTypeBuilder<SignupRiskFingerprint> builder)
    {
        builder.ToTable("SignupRiskFingerprints");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VisitorId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.RequestId).HasMaxLength(100);
        builder.Property(x => x.OrderId).HasMaxLength(36);
        builder.Property(x => x.MemberId).HasMaxLength(36);
        builder.Property(x => x.SponsorReplicateSite).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(45);     // IPv6 max
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CountryIso2).HasMaxLength(2);
        builder.Property(x => x.FlagReason).HasMaxLength(200);
        builder.Property(x => x.ClearedBy).HasMaxLength(36);
        builder.Property(x => x.ClearReason).HasMaxLength(500);
        builder.Property(x => x.Flow).HasConversion<int>();

        // Most-common queries: "events for this visitor in the last X hours" and
        // "events from this IP in the last X hours". Compound indexes win there.
        // Cleared rows must be excluded from threshold counts — the index includes it
        // so the predicate is a covered seek instead of a scan.
        builder.HasIndex(x => new { x.VisitorId, x.CreationDate, x.Cleared });
        builder.HasIndex(x => new { x.IpAddress, x.CreationDate });
        builder.HasIndex(x => x.OrderId);
    }
}
