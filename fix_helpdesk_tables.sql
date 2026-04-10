-- =============================================================
-- fix_helpdesk_tables.sql
-- Creates missing helpdesk tables + applies AddProductThemeClass
-- Safe to run: uses IF NOT EXISTS guards throughout
-- After running, re-insert both migration history entries
-- =============================================================

-- -------------------------------------------------------
-- 1. SlaPolicies
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SlaPolicies')
BEGIN
    CREATE TABLE [SlaPolicies] (
        [Id]                           nvarchar(36)    NOT NULL,
        [Name]                         nvarchar(200)   NOT NULL,
        [Description]                  nvarchar(500)   NULL,
        [FirstResponseCriticalMinutes] int             NOT NULL,
        [FirstResponseHighMinutes]     int             NOT NULL,
        [FirstResponseNormalMinutes]   int             NOT NULL,
        [FirstResponseLowMinutes]      int             NOT NULL,
        [ResolutionCriticalMinutes]    int             NOT NULL,
        [ResolutionHighMinutes]        int             NOT NULL,
        [ResolutionNormalMinutes]      int             NOT NULL,
        [ResolutionLowMinutes]         int             NOT NULL,
        [Timezone]                     nvarchar(100)   NOT NULL,
        [WorkdaysJson]                 nvarchar(50)    NOT NULL,
        [BusinessHoursStart]           nvarchar(10)    NOT NULL,
        [BusinessHoursEnd]             nvarchar(10)    NOT NULL,
        [WarningThresholdPercent]      int             NOT NULL,
        [NotifyAgentAtMinutes]         int             NOT NULL,
        [NotifySupervisorAtMinutes]    int             NOT NULL,
        [NotifyManagerAtMinutes]       int             NOT NULL,
        [IsDefault]                    bit             NOT NULL,
        [IsActive]                     bit             NOT NULL,
        [CreationDate]                 datetime2       NOT NULL,
        [CreatedBy]                    nvarchar(max)   NOT NULL,
        [LastUpdateDate]               datetime2       NOT NULL,
        [LastUpdateBy]                 nvarchar(max)   NULL,
        [IsDeleted]                    bit             NOT NULL,
        [DeletedAt]                    datetime2       NULL,
        [DeletedBy]                    nvarchar(max)   NULL,
        [RowVersion]                   rowversion      NULL,
        CONSTRAINT [PK_SlaPolicies] PRIMARY KEY ([Id])
    );
    PRINT 'Created table: SlaPolicies';
END
ELSE
    PRINT 'Table already exists: SlaPolicies';

-- -------------------------------------------------------
-- 2. SupportTeams
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupportTeams')
BEGIN
    CREATE TABLE [SupportTeams] (
        [Id]                  int           NOT NULL IDENTITY(1,1),
        [Name]                nvarchar(200) NOT NULL,
        [Description]         nvarchar(500) NULL,
        [SupervisorAgentId]   nvarchar(450) NULL,
        [RoutingMethod]       nvarchar(30)  NOT NULL,
        [IsActive]            bit           NOT NULL,
        [CreationDate]        datetime2     NOT NULL,
        [CreatedBy]           nvarchar(max) NOT NULL,
        [LastUpdateDate]      datetime2     NULL,
        [LastUpdateBy]        nvarchar(max) NULL,
        CONSTRAINT [PK_SupportTeams] PRIMARY KEY ([Id])
    );
    PRINT 'Created table: SupportTeams';
END
ELSE
    PRINT 'Table already exists: SupportTeams';

-- -------------------------------------------------------
-- 3. TicketMetrics
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketMetrics')
BEGIN
    CREATE TABLE [TicketMetrics] (
        [Id]                      int           NOT NULL IDENTITY(1,1),
        [Date]                    datetime2     NOT NULL,
        [TotalCreated]            int           NOT NULL,
        [TotalResolved]           int           NOT NULL,
        [TotalClosed]             int           NOT NULL,
        [AvgFirstResponseMinutes] float         NOT NULL,
        [AvgResolutionMinutes]    float         NOT NULL,
        [FirstContactResolutionRate] float      NOT NULL,
        [SlaComplianceRate]       float         NOT NULL,
        [CsatAverage]             float         NOT NULL,
        [CsatResponseCount]       int           NOT NULL,
        [FrtBreaches]             int           NOT NULL,
        [ResolutionBreaches]      int           NOT NULL,
        [DeflectionAttempts]      int           NOT NULL,
        [DeflectionSuccesses]     int           NOT NULL,
        [TicketsByPriorityJson]   nvarchar(2000) NOT NULL,
        [TicketsByCategoryJson]   nvarchar(2000) NOT NULL,
        [TicketsByChannelJson]    nvarchar(1000) NOT NULL,
        [TicketsByAgentJson]      nvarchar(4000) NOT NULL,
        [CreationDate]            datetime2     NOT NULL,
        [CreatedBy]               nvarchar(max) NOT NULL,
        [LastUpdateDate]          datetime2     NULL,
        [LastUpdateBy]            nvarchar(max) NULL,
        CONSTRAINT [PK_TicketMetrics] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_TicketMetrics_Date] ON [TicketMetrics] ([Date]);
    PRINT 'Created table: TicketMetrics';
END
ELSE
    PRINT 'Table already exists: TicketMetrics';

-- -------------------------------------------------------
-- 4. TicketSequences
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketSequences')
BEGIN
    CREATE TABLE [TicketSequences] (
        [Date]          date NOT NULL,
        [LastSequence]  int  NOT NULL,
        CONSTRAINT [PK_TicketSequences] PRIMARY KEY ([Date])
    );
    PRINT 'Created table: TicketSequences';
END
ELSE
    PRINT 'Table already exists: TicketSequences';

-- -------------------------------------------------------
-- 5. CannedResponses (FK -> SupportTeams)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CannedResponses')
BEGIN
    CREATE TABLE [CannedResponses] (
        [Id]             nvarchar(450) NOT NULL,
        [Title]          nvarchar(200) NOT NULL,
        [Body]           nvarchar(max) NOT NULL,
        [Category]       nvarchar(100) NULL,
        [TagsJson]       nvarchar(500) NOT NULL,
        [Scope]          nvarchar(20)  NOT NULL,
        [OwnerAgentId]   nvarchar(450) NULL,
        [TeamId]         int           NULL,
        [UsageCount]     int           NOT NULL,
        [IsActive]       bit           NOT NULL,
        [CreationDate]   datetime2     NOT NULL,
        [CreatedBy]      nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2     NOT NULL,
        [LastUpdateBy]   nvarchar(max) NULL,
        [IsDeleted]      bit           NOT NULL,
        [DeletedAt]      datetime2     NULL,
        [DeletedBy]      nvarchar(max) NULL,
        [RowVersion]     rowversion    NULL,
        CONSTRAINT [PK_CannedResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CannedResponses_SupportTeams_TeamId]
            FOREIGN KEY ([TeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL
    );
    CREATE INDEX [IX_CannedResponses_TeamId] ON [CannedResponses] ([TeamId]);
    PRINT 'Created table: CannedResponses';
END
ELSE
    PRINT 'Table already exists: CannedResponses';

-- -------------------------------------------------------
-- 6. SupportAgents (FK -> SupportTeams)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupportAgents')
BEGIN
    CREATE TABLE [SupportAgents] (
        [Id]                    nvarchar(450) NOT NULL,
        [UserId]                nvarchar(450) NOT NULL,
        [MemberId]              nvarchar(50)  NOT NULL,
        [DisplayName]           nvarchar(200) NOT NULL,
        [Email]                 nvarchar(200) NOT NULL,
        [TeamId]                int           NULL,
        [Tier]                  int           NOT NULL,
        [SkillsJson]            nvarchar(1000) NOT NULL,
        [LanguagesJson]         nvarchar(200) NOT NULL,
        [MaxConcurrentTickets]  int           NOT NULL,
        [CurrentTicketCount]    int           NOT NULL,
        [Availability]          nvarchar(20)  NOT NULL,
        [IsActive]              bit           NOT NULL,
        [CreationDate]          datetime2     NOT NULL,
        [CreatedBy]             nvarchar(max) NOT NULL,
        [LastUpdateDate]        datetime2     NOT NULL,
        [LastUpdateBy]          nvarchar(max) NULL,
        [IsDeleted]             bit           NOT NULL,
        [DeletedAt]             datetime2     NULL,
        [DeletedBy]             nvarchar(max) NULL,
        [RowVersion]            rowversion    NULL,
        CONSTRAINT [PK_SupportAgents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportAgents_SupportTeams_TeamId]
            FOREIGN KEY ([TeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL
    );
    CREATE UNIQUE INDEX [IX_SupportAgents_UserId] ON [SupportAgents] ([UserId]);
    CREATE INDEX [IX_SupportAgents_TeamId] ON [SupportAgents] ([TeamId]);
    PRINT 'Created table: SupportAgents';
END
ELSE
    PRINT 'Table already exists: SupportAgents';

-- -------------------------------------------------------
-- 7. TicketCategories
-- Table may already exist from a prior migration with fewer columns.
-- We CREATE it if absent, or ALTER to add missing columns if present.
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketCategories')
BEGIN
    CREATE TABLE [TicketCategories] (
        [Id]                  int           NOT NULL IDENTITY(1,1),
        [Name]                nvarchar(200) NOT NULL,
        [Description]         nvarchar(500) NULL,
        [ParentCategoryId]    int           NULL,
        [DefaultTeamId]       int           NULL,
        [DefaultPriority]     nvarchar(20)  NOT NULL DEFAULT 'Normal',
        [DefaultSlaPolicyId]  nvarchar(36)  NULL,
        [SortOrder]           int           NOT NULL DEFAULT 0,
        [IsActive]            bit           NOT NULL DEFAULT 1,
        [CreationDate]        datetime2     NOT NULL,
        [CreatedBy]           nvarchar(max) NOT NULL,
        [LastUpdateDate]      datetime2     NULL,
        [LastUpdateBy]        nvarchar(max) NULL,
        CONSTRAINT [PK_TicketCategories] PRIMARY KEY ([Id])
    );
    PRINT 'Created table: TicketCategories';
END
ELSE
    PRINT 'Table TicketCategories already exists — checking for missing columns';

-- Add missing columns if they do not exist (safe ALTER path for pre-existing table)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'Description')
    ALTER TABLE [TicketCategories] ADD [Description] nvarchar(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'DefaultPriority')
    ALTER TABLE [TicketCategories] ADD [DefaultPriority] nvarchar(20) NOT NULL DEFAULT 'Normal';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'DefaultSlaPolicyId')
    ALTER TABLE [TicketCategories] ADD [DefaultSlaPolicyId] nvarchar(36) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'DefaultTeamId')
    ALTER TABLE [TicketCategories] ADD [DefaultTeamId] int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'ParentCategoryId')
    ALTER TABLE [TicketCategories] ADD [ParentCategoryId] int NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'SortOrder')
    ALTER TABLE [TicketCategories] ADD [SortOrder] int NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'IsActive')
    ALTER TABLE [TicketCategories] ADD [IsActive] bit NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'LastUpdateDate')
    ALTER TABLE [TicketCategories] ADD [LastUpdateDate] datetime2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TicketCategories') AND name = 'LastUpdateBy')
    ALTER TABLE [TicketCategories] ADD [LastUpdateBy] nvarchar(max) NULL;

-- Add FKs only if they don't already exist
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId')
    ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId]
        FOREIGN KEY ([DefaultSlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TicketCategories_SupportTeams_DefaultTeamId')
    ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SupportTeams_DefaultTeamId]
        FOREIGN KEY ([DefaultTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TicketCategories_TicketCategories_ParentCategoryId')
    ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_TicketCategories_ParentCategoryId]
        FOREIGN KEY ([ParentCategoryId]) REFERENCES [TicketCategories] ([Id]);

-- Indexes
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketCategories_DefaultSlaPolicyId' AND object_id = OBJECT_ID('TicketCategories'))
    CREATE INDEX [IX_TicketCategories_DefaultSlaPolicyId] ON [TicketCategories] ([DefaultSlaPolicyId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketCategories_DefaultTeamId' AND object_id = OBJECT_ID('TicketCategories'))
    CREATE INDEX [IX_TicketCategories_DefaultTeamId] ON [TicketCategories] ([DefaultTeamId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketCategories_ParentCategoryId' AND object_id = OBJECT_ID('TicketCategories'))
    CREATE INDEX [IX_TicketCategories_ParentCategoryId] ON [TicketCategories] ([ParentCategoryId]);

-- Seed a default category using dynamic SQL to bypass compile-time column validation
-- (safe: only inserts if no rows exist)
IF NOT EXISTS (SELECT 1 FROM [TicketCategories])
    EXEC sp_executesql N'INSERT INTO [TicketCategories] ([Name],[Description],[DefaultPriority],[SortOrder],[IsActive],[CreationDate],[CreatedBy]) VALUES (''General'', ''General support'', ''Normal'', 1, 1, GETUTCDATE(), ''system'')';

PRINT 'TicketCategories: columns and indexes verified';

-- -------------------------------------------------------
-- 8. KbArticles (FK -> TicketCategories)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KbArticles')
BEGIN
    CREATE TABLE [KbArticles] (
        [Id]               nvarchar(450) NOT NULL,
        [Title]            nvarchar(300) NOT NULL,
        [Slug]             nvarchar(300) NOT NULL,
        [Body]             nvarchar(max) NOT NULL,
        [CategoryId]       int           NOT NULL,
        [TagsJson]         nvarchar(1000) NOT NULL,
        [Visibility]       int           NOT NULL,
        [AuthorAgentId]    nvarchar(max) NOT NULL,
        [ViewCount]        int           NOT NULL,
        [HelpfulCount]     int           NOT NULL,
        [NotHelpfulCount]  int           NOT NULL,
        [SourceTicketId]   nvarchar(36)  NULL,
        [PublishedAt]      datetime2     NULL,
        [Version]          int           NOT NULL,
        [CreationDate]     datetime2     NOT NULL,
        [CreatedBy]        nvarchar(max) NOT NULL,
        [LastUpdateDate]   datetime2     NOT NULL,
        [LastUpdateBy]     nvarchar(max) NULL,
        [IsDeleted]        bit           NOT NULL,
        [DeletedAt]        datetime2     NULL,
        [DeletedBy]        nvarchar(max) NULL,
        [RowVersion]       rowversion    NULL,
        CONSTRAINT [PK_KbArticles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KbArticles_TicketCategories_CategoryId]
            FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategories] ([Id])
    );
    CREATE UNIQUE INDEX [IX_KbArticles_Slug] ON [KbArticles] ([Slug]);
    CREATE INDEX [IX_KbArticles_CategoryId] ON [KbArticles] ([CategoryId]);
    CREATE INDEX [IX_KbArticles_HelpfulCount] ON [KbArticles] ([HelpfulCount]);
    CREATE INDEX [IX_KbArticles_Visibility_CategoryId] ON [KbArticles] ([Visibility],[CategoryId]);
    PRINT 'Created table: KbArticles';
END
ELSE
    PRINT 'Table already exists: KbArticles';

-- -------------------------------------------------------
-- 9. Tickets (FK -> SlaPolicies, SupportTeams, TicketCategories)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tickets')
BEGIN
    CREATE TABLE [Tickets] (
        [Id]                       nvarchar(450) NOT NULL,
        [TicketNumber]             nvarchar(20)  NOT NULL,
        [MemberId]                 nvarchar(450) NOT NULL,
        [AssignedToUserId]         nvarchar(450) NULL,
        [AssignedTeamId]           int           NULL,
        [CategoryId]               int           NOT NULL,
        [Subcategory]              nvarchar(100) NULL,
        [Priority]                 int           NOT NULL,
        [Status]                   int           NOT NULL,
        [Channel]                  int           NOT NULL,
        [EscalationLevel]          int           NOT NULL,
        [Subject]                  nvarchar(300) NOT NULL,
        [Body]                     nvarchar(max) NOT NULL,
        [MergedIntoTicketId]       nvarchar(36)  NULL,
        [Language]                 nvarchar(10)  NULL,
        [CustomerTier]             nvarchar(50)  NULL,
        [SlaPolicyId]              nvarchar(36)  NULL,
        [SlaFirstResponseDue]      datetime2     NULL,
        [SlaFirstResponseAt]       datetime2     NULL,
        [SlaResolutionDue]         datetime2     NULL,
        [IsSlaFirstResponseBreached] bit         NOT NULL,
        [IsSlaResolutionBreached]  bit           NOT NULL,
        [IsSlaPaused]              bit           NOT NULL,
        [SlaPausedAt]              datetime2     NULL,
        [SlaPausedMinutes]         float         NOT NULL,
        [ResolvedAt]               datetime2     NULL,
        [ResolutionSummary]        nvarchar(2000) NULL,
        [ResolvedByAgentId]        nvarchar(450) NULL,
        [CsatScore]                int           NULL,
        [CsatComment]              nvarchar(1000) NULL,
        [CsatSubmittedAt]          datetime2     NULL,
        [FollowUpSent]             bit           NOT NULL,
        [ClosedAt]                 datetime2     NULL,
        [CreationDate]             datetime2     NOT NULL,
        [CreatedBy]                nvarchar(max) NOT NULL,
        [LastUpdateDate]           datetime2     NOT NULL,
        [LastUpdateBy]             nvarchar(max) NULL,
        [IsDeleted]                bit           NOT NULL,
        [DeletedAt]                datetime2     NULL,
        [DeletedBy]                nvarchar(max) NULL,
        [RowVersion]               rowversion    NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tickets_SlaPolicies_SlaPolicyId]
            FOREIGN KEY ([SlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_SupportTeams_AssignedTeamId]
            FOREIGN KEY ([AssignedTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_TicketCategories_CategoryId]
            FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategories] ([Id])
    );
    CREATE UNIQUE INDEX [IX_Tickets_TicketNumber] ON [Tickets] ([TicketNumber]);
    CREATE INDEX [IX_Tickets_MemberId_CreationDate] ON [Tickets] ([MemberId],[CreationDate]);
    CREATE INDEX [IX_Tickets_AssignedTeamId_Status] ON [Tickets] ([AssignedTeamId],[Status]);
    CREATE INDEX [IX_Tickets_AssignedToUserId_Status] ON [Tickets] ([AssignedToUserId],[Status]);
    CREATE INDEX [IX_Tickets_CategoryId] ON [Tickets] ([CategoryId]);
    CREATE INDEX [IX_Tickets_SlaPolicyId] ON [Tickets] ([SlaPolicyId]);
    CREATE INDEX [IX_Tickets_Status_LastUpdateDate] ON [Tickets] ([Status],[LastUpdateDate]);
    PRINT 'Created table: Tickets';
END
ELSE
    PRINT 'Table already exists: Tickets';

-- -------------------------------------------------------
-- 10. KbArticleVersions (FK -> KbArticles)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'KbArticleVersions')
BEGIN
    CREATE TABLE [KbArticleVersions] (
        [Id]              bigint        NOT NULL IDENTITY(1,1),
        [ArticleId]       nvarchar(450) NOT NULL,
        [VersionNumber]   int           NOT NULL,
        [BodySnapshot]    nvarchar(max) NOT NULL,
        [EditedByAgentId] nvarchar(max) NOT NULL,
        [ChangeNote]      nvarchar(max) NULL,
        [CreationDate]    datetime2     NOT NULL,
        [CreatedBy]       nvarchar(max) NOT NULL,
        CONSTRAINT [PK_KbArticleVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KbArticleVersions_KbArticles_ArticleId]
            FOREIGN KEY ([ArticleId]) REFERENCES [KbArticles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_KbArticleVersions_ArticleId] ON [KbArticleVersions] ([ArticleId]);
    PRINT 'Created table: KbArticleVersions';
END
ELSE
    PRINT 'Table already exists: KbArticleVersions';

-- -------------------------------------------------------
-- 11. SlaBreaches (FK -> Tickets)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SlaBreaches')
BEGIN
    CREATE TABLE [SlaBreaches] (
        [Id]                    bigint        NOT NULL IDENTITY(1,1),
        [TicketId]              nvarchar(450) NOT NULL,
        [SlaPolicyId]           nvarchar(max) NOT NULL,
        [MetricType]            int           NOT NULL,
        [DueAt]                 datetime2     NOT NULL,
        [BreachedAt]            datetime2     NOT NULL,
        [BreachDurationMinutes] int           NOT NULL,
        [AssignedAgentId]       nvarchar(450) NULL,
        [AssignedTeamId]        int           NULL,
        [CreationDate]          datetime2     NOT NULL,
        [CreatedBy]             nvarchar(max) NOT NULL,
        CONSTRAINT [PK_SlaBreaches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SlaBreaches_Tickets_TicketId]
            FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_SlaBreaches_TicketId] ON [SlaBreaches] ([TicketId]);
    CREATE INDEX [IX_SlaBreaches_BreachedAt] ON [SlaBreaches] ([BreachedAt]);
    PRINT 'Created table: SlaBreaches';
END
ELSE
    PRINT 'Table already exists: SlaBreaches';

-- -------------------------------------------------------
-- 12. TicketAttachments (FK -> Tickets)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketAttachments')
BEGIN
    CREATE TABLE [TicketAttachments] (
        [Id]            bigint        NOT NULL IDENTITY(1,1),
        [TicketId]      nvarchar(450) NOT NULL,
        [FileName]      nvarchar(max) NOT NULL,
        [FileUrl]       nvarchar(max) NOT NULL,
        [FileSizeBytes] bigint        NOT NULL,
        [ContentType]   nvarchar(max) NOT NULL,
        [CreationDate]  datetime2     NOT NULL,
        [CreatedBy]     nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAttachments_Tickets_TicketId]
            FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TicketAttachments_TicketId] ON [TicketAttachments] ([TicketId]);
    PRINT 'Created table: TicketAttachments';
END
ELSE
    PRINT 'Table already exists: TicketAttachments';

-- -------------------------------------------------------
-- 13. TicketComments (FK -> Tickets)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketComments')
BEGIN
    CREATE TABLE [TicketComments] (
        [Id]           bigint        NOT NULL IDENTITY(1,1),
        [TicketId]     nvarchar(450) NOT NULL,
        [AuthorId]     nvarchar(max) NOT NULL,
        [AuthorType]   nvarchar(20)  NOT NULL,
        [Body]         nvarchar(max) NOT NULL,
        [IsInternal]   bit           NOT NULL,
        [CreationDate] datetime2     NOT NULL,
        [CreatedBy]    nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketComments_Tickets_TicketId]
            FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TicketComments_TicketId_CreationDate] ON [TicketComments] ([TicketId],[CreationDate]);
    CREATE INDEX [IX_TicketComments_IsInternal] ON [TicketComments] ([IsInternal]);
    PRINT 'Created table: TicketComments';
END
ELSE
    PRINT 'Table already exists: TicketComments';

-- -------------------------------------------------------
-- 14. TicketHistories (FK -> Tickets)
-- -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TicketHistories')
BEGIN
    CREATE TABLE [TicketHistories] (
        [Id]             bigint        NOT NULL IDENTITY(1,1),
        [TicketId]       nvarchar(450) NOT NULL,
        [Field]          nvarchar(100) NOT NULL,
        [OldValue]       nvarchar(500) NULL,
        [NewValue]       nvarchar(500) NULL,
        [ChangedByType]  nvarchar(20)  NOT NULL,
        [ChangedById]    nvarchar(450) NULL,
        [ChangeReason]   nvarchar(500) NULL,
        [CreationDate]   datetime2     NOT NULL,
        [CreatedBy]      nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketHistories_Tickets_TicketId]
            FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TicketHistories_TicketId_CreationDate] ON [TicketHistories] ([TicketId],[CreationDate]);
    PRINT 'Created table: TicketHistories';
END
ELSE
    PRINT 'Table already exists: TicketHistories';

-- =============================================================
-- AddProductThemeClass migration changes
-- =============================================================

-- Alter Products.Description to nvarchar(max)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Products') AND name = 'Description'
      AND max_length != -1
)
BEGIN
    ALTER TABLE [Products] ALTER COLUMN [Description] nvarchar(max) NOT NULL;
    PRINT 'Altered Products.Description to nvarchar(max)';
END

-- Alter Products.DescriptionPromo to nvarchar(max)
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Products') AND name = 'DescriptionPromo'
      AND max_length != -1
)
BEGIN
    ALTER TABLE [Products] ALTER COLUMN [DescriptionPromo] nvarchar(max) NULL;
    PRINT 'Altered Products.DescriptionPromo to nvarchar(max)';
END

-- Add Products.ThemeClass if it doesn't exist
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Products') AND name = 'ThemeClass'
)
BEGIN
    ALTER TABLE [Products] ADD [ThemeClass] nvarchar(50) NULL;

    UPDATE [Products] SET [ThemeClass] = 'theme-product-guest'
    WHERE [Id] = '00000001-prod-0000-0000-000000000001';

    UPDATE [Products] SET [ThemeClass] = 'theme-product-vip'
    WHERE [Id] = '00000002-prod-0000-0000-000000000002';

    UPDATE [Products] SET [ThemeClass] = 'theme-product-elite'
    WHERE [Id] = '00000003-prod-0000-0000-000000000003';

    UPDATE [Products] SET [ThemeClass] = 'theme-product-turbo'
    WHERE [Id] = '00000004-prod-0000-0000-000000000004';

    PRINT 'Added Products.ThemeClass column with seed data';
END
ELSE
    PRINT 'Column already exists: Products.ThemeClass';

-- =============================================================
-- Re-insert migration history entries
-- =============================================================
IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = '20260405130705_HelpdeskModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260405130705_HelpdeskModule', '9.0.3');
    PRINT 'Inserted migration history: 20260405130705_HelpdeskModule';
END
ELSE
    PRINT 'Migration history already exists: 20260405130705_HelpdeskModule';

IF NOT EXISTS (
    SELECT 1 FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = '20260406183238_AddProductThemeClass'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20260406183238_AddProductThemeClass', '9.0.3');
    PRINT 'Inserted migration history: 20260406183238_AddProductThemeClass';
END
ELSE
    PRINT 'Migration history already exists: 20260406183238_AddProductThemeClass';

PRINT '=== Done ===';
