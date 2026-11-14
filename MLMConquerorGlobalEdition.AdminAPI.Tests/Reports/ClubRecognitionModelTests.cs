using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.Domain.Entities.Recognition;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Esquema de Club Recognition en el modelo de diseño de SQL Server (sin conectarse): una fila por Ambassador y nivel,
/// fechas sin hora y la semilla de niveles y opciones de la especificación.
/// </summary>
public class ClubRecognitionModelTests
{
    private static readonly IModel Modelo = CrearModelo();

    private static IModel CrearModelo()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=modelo-sin-conexion;Integrated Security=true;TrustServerCertificate=True")
            .Options);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entidad<T>() => Modelo.FindEntityType(typeof(T))!;

    [Fact]
    public void Una_fila_por_Ambassador_y_nivel()
        => Entidad<MemberRecognition>().GetIndexes().Should().ContainSingle(i => i.IsUnique
            && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "MemberId", "RecognitionLevelId" }));

    [Fact]
    public void Reached_y_el_mes_de_la_llamada_son_fechas_sin_hora()
    {
        Entidad<MemberRecognition>().FindProperty(nameof(MemberRecognition.ReachedDate))!.GetColumnType()
            .Should().Be("date");
        Entidad<MemberRecognition>().FindProperty(nameof(MemberRecognition.CeoCallMonth))!.GetColumnType()
            .Should().Be("date");
    }

    [Fact]
    public void Semilla_de_los_seis_niveles()
    {
        var niveles = Entidad<RecognitionLevel>().GetSeedData()
            .Select(d => ((int)d["Id"]!, (string)d["Name"]!, (decimal)d["Threshold"]!,
                          d.TryGetValue("WindowDays", out var ventana) ? (int?)ventana : null,
                          (int)d["SortOrder"]!, (bool)d["IsActive"]!))
            .OrderBy(n => n.Item1)
            .ToList();

        niveles.Should().Equal(
            (1, "100k Earner",                    50_000m,    (int?)180, 1, true),
            (2, "500k Earner",                    250_000m,   (int?)180, 2, true),
            (3, "Million Dollar Earner",          500_000m,   (int?)180, 3, true),
            (4, "100k Lifetime Earner",           100_000m,   (int?)null, 4, true),
            (5, "500k Lifetime Earner",           500_000m,   (int?)null, 5, true),
            (6, "Lifetime Million Dollar Earner", 1_000_000m, (int?)null, 6, true));
    }

    [Fact]
    public void Semilla_de_las_opciones()
    {
        var opciones = Entidad<RecognitionOption>().GetSeedData()
            .Select(d => ((int)d["Id"]!, (string)d["Name"]!,
                          d.TryGetValue("RequiresReason", out var motivo) && (bool)motivo!))
            .OrderBy(o => o.Item1)
            .ToList();

        opciones.Should().Equal(
            (1, "Recognized Higher Level", false),
            (2, "Recognized Long Time Ago", false),
            (3, "Do not recognize", true));
    }
}
