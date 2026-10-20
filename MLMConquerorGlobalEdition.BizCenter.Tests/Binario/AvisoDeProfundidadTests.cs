using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Riesgo P-R1 del Plan P: por encima de DualBinaryPath.WarnDepth (12.000) niveles queda poco margen hasta el limite de
/// la clave (13.567). Crear o mover un nodo mas alla deja un aviso en el log.
/// </summary>
public class AvisoDeProfundidadTests
{
    private sealed class Captura : ILoggerProvider, ILogger
    {
        public readonly List<(LogLevel Nivel, string Texto)> Mensajes = new();
        public ILogger CreateLogger(string categoryName) => this;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Mensajes) Mensajes.Add((logLevel, formatter(state, exception)));
        }
        public void Dispose() { }
    }

    private static (AppDbContext Db, Captura Log) Contexto()
    {
        var log = new Captura();
        var fabrica = LoggerFactory.Create(b => b.AddProvider(log));
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .UseLoggerFactory(fabrica)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        return (db, log);
    }

    private static DualTeamEntity NodoA(int profundidad, string id)
    {
        var k = DualBinaryPath.Root(1);
        for (var i = 0; i < profundidad; i++) k = DualBinaryPath.Child(k, TreeSide.Left);
        return new DualTeamEntity { MemberId = id, BinaryPath = k, Depth = profundidad, CreatedBy = "prueba" };
    }

    [Fact]
    public async Task Un_nodo_por_encima_de_12000_niveles_deja_un_aviso()
    {
        var (db, log) = Contexto();
        await using var _ = db;

        await DualTeamNodes.ChildAsync(db, NodoA(DualBinaryPath.WarnDepth, "HONDO"), "NUEVO", TreeSide.Left, DateTime.Now, "prueba");

        log.Mensajes.Should().Contain(m => m.Nivel == LogLevel.Warning && m.Texto.Contains("NUEVO") && m.Texto.Contains("12001"));
    }

    [Fact]
    public async Task Un_nodo_por_debajo_no_avisa()
    {
        var (db, log) = Contexto();
        await using var _ = db;

        await DualTeamNodes.ChildAsync(db, NodoA(100, "POCO"), "NUEVO", TreeSide.Left, DateTime.Now, "prueba");

        log.Mensajes.Should().NotContain(m => m.Nivel == LogLevel.Warning && m.Texto.Contains("NUEVO"));
    }
}
