using Microsoft.EntityFrameworkCore.Migrations;

namespace MLMConquerorGlobalEdition.Repository.Migrations.Infraestructura;

/// <summary>
/// Plan Q (DQ11, Bloque 6): InitialCreate crea TicketCategories con 7 columnas y HelpdeskModule la vuelve a crear con 13.
/// Como la tabla ya existia, SoloSiLaTablaEsNueva salta su CREATE TABLE, sus indices y sus claves ajenas, y la primera
/// migracion que inserta categorias (SeedItInfrastructureKbArticle) falla con 207. En las bases reales la completo
/// fix_helpdesk_tables.sql (seccion 7) con ALTER TABLE: aqui se hace lo mismo, con los mismos tipos, solo si la tabla era
/// previa a HelpdeskModule y solo lo que falte. Cada sentencia va en EXEC para que el lote compile sin las columnas.
/// </summary>
public static class CompletarCategoriasDeTickets
{
    public static void Aplicar(MigrationBuilder mb) => mb.Sql($@"
IF EXISTS (SELECT 1 FROM dbo.[{SoloSiLaTablaEsNueva.TablaDePrevias}] WHERE Nombre = N'TicketCategories')
BEGIN
    IF COL_LENGTH(N'dbo.TicketCategories', N'Description') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [Description] nvarchar(500) NULL;');
    IF COL_LENGTH(N'dbo.TicketCategories', N'DefaultPriority') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [DefaultPriority] nvarchar(20) NOT NULL DEFAULT ''Normal'';');
    IF COL_LENGTH(N'dbo.TicketCategories', N'DefaultSlaPolicyId') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [DefaultSlaPolicyId] nvarchar(36) NULL;');
    IF COL_LENGTH(N'dbo.TicketCategories', N'DefaultTeamId') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [DefaultTeamId] int NULL;');
    IF COL_LENGTH(N'dbo.TicketCategories', N'ParentCategoryId') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [ParentCategoryId] int NULL;');
    IF COL_LENGTH(N'dbo.TicketCategories', N'SortOrder') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD [SortOrder] int NOT NULL DEFAULT 0;');

    IF OBJECT_ID(N'dbo.FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId', N'F') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId]
              FOREIGN KEY ([DefaultSlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL;');
    IF OBJECT_ID(N'dbo.FK_TicketCategories_SupportTeams_DefaultTeamId', N'F') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SupportTeams_DefaultTeamId]
              FOREIGN KEY ([DefaultTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL;');
    IF OBJECT_ID(N'dbo.FK_TicketCategories_TicketCategories_ParentCategoryId', N'F') IS NULL
        EXEC(N'ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_TicketCategories_ParentCategoryId]
              FOREIGN KEY ([ParentCategoryId]) REFERENCES [TicketCategories] ([Id]);');

    IF INDEXPROPERTY(OBJECT_ID(N'dbo.TicketCategories'), N'IX_TicketCategories_DefaultSlaPolicyId', 'IndexID') IS NULL
        EXEC(N'CREATE INDEX [IX_TicketCategories_DefaultSlaPolicyId] ON [TicketCategories] ([DefaultSlaPolicyId]);');
    IF INDEXPROPERTY(OBJECT_ID(N'dbo.TicketCategories'), N'IX_TicketCategories_DefaultTeamId', 'IndexID') IS NULL
        EXEC(N'CREATE INDEX [IX_TicketCategories_DefaultTeamId] ON [TicketCategories] ([DefaultTeamId]);');
    IF INDEXPROPERTY(OBJECT_ID(N'dbo.TicketCategories'), N'IX_TicketCategories_ParentCategoryId', 'IndexID') IS NULL
        EXEC(N'CREATE INDEX [IX_TicketCategories_ParentCategoryId] ON [TicketCategories] ([ParentCategoryId]);');
END;");
}
