using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Member;

namespace MLMConquerorGlobalEdition.Repository.Configurations;

public class MemberProfileConfiguration : IEntityTypeConfiguration<MemberProfile>
{
    public void Configure(EntityTypeBuilder<MemberProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.MemberId).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.MemberId).IsUnique();

        // Plan J: el patrocinador mide como GenealogyTree.MemberId y DualTeamTree.MemberId (450), con
        // los que se une; en la solución del cliente hay ids de 50 caracteres. 450 es lo máximo que
        // admite índice: se indexa con la fecha de alta para los clientes personales y la actividad.
        builder.Property(x => x.SponsorMemberId).HasMaxLength(450);
        builder.HasIndex(x => new { x.SponsorMemberId, x.EnrollDate });
        // Plan J: la actividad por fechas reduce primero por fecha de alta, sin patrocinador concreto. Plan Q (DQ10): con
        // el patrocinador, el tipo y el nivel de alta incluidos, Actividad agrupa sin ir a la tabla.
        builder.HasIndex(m => m.EnrollDate).HasDatabaseName("IX_MemberProfiles_EnrollDate")
            .IncludeProperties(m => new { m.SponsorMemberId, m.MemberType, m.SignupMembershipLevelId });

        // Plan Q: los clientes (MemberType = ExternalMember = 1) son pocos; el reporte de equipo parte de ellos. El filtro
        // global de MemberProfile anade IsDeleted = 0 a todas sus consultas, asi que el indice filtrado sirve.
        builder.HasIndex(new[] { nameof(MemberProfile.MemberId) }, "IX_MemberProfiles_Clientes")
            .HasFilter("[MemberType] = 1 AND [IsDeleted] = 0")
            .IncludeProperties(m => new { m.EnrollDate, m.SponsorMemberId });

        // Plan J (N3): las filas que ya existen quedan sin la marca; nadie pasa a calificado solo.
        builder.Property(x => x.KeepQualified).IsRequired().HasDefaultValue(false);

        // Plan K (K7): encendida por defecto, también para las filas que ya existen. HasSentinel(true):
        // EF solo omite la columna al insertar cuando vale true (y entonces la base pone su 1); un false
        // se manda siempre. Con el centinela por defecto (false) nunca se podría insertar un false.
        builder.Property(x => x.FeesFromBalance).IsRequired().HasDefaultValue(true).HasSentinel(true);

        builder.Property(x => x.UserId).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();

        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        // Plan Q (DQ8): nombre completo y nombre invertido, calculados y persistidos con intercalacion sin tildes ni
        // mayusculas, para buscar por el comienzo con un Index Seek (Full-Text no esta instalado). MemberNameSearch los usa.
        builder.Property(m => m.SearchName).HasMaxLength(201)
            .HasComputedColumnSql("CAST(CONCAT([FirstName], N' ', [LastName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI", stored: true);
        builder.Property(m => m.SearchNameReversed).HasMaxLength(201)
            .HasComputedColumnSql("CAST(CONCAT([LastName], N' ', [FirstName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI", stored: true);
        builder.HasIndex(m => m.SearchName).HasDatabaseName("IX_MemberProfiles_SearchName").IncludeProperties(m => m.MemberId);
        builder.HasIndex(m => m.SearchNameReversed).HasDatabaseName("IX_MemberProfiles_SearchNameReversed").IncludeProperties(m => m.MemberId);
        builder.Property(x => x.Phone).HasMaxLength(30);
        builder.Property(x => x.Country).IsRequired().HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.Address).HasMaxLength(500);

        builder.Property(x => x.SsnEncrypted).HasMaxLength(512);

        builder.Property(x => x.ReplicateSiteSlug).HasMaxLength(100);
        builder.HasIndex(x => x.ReplicateSiteSlug).IsUnique().HasFilter("[ReplicateSiteSlug] IS NOT NULL");

        // Active membership — one-to-one (nullable)
        builder.HasOne(x => x.ActiveMembership)
            .WithOne()
            .HasForeignKey<MemberProfile>("ActiveMembershipId")
            .IsRequired(false);

        // Membership history — one-to-many. Plan Q (revision del bloque 2, M-4): sin cascada. Borrar un miembro borraba
        // sus suscripciones en SQL, por debajo de EF y del interceptor de calificados y puntos (DQ13, DQ16). Nada borra
        // miembros hoy (las bajas son logicas); quien lo haga borra antes sus suscripciones por EF.
        builder.HasMany(x => x.MembershipHistory)
            .WithOne()
            .HasForeignKey(s => s.MemberId)
            .HasPrincipalKey(m => m.MemberId)
            .OnDelete(DeleteBehavior.NoAction);

        // Tree nodes
        builder.HasOne(x => x.EnrollmentNode)
            .WithOne()
            .HasForeignKey<MemberProfile>("EnrollmentNodeId")
            .IsRequired(false);

        builder.HasOne(x => x.BinaryNode)
            .WithOne()
            .HasForeignKey<MemberProfile>("BinaryNodeId")
            .IsRequired(false);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
