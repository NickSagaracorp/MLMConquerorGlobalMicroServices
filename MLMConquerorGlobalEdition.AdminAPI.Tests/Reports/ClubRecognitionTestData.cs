using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Events;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>Base InMemory y siembra de las pruebas de Club Recognition. Los métodos añaden al contexto, no guardan.</summary>
internal static class ClubRecognitionTestData
{
    public static readonly DateTime Semilla = new(2026, 11, 14);

    public static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Los seis niveles de la semilla (mismos ids, montos y ventanas). <paramref name="conTalla"/>: ids con talla.</summary>
    public static void Niveles(AppDbContext db, params int[] conTalla) => db.RecognitionLevels.AddRange(
        Nivel(1, "100k Earner",                    50_000m,    180,  1, conTalla),
        Nivel(2, "500k Earner",                    250_000m,   180,  2, conTalla),
        Nivel(3, "Million Dollar Earner",          500_000m,   180,  3, conTalla),
        Nivel(4, "100k Lifetime Earner",           100_000m,   null, 4, conTalla),
        Nivel(5, "500k Lifetime Earner",           500_000m,   null, 5, conTalla),
        Nivel(6, "Lifetime Million Dollar Earner", 1_000_000m, null, 6, conTalla));

    private static RecognitionLevel Nivel(int id, string nombre, decimal umbral, int? ventana, int orden, int[] conTalla) => new()
    {
        Id = id, Name = nombre, Threshold = umbral, WindowDays = ventana, SortOrder = orden, IsActive = true,
        RewardNeedsSize = conTalla.Contains(id), CreationDate = Semilla, CreatedBy = "seed"
    };

    /// <summary>Las tres opciones de la semilla (ids 1 a 3; la 3 pide motivo).</summary>
    public static void Opciones(AppDbContext db) => db.RecognitionOptions.AddRange(
        new RecognitionOption { Id = 1, Name = "Recognized Higher Level",  SortOrder = 1, CreationDate = Semilla, CreatedBy = "seed" },
        new RecognitionOption { Id = 2, Name = "Recognized Long Time Ago", SortOrder = 2, CreationDate = Semilla, CreatedBy = "seed" },
        new RecognitionOption { Id = 3, Name = "Do not recognize", RequiresReason = true, SortOrder = 3, CreationDate = Semilla, CreatedBy = "seed" });

    public static MemberProfile Miembro(AppDbContext db, string memberId, string nombre = "Nombre", string? apellido = null,
        string? email = null, string pais = "US", string? slug = null, MemberType tipo = MemberType.Ambassador,
        MemberAccountStatus estado = MemberAccountStatus.Active, bool corporativa = false)
    {
        var perfil = new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = memberId, FirstName = nombre, LastName = apellido ?? memberId,
            Email = email ?? $"{memberId.ToLowerInvariant()}@example.com", Country = pais, ReplicateSiteSlug = slug,
            MemberType = tipo, Status = estado, IsCorporateAccount = corporativa,
            CreatedBy = "seed", CreationDate = Semilla, LastUpdateDate = Semilla
        };
        db.MemberProfiles.Add(perfil);
        return perfil;
    }

    public static void Comision(AppDbContext db, string beneficiario, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = beneficiario, Amount = monto, EarnedDate = fecha,
            PaymentDate = fecha, Status = estado, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = fecha, LastUpdateDate = fecha
        });

    public static MemberRecognition Registro(AppDbContext db, long id, string memberId, int nivel, DateTime alcanzado,
        decimal total, RecognitionSource origen = RecognitionSource.Historical)
    {
        var registro = new MemberRecognition
        {
            Id = id, MemberId = memberId, RecognitionLevelId = nivel, ReachedDate = alcanzado, QualifyingTotal = total,
            Source = origen, CreationDate = Semilla, CreatedBy = "seed"
        };
        db.MemberRecognitions.Add(registro);
        return registro;
    }

    public static void Evento(AppDbContext db, string id, string titulo, DateTime fecha, bool borrado = false) =>
        db.CorporateEvents.Add(new CorporateEvent
        {
            Id = id, Title = titulo, EventDate = fecha, IsDeleted = borrado,
            CreatedBy = "seed", CreationDate = Semilla, LastUpdateDate = Semilla
        });

    public static IDateTimeProvider Reloj(DateTime ahora)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(ahora);
        return reloj.Object;
    }

    public static ICurrentUserService Usuario(string id = "admin-1")
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.SetupGet(u => u.UserId).Returns(id);
        return usuario.Object;
    }
}
