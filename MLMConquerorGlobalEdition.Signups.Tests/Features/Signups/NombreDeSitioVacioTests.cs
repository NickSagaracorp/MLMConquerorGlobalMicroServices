using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Identity;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.DTOs;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupAmbassador;
using MLMConquerorGlobalEdition.SignupAPI.Features.Signups.Commands.SignupMember;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Signups;

/// <summary>
/// El fallo que dejaba fuera a todo un evento.
///
/// MemberProfiles.ReplicateSiteSlug tiene un índice único FILTRADO por IS NOT NULL. La cadena
/// vacía no es NULL, así que entra en la unicidad como cualquier otro valor: la primera alta sin
/// nombre de sitio pasaba y todas las siguientes reventaban con clave duplicada, que al usuario
/// le salía como "An unexpected error occurred". En un evento, donde casi nadie elige nombre de
/// sitio, se daba de alta el primero y fallaban los demás.
///
/// POR QUÉ ESTAS PRUEBAS MIRAN EL VALOR Y NO LA EXCEPCIÓN: el proveedor InMemory de EF no aplica
/// índices únicos, así que una prueba que esperara la excepción de clave duplicada pasaría en
/// verde incluso con el fallo dentro. Lo que sí distingue el código roto del arreglado es el
/// valor que llega a la columna: antes "" y ahora null. Esa es la invariante de la que depende
/// el filtro del índice, y es la que se comprueba aquí.
/// </summary>
public class NombreDeSitioVacioTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Mock<IDateTimeProvider> BuildClock()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(FixedNow);
        return m;
    }

    private static Mock<IPushNotificationService> BuildPush()
    {
        var m = new Mock<IPushNotificationService>();
        m.Setup(p => p.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return m;
    }

    private static Mock<IEncryptionService> BuildEncryption()
    {
        var m = new Mock<IEncryptionService>();
        m.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => "ENC:" + s);
        m.Setup(e => e.Decrypt(It.IsAny<string>()))
         .Returns<string>(c => c.StartsWith("ENC:") ? c[4..] : c);
        return m;
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManager()
    {
        var mgr = UserManagerHelper.Create();
        mgr.Setup(u => u.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        mgr.Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mgr.Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        return mgr;
    }

    private static SignupAmbassadorHandler BuildAmbassadorHandler(
        MLMConquerorGlobalEdition.Repository.Context.AppDbContext db) =>
        new(db, BuildClock().Object, BuildUserManager().Object,
            BuildPush().Object, BuildEncryption().Object);

    private static MembershipLevel BuildLevel(int id = 1) => new()
    {
        Id           = id,
        Name         = "Ambassador Basic",
        Description  = "Entry level ambassador tier",
        Price        = 80,
        RenewalPrice = 80,
        IsActive     = true,
        IsFree       = false,
        IsAutoRenew  = true,
        SortOrder    = 1,
        CreatedBy    = "seed",
        CreationDate = FixedNow
    };

    private static AmbassadorSignupRequest BuildRequest(string email, string? slug) => new()
    {
        FirstName         = "Carlos",
        LastName          = "Rivera",
        DateOfBirth       = new DateTime(1990, 6, 15),
        Email             = email,
        Password          = "SecurePass!1",
        ConfirmPassword   = "SecurePass!1",
        Phone             = "+1-555-0100",
        Country           = "US",
        State             = "FL",
        City              = "Miami",
        Address           = "123 Ocean Drive",
        ZipCode           = "33139",
        ReplicateSiteSlug = slug,
        MembershipLevelId = 1
    };

    // ── La normalización, valor a valor ─────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Alta_SinNombreDeSitio_PersisteNullYNoCadenaVacia(string? slugQueMandaElCliente)
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildAmbassadorHandler(db);

        var result = await handler.Handle(
            new SignupAmbassadorCommand(BuildRequest("uno@example.com", slugQueMandaElCliente)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.MemberProfiles.SingleAsync(m => m.Email == "uno@example.com");
        persisted.ReplicateSiteSlug.Should().BeNull(
            "el índice único está filtrado por IS NOT NULL: la cadena vacía entra en la unicidad y NULL no.");
    }

    [Fact]
    public async Task Alta_ConNombreDeSitio_LoConservaYLeQuitaLosEspacios()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildAmbassadorHandler(db);

        var result = await handler.Handle(
            new SignupAmbassadorCommand(BuildRequest("dos@example.com", "  mi-sitio  ")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.MemberProfiles.SingleAsync(m => m.Email == "dos@example.com");
        persisted.ReplicateSiteSlug.Should().Be("mi-sitio");
    }

    // ── La regresión que pide el encargo: DOS ALTAS SEGUIDAS sin nombre de sitio ──

    [Fact]
    public async Task DosAltasSeguidasSinNombreDeSitio_LasDosPasan()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildAmbassadorHandler(db);

        var primera = await handler.Handle(
            new SignupAmbassadorCommand(BuildRequest("primera@example.com", string.Empty)),
            CancellationToken.None);

        var segunda = await handler.Handle(
            new SignupAmbassadorCommand(BuildRequest("segunda@example.com", string.Empty)),
            CancellationToken.None);

        primera.IsSuccess.Should().BeTrue();
        segunda.IsSuccess.Should().BeTrue(
            "con el fallo dentro, la segunda alta sin nombre de sitio moría con clave duplicada.");

        var perfiles = await db.MemberProfiles
            .Where(m => m.Email == "primera@example.com" || m.Email == "segunda@example.com")
            .ToListAsync();

        perfiles.Should().HaveCount(2);
        perfiles.Should().OnlyContain(m => m.ReplicateSiteSlug == null);

        // Lo que de verdad reventaba en SQL Server: dos filas con el MISMO valor no nulo.
        (await db.MemberProfiles.CountAsync(m => m.ReplicateSiteSlug == string.Empty))
            .Should().Be(0, "ninguna fila puede llevar cadena vacía en la columna que indexa la unicidad.");
    }

    [Fact]
    public async Task TresAltasSeguidasSinNombreDeSitio_TodasPasan()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.SaveChangesAsync();

        var handler = BuildAmbassadorHandler(db);

        foreach (var email in new[] { "a@example.com", "b@example.com", "c@example.com" })
        {
            var r = await handler.Handle(
                new SignupAmbassadorCommand(BuildRequest(email, "   ")), CancellationToken.None);
            r.IsSuccess.Should().BeTrue($"el alta de {email} no puede depender de cuántas la precedieron.");
        }

        (await db.MemberProfiles.CountAsync(m => m.ReplicateSiteSlug == null)).Should().Be(3);
    }

    // ── Y que el arreglo no se lleve por delante la detección de duplicados reales ──

    [Fact]
    public async Task DosAltasConElMismoNombreDeSitio_LaSegundaSigueFallando()
    {
        await using var db = InMemoryDbHelper.Create();
        await db.MembershipLevels.AddAsync(BuildLevel());
        await db.MemberProfiles.AddAsync(new MemberProfile
        {
            MemberId          = "AMB-000001",
            FirstName         = "Maria",
            LastName          = "Gomez",
            Email             = "maria@example.com",
            MemberType        = MemberType.Ambassador,
            Status            = MemberAccountStatus.Active,
            ReplicateSiteSlug = "mi-sitio",
            EnrollDate        = FixedNow.AddMonths(-6),
            Country           = "US",
            CreatedBy         = "seed",
            LastUpdateDate    = FixedNow
        });
        await db.SaveChangesAsync();

        var handler = BuildAmbassadorHandler(db);

        var act = () => handler.Handle(
            new SignupAmbassadorCommand(BuildRequest("otro@example.com", " mi-sitio ")),
            CancellationToken.None);

        await act.Should().ThrowAsync<MLMConquerorGlobalEdition.Domain.Exceptions.DuplicateReplicateSiteException>(
            "recortar los espacios no puede abrir una puerta para colar un duplicado con adornos.");
    }

    // ── Las otras dos vías que el encargo pedía revisar ──────────────────────────

    [Fact]
    public void SignupMemberHandler_NoEscribeNombreDeSitio_AsiQueNoTieneElFallo()
    {
        // MemberSignupRequest ni siquiera tiene la propiedad: el alta de miembro externo no pide
        // nombre de sitio, y SignupMemberHandler deja MemberProfile.ReplicateSiteSlug en null.
        // Se comprueba sobre el contrato para que la prueba se ponga roja si alguien añade el
        // campo sin normalizarlo.
        typeof(MemberSignupRequest).GetProperty("ReplicateSiteSlug").Should().BeNull(
            "si aparece esta propiedad hay que normalizar la cadena vacía en SignupMemberHandler igual que en el de embajador.");

        typeof(SignupMemberHandler).Should().NotBeNull();
    }
}
