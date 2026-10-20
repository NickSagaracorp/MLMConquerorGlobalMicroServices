using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class BusquedaYNivelDeAlta : Migration
    {
        /// <summary>
        /// Plan Q, revision del bloque 5 (I5). Las columnas de busqueda son calculadas NO persistidas: anadirlas es solo
        /// metadatos (una persistida reescribia MemberProfiles entera con un candado Sch-M en una sola transaccion). Sus
        /// indices guardan el valor (la expresion es deterministica y precisa) y filtran IsDeleted = 0 (I3). Los indices se
        /// crean fuera de la transaccion de la migracion, cada uno en la suya, y ONLINE donde la edicion lo admite
        /// (Enterprise o Developer, Azure SQL, Managed Instance; en las demas, sin ONLINE). IX_MemberProfiles_EnrollDate se
        /// rehace con DROP_EXISTING: no queda un momento sin indice. Cada paso comprueba si ya esta hecho, asi que una
        /// migracion interrumpida a medias se puede volver a ejecutar.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'dbo.MemberProfiles', N'SignupMembershipLevelId') IS NULL
    ALTER TABLE [MemberProfiles] ADD [SignupMembershipLevelId] int NULL;
IF COL_LENGTH(N'dbo.MemberProfiles', N'SearchName') IS NULL
    ALTER TABLE [MemberProfiles] ADD [SearchName] AS CAST(CONCAT([FirstName], N' ', [LastName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI;
IF COL_LENGTH(N'dbo.MemberProfiles', N'SearchNameReversed') IS NULL
    ALTER TABLE [MemberProfiles] ADD [SearchNameReversed] AS CAST(CONCAT([LastName], N' ', [FirstName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI;");

            Indice(migrationBuilder, "IX_MemberProfiles_Clientes",
                "[MemberId] ASC) INCLUDE ([EnrollDate], [SponsorMemberId]) WHERE [MemberType] = 1 AND [IsDeleted] = 0");
            Indice(migrationBuilder, "IX_MemberProfiles_SearchName",
                "[SearchName] ASC) INCLUDE ([MemberId]) WHERE [IsDeleted] = 0");
            Indice(migrationBuilder, "IX_MemberProfiles_SearchNameReversed",
                "[SearchNameReversed] ASC) INCLUDE ([MemberId]) WHERE [IsDeleted] = 0");
            // El de la fecha de alta ya existe (sin columnas incluidas): se rehace en su sitio, con DROP_EXISTING. Hecho si ya
            // incluye SignupMembershipLevelId.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic
               JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
               WHERE i.object_id = OBJECT_ID(N'dbo.MemberProfiles') AND i.name = N'IX_MemberProfiles_EnrollDate'
                 AND COL_NAME(ic.object_id, ic.column_id) = N'SignupMembershipLevelId')
BEGIN
    " + Online + @"
    DECLARE @sql nvarchar(max) = N'CREATE INDEX [IX_MemberProfiles_EnrollDate] ON [MemberProfiles] ([EnrollDate] ASC) '
        + N'INCLUDE ([SponsorMemberId], [MemberType], [SignupMembershipLevelId]) '
        + CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MemberProfiles') AND name = N'IX_MemberProfiles_EnrollDate')
               THEN N'WITH (DROP_EXISTING = ON, ONLINE = ' + @online + N')'
               ELSE N'WITH (ONLINE = ' + @online + N')' END;
    EXEC (@sql);
END", suppressTransaction: true);
        }

        /// <summary>ON en las ediciones que admiten indices en linea (EngineEdition 3: Enterprise, Developer, Evaluation; 5: Azure SQL; 8: Managed Instance).</summary>
        private const string Online =
            "DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN N'ON' ELSE N'OFF' END;";

        /// <summary>Crea el indice de MemberProfiles si no existe, fuera de la transaccion de la migracion y ONLINE si se puede.</summary>
        private static void Indice(MigrationBuilder migrationBuilder, string nombre, string definicion) =>
            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MemberProfiles') AND name = N'{nombre}')
BEGIN
    {Online}
    DECLARE @sql nvarchar(max) = N'CREATE INDEX [{nombre}] ON [MemberProfiles] ({definicion.Replace("'", "''")} WITH (ONLINE = ' + @online + N')';
    EXEC (@sql);
END", suppressTransaction: true);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_Clientes",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_SearchName",
                table: "MemberProfiles");

            migrationBuilder.DropIndex(
                name: "IX_MemberProfiles_SearchNameReversed",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SearchName",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SearchNameReversed",
                table: "MemberProfiles");

            migrationBuilder.DropColumn(
                name: "SignupMembershipLevelId",
                table: "MemberProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_MemberProfiles_EnrollDate",
                table: "MemberProfiles",
                column: "EnrollDate");
        }
    }
}
