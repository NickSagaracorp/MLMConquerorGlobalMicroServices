namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Las clases que llaman o dependen de EarningsRankingService.InvalidateAll / RankReportService.InvalidateAll (estaticos,
/// globales al proceso) corren una a una: si no, una invalida la cache de otra en pleno calculo y las pruebas fallan
/// al azar.
/// </summary>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public sealed class CacheDeReportesCollection
{
    public const string Nombre = "CacheDeReportes";
}
