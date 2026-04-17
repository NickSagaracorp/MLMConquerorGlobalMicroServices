using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MLMConquerorGlobalEdition.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RepairTicketsTableSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The HelpdeskModule migration tried to CREATE TABLE Tickets but InitialCreate
            // already created it with only 18 columns. This migration adds the missing 23
            // columns (plus FKs and indexes) so the Tickets table matches the full entity schema.

            // ── 1. Add missing NOT NULL columns (with defaults for existing rows) ────────────
            migrationBuilder.Sql(@"
                ALTER TABLE Tickets
                ADD
                    TicketNumber             nvarchar(20)    NOT NULL CONSTRAINT DF_Tickets_TicketNumber             DEFAULT (''),
                    Channel                  int             NOT NULL CONSTRAINT DF_Tickets_Channel                  DEFAULT (0),
                    EscalationLevel          int             NOT NULL CONSTRAINT DF_Tickets_EscalationLevel          DEFAULT (0),
                    IsSlaFirstResponseBreached bit           NOT NULL CONSTRAINT DF_Tickets_IsSlaFRBreached           DEFAULT (0),
                    IsSlaResolutionBreached  bit             NOT NULL CONSTRAINT DF_Tickets_IsSlaResBreached          DEFAULT (0),
                    IsSlaPaused              bit             NOT NULL CONSTRAINT DF_Tickets_IsSlaPaused               DEFAULT (0),
                    SlaPausedMinutes         float           NOT NULL CONSTRAINT DF_Tickets_SlaPausedMinutes          DEFAULT (0),
                    FollowUpSent             bit             NOT NULL CONSTRAINT DF_Tickets_FollowUpSent              DEFAULT (0);
            ");

            // ── 2. Add missing NULLABLE columns ──────────────────────────────────────────────
            migrationBuilder.Sql(@"
                ALTER TABLE Tickets
                ADD
                    AssignedTeamId          int              NULL,
                    Subcategory             nvarchar(100)    NULL,
                    Language                nvarchar(10)     NULL,
                    CustomerTier            nvarchar(50)     NULL,
                    SlaPolicyId             nvarchar(36)     NULL,
                    SlaFirstResponseDue     datetime2        NULL,
                    SlaFirstResponseAt      datetime2        NULL,
                    SlaResolutionDue        datetime2        NULL,
                    SlaPausedAt             datetime2        NULL,
                    ResolutionSummary       nvarchar(2000)   NULL,
                    ResolvedByAgentId       nvarchar(450)    NULL,
                    CsatScore               int              NULL,
                    CsatComment             nvarchar(1000)   NULL,
                    CsatSubmittedAt         datetime2        NULL,
                    ClosedAt                datetime2        NULL;
            ");

            // ── 3. Add FK: Tickets → SlaPolicies ─────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Tickets_SlaPolicies_SlaPolicyId')
                BEGIN
                    ALTER TABLE Tickets
                    ADD CONSTRAINT FK_Tickets_SlaPolicies_SlaPolicyId
                        FOREIGN KEY (SlaPolicyId) REFERENCES SlaPolicies(Id)
                        ON DELETE SET NULL;
                END
            ");

            // ── 4. Add FK: Tickets → SupportTeams ────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Tickets_SupportTeams_AssignedTeamId')
                BEGIN
                    ALTER TABLE Tickets
                    ADD CONSTRAINT FK_Tickets_SupportTeams_AssignedTeamId
                        FOREIGN KEY (AssignedTeamId) REFERENCES SupportTeams(Id)
                        ON DELETE SET NULL;
                END
            ");

            // ── 5. Update FK_Tickets_TicketCategories to RESTRICT (no cascade) ──────────────
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = 'FK_Tickets_TicketCategories_CategoryId')
                BEGIN
                    ALTER TABLE Tickets DROP CONSTRAINT FK_Tickets_TicketCategories_CategoryId;
                END
                ALTER TABLE Tickets
                ADD CONSTRAINT FK_Tickets_TicketCategories_CategoryId
                    FOREIGN KEY (CategoryId) REFERENCES TicketCategories(Id);
            ");

            // ── 6. Add missing indexes ────────────────────────────────────────────────────────
            // Note: IX_Tickets_AssignedToUserId_Status and IX_Tickets_MemberId_Status are skipped
            // because AssignedToUserId and MemberId are nvarchar(max) from InitialCreate (not indexable).
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_TicketNumber' AND object_id = OBJECT_ID('Tickets'))
                    CREATE UNIQUE INDEX IX_Tickets_TicketNumber ON Tickets(TicketNumber);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_AssignedTeamId_Status' AND object_id = OBJECT_ID('Tickets'))
                    CREATE INDEX IX_Tickets_AssignedTeamId_Status ON Tickets(AssignedTeamId, Status);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_CategoryId_Status' AND object_id = OBJECT_ID('Tickets'))
                    CREATE INDEX IX_Tickets_CategoryId_Status ON Tickets(CategoryId, Status);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_SlaPolicyId' AND object_id = OBJECT_ID('Tickets'))
                    CREATE INDEX IX_Tickets_SlaPolicyId ON Tickets(SlaPolicyId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_Status_CreationDate' AND object_id = OBJECT_ID('Tickets'))
                    CREATE INDEX IX_Tickets_Status_CreationDate ON Tickets(Status, CreationDate);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS IX_Tickets_TicketNumber ON Tickets;
                DROP INDEX IF EXISTS IX_Tickets_AssignedTeamId_Status ON Tickets;
                DROP INDEX IF EXISTS IX_Tickets_CategoryId_Status ON Tickets;
                DROP INDEX IF EXISTS IX_Tickets_SlaPolicyId ON Tickets;
                DROP INDEX IF EXISTS IX_Tickets_Status_CreationDate ON Tickets;
            ");

            // Drop FKs added here
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_SlaPolicies_SlaPolicyId')
                    ALTER TABLE Tickets DROP CONSTRAINT FK_Tickets_SlaPolicies_SlaPolicyId;
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_SupportTeams_AssignedTeamId')
                    ALTER TABLE Tickets DROP CONSTRAINT FK_Tickets_SupportTeams_AssignedTeamId;
            ");

            // Drop default constraints
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_TicketNumber')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_TicketNumber;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_Channel')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_Channel;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_EscalationLevel')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_EscalationLevel;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_IsSlaFRBreached')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_IsSlaFRBreached;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_IsSlaResBreached')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_IsSlaResBreached;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_IsSlaPaused')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_IsSlaPaused;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_SlaPausedMinutes')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_SlaPausedMinutes;
                IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Tickets_FollowUpSent')
                    ALTER TABLE Tickets DROP CONSTRAINT DF_Tickets_FollowUpSent;
            ");

            // Drop added columns
            migrationBuilder.Sql(@"
                ALTER TABLE Tickets DROP COLUMN TicketNumber;
                ALTER TABLE Tickets DROP COLUMN AssignedTeamId;
                ALTER TABLE Tickets DROP COLUMN Subcategory;
                ALTER TABLE Tickets DROP COLUMN Channel;
                ALTER TABLE Tickets DROP COLUMN EscalationLevel;
                ALTER TABLE Tickets DROP COLUMN Language;
                ALTER TABLE Tickets DROP COLUMN CustomerTier;
                ALTER TABLE Tickets DROP COLUMN SlaPolicyId;
                ALTER TABLE Tickets DROP COLUMN SlaFirstResponseDue;
                ALTER TABLE Tickets DROP COLUMN SlaFirstResponseAt;
                ALTER TABLE Tickets DROP COLUMN SlaResolutionDue;
                ALTER TABLE Tickets DROP COLUMN IsSlaFirstResponseBreached;
                ALTER TABLE Tickets DROP COLUMN IsSlaResolutionBreached;
                ALTER TABLE Tickets DROP COLUMN IsSlaPaused;
                ALTER TABLE Tickets DROP COLUMN SlaPausedAt;
                ALTER TABLE Tickets DROP COLUMN SlaPausedMinutes;
                ALTER TABLE Tickets DROP COLUMN ResolutionSummary;
                ALTER TABLE Tickets DROP COLUMN ResolvedByAgentId;
                ALTER TABLE Tickets DROP COLUMN CsatScore;
                ALTER TABLE Tickets DROP COLUMN CsatComment;
                ALTER TABLE Tickets DROP COLUMN CsatSubmittedAt;
                ALTER TABLE Tickets DROP COLUMN FollowUpSent;
                ALTER TABLE Tickets DROP COLUMN ClosedAt;
            ");
        }
    }
}
