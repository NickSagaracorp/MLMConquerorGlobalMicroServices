IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditTracking] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityName] nvarchar(max) NOT NULL,
        [EntityId] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [ChangedBy] nvarchar(max) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditTracking] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionOperationType] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionOperationType] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CorporateEvents] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [EventDate] datetime2 NOT NULL,
        [Location] nvarchar(max) NULL,
        [ImageUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CorporateEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CorporatePromos] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [BannerUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CorporatePromos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CreditCards] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [MaskedCardNumber] nvarchar(max) NOT NULL,
        [Last4] nvarchar(max) NOT NULL,
        [First6] nvarchar(max) NOT NULL,
        [CardBrand] nvarchar(max) NOT NULL,
        [ExpiryMonth] int NOT NULL,
        [ExpiryYear] int NOT NULL,
        [Gateway] nvarchar(max) NOT NULL,
        [GatewayToken] nvarchar(max) NOT NULL,
        [CardToken] nvarchar(max) NOT NULL,
        [IsExpired] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CreditCards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [DualTeamTree] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [ParentMemberId] nvarchar(max) NULL,
        [Side] int NOT NULL,
        [HierarchyPath] nvarchar(2000) NOT NULL,
        [LeftLegPoints] decimal(18,4) NOT NULL,
        [RightLegPoints] decimal(18,4) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_DualTeamTree] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [ErrorLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [ApiName] nvarchar(max) NOT NULL,
        [Endpoint] nvarchar(max) NOT NULL,
        [CodeSection] nvarchar(max) NOT NULL,
        [ErrorCode] nvarchar(max) NOT NULL,
        [TechnicalMessage] nvarchar(max) NOT NULL,
        [FullException] nvarchar(max) NULL,
        [InnerException] nvarchar(max) NULL,
        [MemberId] nvarchar(max) NULL,
        [RequestData] nvarchar(max) NULL,
        [TraceId] nvarchar(max) NULL,
        [Language] nvarchar(max) NOT NULL,
        [HttpStatusCode] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [IsResolved] bit NOT NULL,
        [ResolvedBy] nvarchar(max) NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ErrorLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [ErrorMessages] (
        [Id] int NOT NULL IDENTITY,
        [ErrorCode] nvarchar(100) NOT NULL,
        [Language] nvarchar(10) NOT NULL,
        [UserFriendlyMessage] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ErrorMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [GenealogyTree] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [ParentMemberId] nvarchar(max) NULL,
        [HierarchyPath] nvarchar(2000) NOT NULL,
        [Level] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_GenealogyTree] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [GhostPoints] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [LegMemberId] nvarchar(450) NOT NULL,
        [Side] int NOT NULL,
        [Points] decimal(18,4) NOT NULL,
        [AdminNote] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_GhostPoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberIdentificationTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MemberIdentificationTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MembershipLevels] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Price] decimal(10,2) NOT NULL,
        [RenewalPrice] decimal(10,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsFree] bit NOT NULL,
        [IsAutoRenew] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MembershipLevels] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberStatistics] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [PersonalPoints] int NOT NULL,
        [ExternalCustomerPoints] int NOT NULL,
        [DualTeamSize] int NOT NULL,
        [EnrollmentTeamSize] int NOT NULL,
        [DualTeamPoints] int NOT NULL,
        [EnrollmentPoints] int NOT NULL,
        [QualifiedSponsoredMembers] int NOT NULL,
        [QualifiedSponsoredExternalCustomers] int NOT NULL,
        [EnrollmentTeamGrowth] int NOT NULL,
        [DualteamGrowth] int NOT NULL,
        [EnrollmentTeamPointsGrowth] int NOT NULL,
        [DualTeamPointsGrowth] int NOT NULL,
        [CurrentWeekIncomeGrowth] decimal(18,2) NOT NULL,
        [CurrentMonthIncomeGrowth] decimal(18,2) NOT NULL,
        [CurrentYearIncomeGrowth] decimal(18,2) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberStatistics] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [MembershipSubscriptionId] nvarchar(max) NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CheckoutScreenshotUrl] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [PlacementLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [PlacedUnderMemberId] nvarchar(max) NOT NULL,
        [Side] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NULL,
        [UnplacementCount] int NOT NULL,
        [FirstPlacementDate] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PlacementLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductLoyaltySettings] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] nvarchar(max) NOT NULL,
        [PointsPerUnit] decimal(18,2) NOT NULL,
        [RequiredSuccessfulPayments] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductLoyaltySettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [RankDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [Status] int NOT NULL,
        [CertificateTemplateUrl] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RankDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TicketCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TokenTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(450) NOT NULL,
        [TokenTypeId] int NOT NULL,
        [TransactionType] int NOT NULL,
        [Quantity] int NOT NULL,
        [DistributedToMemberId] nvarchar(max) NULL,
        [UsedByMemberId] nvarchar(max) NULL,
        [UsedAt] datetime2 NULL,
        [GeneratedPdfUrl] nvarchar(max) NULL,
        [ReferenceId] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TokenTransactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TokenTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsGuestPass] bit NOT NULL,
        [TemplateUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [WalletHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [WalletId] nvarchar(max) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [WalletType] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangeReason] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_WalletHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [Wallets] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [WalletType] int NOT NULL,
        [Status] int NOT NULL,
        [AccountIdentifier] nvarchar(max) NULL,
        [eWalletPasswordEncrypted] nvarchar(max) NULL,
        [IsPreferred] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_Wallets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionTypes] (
        [Id] int NOT NULL IDENTITY,
        [CommissionCategoryId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Percentage] decimal(10,2) NOT NULL,
        [PaymentDelayDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsRealTime] bit NOT NULL,
        [IsPaidOnSignup] bit NOT NULL,
        [IsPaidOnRenewal] bit NOT NULL,
        [Cummulative] bit NOT NULL,
        [TriggerOrder] int NOT NULL,
        [NewMembers] int NOT NULL,
        [DaysAfterJoining] int NOT NULL,
        [MembersRebill] int NOT NULL,
        [LifeTimeRank] int NOT NULL,
        [CurrentRank] int NOT NULL,
        [LevelNo] int NOT NULL,
        [ResidualBased] bit NOT NULL,
        [ResidualOverCommissionType] int NOT NULL,
        [ResidualPercentage] float NOT NULL,
        [PersonalPoints] int NOT NULL,
        [TeamPoints] int NOT NULL,
        [MaxTeamPointsPerBranch] float NOT NULL,
        [EnrollmentTeam] int NOT NULL,
        [MaxEnrollmentTeamPointsPerBranch] float NOT NULL,
        [ExternalMembers] int NOT NULL,
        [SponsoredMembers] int NOT NULL,
        [IsSponsorBonus] bit NOT NULL,
        [ReverseId] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommissionTypes_CommissionCategories_CommissionCategoryId] FOREIGN KEY ([CommissionCategoryId]) REFERENCES [CommissionCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MembershipLevelBenefits] (
        [Id] int NOT NULL IDENTITY,
        [MembershipLevelId] int NOT NULL,
        [BenefitName] nvarchar(max) NOT NULL,
        [BenefitDescription] nvarchar(max) NULL,
        [BenefitValue] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MembershipLevelBenefits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MembershipLevelBenefits_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [MonthlyFee] decimal(10,2) NOT NULL,
        [SetupFee] decimal(10,2) NOT NULL,
        [Price90Days] decimal(10,2) NOT NULL,
        [Price180Days] decimal(10,2) NOT NULL,
        [AnnualPrice] decimal(10,2) NOT NULL,
        [DescriptionPromo] nvarchar(2000) NULL,
        [MonthlyFeePromo] decimal(10,2) NOT NULL,
        [SetupFeePromo] decimal(10,2) NOT NULL,
        [ImageUrlPromo] nvarchar(500) NULL,
        [QualificationPoinsPromo] int NOT NULL,
        [QualificationPoins] int NOT NULL,
        [IsActive] bit NOT NULL,
        [OldSystemProductId] int NOT NULL,
        [MembershipLevelId] int NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderDetails] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] nvarchar(max) NOT NULL,
        [ProductId] nvarchar(max) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [OrdersId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderDetails_Orders_OrdersId] FOREIGN KEY ([OrdersId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [PaymentHistories] (
        [Id] nvarchar(450) NOT NULL,
        [OrderId] nvarchar(max) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [GatewayName] nvarchar(max) NOT NULL,
        [GatewayTransactionId] nvarchar(max) NULL,
        [TransactionStatus] int NOT NULL,
        [FailureReason] nvarchar(max) NULL,
        [ProcessedAt] datetime2 NULL,
        [OrdersId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_PaymentHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentHistories_Orders_OrdersId] FOREIGN KEY ([OrdersId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberRankHistories] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [RankDefinitionId] int NOT NULL,
        [PreviousRankId] int NULL,
        [AchievedAt] datetime2 NOT NULL,
        [GeneratedCertificateUrl] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_MemberRankHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MemberRankHistories_RankDefinitions_RankDefinitionId] FOREIGN KEY ([RankDefinitionId]) REFERENCES [RankDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [RankRequirements] (
        [Id] int NOT NULL IDENTITY,
        [RankDefinitionId] int NOT NULL,
        [LevelNo] int NOT NULL,
        [PersonalPoints] int NOT NULL,
        [TeamPoints] int NOT NULL,
        [MaxTeamPointsPerBranch] float NOT NULL,
        [EnrollmentTeam] int NOT NULL,
        [PlacementQualifiedTeamMembers] int NOT NULL,
        [EnrollmentQualifiedTeamMembers] int NOT NULL,
        [MaxEnrollmentTeamPointsPerBranch] float NOT NULL,
        [ExternalMembers] int NOT NULL,
        [SponsoredMembers] int NOT NULL,
        [SalesVolume] decimal(10,2) NOT NULL,
        [RankBonus] decimal(10,2) NOT NULL,
        [DailyBonus] decimal(10,2) NOT NULL,
        [MonthlyBonus] decimal(10,2) NOT NULL,
        [LifetimeHoldingDuration] int NOT NULL,
        [RankDescription] nvarchar(1000) NOT NULL,
        [CurrentRankDescription] nvarchar(1000) NOT NULL,
        [AchievementMessage] nvarchar(1500) NULL,
        [CertificateUrl] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RankRequirements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RankRequirements_RankDefinitions_RankDefinitionId] FOREIGN KEY ([RankDefinitionId]) REFERENCES [RankDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [AssignedToUserId] nvarchar(max) NULL,
        [CategoryId] int NOT NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [Subject] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [MergedIntoTicketId] nvarchar(max) NULL,
        [ResolvedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tickets_TicketCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategories] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionEarnings] (
        [Id] nvarchar(450) NOT NULL,
        [BeneficiaryMemberId] nvarchar(450) NOT NULL,
        [SourceMemberId] nvarchar(max) NULL,
        [SourceOrderId] nvarchar(450) NULL,
        [CommissionTypeId] int NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Status] int NOT NULL,
        [EarnedDate] datetime2 NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [PeriodDate] datetime2 NULL,
        [CommissionOperationTypeId] int NULL,
        [IsManualEntry] bit NOT NULL,
        [CsvImportBatchId] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CommissionEarnings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommissionEarnings_CommissionOperationType_CommissionOperationTypeId] FOREIGN KEY ([CommissionOperationTypeId]) REFERENCES [CommissionOperationType] ([Id]),
        CONSTRAINT [FK_CommissionEarnings_CommissionTypes_CommissionTypeId] FOREIGN KEY ([CommissionTypeId]) REFERENCES [CommissionTypes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCommissionPromos] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [ProductId1] nvarchar(450) NULL,
        [CorporatePromoId] bigint NOT NULL,
        [CorporatePromoId1] nvarchar(450) NOT NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCommissionPromos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCommissionPromos_CorporatePromos_CorporatePromoId1] FOREIGN KEY ([CorporatePromoId1]) REFERENCES [CorporatePromos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductCommissionPromos_Products_ProductId1] FOREIGN KEY ([ProductId1]) REFERENCES [Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductCommissions] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [ProductId1] nvarchar(450) NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCommissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCommissions_Products_ProductId1] FOREIGN KEY ([ProductId1]) REFERENCES [Products] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketAttachments] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [FileUrl] nvarchar(max) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAttachments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TicketComments] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [AuthorId] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [IsInternal] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketComments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionCountDownHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [CountDownId] nvarchar(450) NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberId1] nvarchar(450) NULL,
        [FastStartBonus1Start] datetime2 NOT NULL,
        [FastStartBonus1End] datetime2 NOT NULL,
        [FastStartBonus2Start] datetime2 NOT NULL,
        [FastStartBonus2End] datetime2 NOT NULL,
        [FastStartBonus3Start] datetime2 NOT NULL,
        [FastStartBonus3End] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_CommissionCountDownHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [CommissionCountDowns] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberId1] nvarchar(450) NULL,
        [FastStartBonus1Start] datetime2 NOT NULL,
        [FastStartBonus1End] datetime2 NOT NULL,
        [FastStartBonus2Start] datetime2 NOT NULL,
        [FastStartBonus2End] datetime2 NOT NULL,
        [FastStartBonus3Start] datetime2 NOT NULL,
        [FastStartBonus3End] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CommissionCountDowns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [LoyaltyPoints] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [ProductId] nvarchar(max) NOT NULL,
        [OrderId] nvarchar(max) NOT NULL,
        [PointsEarned] decimal(18,2) NOT NULL,
        [IsLocked] bit NOT NULL,
        [MissedPayment] bit NOT NULL,
        [NumberOfSuccessPayments] int NOT NULL,
        [MonthNo] int NOT NULL,
        [YearNo] int NOT NULL,
        [UnlockedAt] datetime2 NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_LoyaltyPoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberNotifications] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [FirebaseMessageId] nvarchar(max) NULL,
        [IsDelivered] bit NOT NULL,
        [DeliveredAt] datetime2 NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberNotifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberProfiles] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [MemberId] nvarchar(20) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [DateOfBirth] datetime2 NOT NULL,
        [Phone] nvarchar(30) NULL,
        [WhatsApp] nvarchar(max) NULL,
        [Country] nvarchar(100) NOT NULL,
        [State] nvarchar(100) NULL,
        [City] nvarchar(100) NULL,
        [Address] nvarchar(500) NULL,
        [ZipCode] nvarchar(max) NULL,
        [BusinessName] nvarchar(max) NULL,
        [ShowBusinessName] bit NOT NULL,
        [MemberType] int NOT NULL,
        [Status] int NOT NULL,
        [EnrollDate] datetime2 NOT NULL,
        [SponsorMemberId] nvarchar(max) NULL,
        [ReplicateSiteSlug] nvarchar(100) NULL,
        [ProfilePhotoUrl] nvarchar(max) NULL,
        [IsNamePublic] bit NOT NULL,
        [IsEmailPublic] bit NOT NULL,
        [IsPhonePublic] bit NOT NULL,
        [ActiveMembershipId] nvarchar(450) NULL,
        [EnrollmentNodeId] nvarchar(450) NULL,
        [BinaryNodeId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_MemberProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_MemberProfiles_MemberId] UNIQUE ([MemberId]),
        CONSTRAINT [FK_MemberProfiles_DualTeamTree_BinaryNodeId] FOREIGN KEY ([BinaryNodeId]) REFERENCES [DualTeamTree] ([Id]),
        CONSTRAINT [FK_MemberProfiles_GenealogyTree_EnrollmentNodeId] FOREIGN KEY ([EnrollmentNodeId]) REFERENCES [GenealogyTree] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MembershipSubscriptions] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(20) NOT NULL,
        [MembershipLevelId] int NOT NULL,
        [PreviousMembershipLevelId] int NULL,
        [ChangeReason] int NOT NULL,
        [SubscriptionStatus] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [RenewalDate] datetime2 NULL,
        [HoldDate] datetime2 NULL,
        [CancellationDate] datetime2 NULL,
        [IsFree] bit NOT NULL,
        [IsAutoRenew] bit NOT NULL,
        [LastOrderId] nvarchar(max) NULL,
        [LastOrderId1] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_MembershipSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [MemberProfiles] ([MemberId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MembershipSubscriptions_Orders_LastOrderId1] FOREIGN KEY ([LastOrderId1]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [MemberStatusHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [Reason] nvarchar(max) NULL,
        [ChangedAt] datetime2 NOT NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MemberStatusHistories_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE TABLE [TokenBalances] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [TokenTypeId] int NOT NULL,
        [Balance] int NOT NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_TokenBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TokenBalances_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N'INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''One-time bonuses triggered on new member signup.'', CAST(1 AS bit), NULL, NULL, N''Signup Bonuses''),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Recurring commissions calculated on binary team volume.'', CAST(1 AS bit), NULL, NULL, N''Residual Commissions''),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Bonuses awarded for reaching leadership thresholds.'', CAST(1 AS bit), NULL, NULL, N''Leadership Bonuses''),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Negative-amount entries that reverse previously paid commissions.'', CAST(1 AS bit), NULL, NULL, N''Reversals'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'ErrorCode', N'IsActive', N'Language', N'LastUpdateBy', N'LastUpdateDate', N'UserFriendlyMessage') AND [object_id] = OBJECT_ID(N'[ErrorMessages]'))
        SET IDENTITY_INSERT [ErrorMessages] ON;
    EXEC(N'INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DEFAULT'', CAST(1 AS bit), N''en'', NULL, NULL, N''Something went wrong. Please try again or contact support.''),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DEFAULT'', CAST(1 AS bit), N''es'', NULL, NULL, N''Algo salió mal. Intente de nuevo o comuníquese con soporte.''),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INTERNAL_ERROR'', CAST(1 AS bit), N''en'', NULL, NULL, N''An unexpected error occurred. Please try again later.''),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INTERNAL_ERROR'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ocurrió un error inesperado. Por favor intente más tarde.''),
    (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''ORDER_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The requested order could not be found.''),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''ORDER_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la orden solicitada.''),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The member account could not be found.''),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la cuenta del miembro.''),
    (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_ALREADY_EXISTS'', CAST(1 AS bit), N''en'', NULL, NULL, N''An account with this information already exists.''),
    (10, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_ALREADY_EXISTS'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ya existe una cuenta con esta información.''),
    (11, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''SPONSOR_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The sponsor ID you entered could not be found. Please verify and try again.''),
    (12, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''SPONSOR_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''El ID de patrocinador ingresado no fue encontrado. Verifíquelo e intente de nuevo.''),
    (13, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DUPLICATE_REPLICATE_SITE'', CAST(1 AS bit), N''en'', NULL, NULL, N''This website address is already taken. Please choose a different one.''),
    (14, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DUPLICATE_REPLICATE_SITE'', CAST(1 AS bit), N''es'', NULL, NULL, N''Esta dirección de sitio web ya está en uso. Por favor elija una diferente.''),
    (15, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_LEVEL_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The selected membership plan is not available.''),
    (16, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_LEVEL_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''El plan de membresía seleccionado no está disponible.''),
    (17, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PRODUCT_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''One or more of the selected products are not available.''),
    (18, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PRODUCT_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''Uno o más de los productos seleccionados no están disponibles.''),
    (19, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MINIMUM_AGE_REQUIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''You must be at least 18 years old to register.''),
    (20, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MINIMUM_AGE_REQUIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Debes tener al menos 18 años para registrarte.''),
    (21, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The 30-day placement window has expired for this member.''),
    (22, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''El período de 30 días para colocar a este miembro ha expirado.''),
    (23, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_LIMIT_EXCEEDED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The maximum number of placement changes for this member has been reached.''),
    (24, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_LIMIT_EXCEEDED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Se alcanzó el límite máximo de cambios de posición para este miembro.''),
    (25, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The 72-hour unplacement window has expired.''),
    (26, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''El período de 72 horas para retirar la posición ha expirado.''),
    (27, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_CHANGE_NOT_ALLOWED'', CAST(1 AS bit), N''en'', NULL, NULL, N''This membership change is not permitted at this time.''),
    (28, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_CHANGE_NOT_ALLOWED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Este cambio de membresía no está permitido en este momento.''),
    (29, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''No active membership was found for this account.''),
    (30, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró una membresía activa para esta cuenta.''),
    (31, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_SPONSOR_BONUS_TYPE'', CAST(1 AS bit), N''en'', NULL, NULL, N''The system could not process the bonus at this time. Please contact support.''),
    (32, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_SPONSOR_BONUS_TYPE'', CAST(1 AS bit), N''es'', NULL, NULL, N''El sistema no pudo procesar el bono en este momento. Contacte soporte.''),
    (33, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_REVERSE_TYPE'', CAST(1 AS bit), N''en'', NULL, NULL, N''The reversal could not be processed. Please contact support.''),
    (34, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_REVERSE_TYPE'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reverso. Por favor contacte soporte.''),
    (35, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REVERSE_TYPE_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The reversal could not be processed. Please contact support.''),
    (36, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REVERSE_TYPE_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reverso. Por favor contacte soporte.''),
    (37, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''COMMISSION_PERIOD_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''Commission data for the requested period could not be found.''),
    (38, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''COMMISSION_PERIOD_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontraron datos de comisión para el período solicitado.''),
    (39, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INSUFFICIENT_TOKEN_BALANCE'', CAST(1 AS bit), N''en'', NULL, NULL, N''You do not have enough tokens to complete this action.''),
    (40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INSUFFICIENT_TOKEN_BALANCE'', CAST(1 AS bit), N''es'', NULL, NULL, N''No tienes suficientes tokens para completar esta acción.''),
    (41, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''No payment method was found for this account.''),
    (42, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró un método de pago para esta cuenta.'');
    INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (43, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_PASSWORD_NOT_ENCRYPTED'', CAST(1 AS bit), N''en'', NULL, NULL, N''A security error occurred. Please contact support immediately.''),
    (44, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_PASSWORD_NOT_ENCRYPTED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ocurrió un error de seguridad. Contacte soporte inmediatamente.''),
    (45, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''RANK_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The requested rank information could not be found.''),
    (46, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''RANK_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la información del rango solicitado.''),
    (47, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PAYMENT_FAILED'', CAST(1 AS bit), N''en'', NULL, NULL, N''Your payment could not be processed. Please verify your payment details.''),
    (48, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PAYMENT_FAILED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar tu pago. Por favor verifica tus datos de pago.''),
    (49, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REFUND_FAILED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The refund could not be processed at this time. Please contact support.''),
    (50, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REFUND_FAILED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reembolso en este momento. Contacte soporte.''),
    (51, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNAUTHORIZED'', CAST(1 AS bit), N''en'', NULL, NULL, N''You are not authorized to perform this action.''),
    (52, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNAUTHORIZED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No tienes autorización para realizar esta acción.''),
    (53, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''VALIDATION_ERROR'', CAST(1 AS bit), N''en'', NULL, NULL, N''The information you provided is invalid. Please review and try again.''),
    (54, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''VALIDATION_ERROR'', CAST(1 AS bit), N''es'', NULL, NULL, N''La información proporcionada no es válida. Por favor revísela e intente de nuevo.'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'ErrorCode', N'IsActive', N'Language', N'LastUpdateBy', N'LastUpdateDate', N'UserFriendlyMessage') AND [object_id] = OBJECT_ID(N'[ErrorMessages]'))
        SET IDENTITY_INSERT [ErrorMessages] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsAutoRenew', N'IsFree', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'Price', N'RenewalPrice', N'SortOrder') AND [object_id] = OBJECT_ID(N'[MembershipLevels]'))
        SET IDENTITY_INSERT [MembershipLevels] ON;
    EXEC(N'INSERT INTO [MembershipLevels] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsAutoRenew], [IsFree], [LastUpdateBy], [LastUpdateDate], [Name], [Price], [RenewalPrice], [SortOrder])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Customer account. No team-building access. No commission eligibility.'', CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, N''External Member'', 0.0, 0.0, 1),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Entry-level ambassador. Qualifies for Sponsor Bonus and Fast Start Bonus.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Ambassador – Basic'', 99.0, 79.0, 2),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Mid-tier ambassador. Qualifies for Daily Residual and Boost Bonus.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Ambassador – Advanced'', 199.0, 169.0, 3),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Top-tier ambassador. Qualifies for all bonuses including Presidential and Matching.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Ambassador – Premium'', 399.0, 349.0, 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsAutoRenew', N'IsFree', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'Price', N'RenewalPrice', N'SortOrder') AND [object_id] = OBJECT_ID(N'[MembershipLevels]'))
        SET IDENTITY_INSERT [MembershipLevels] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] ON;
    EXEC(N'INSERT INTO [RankDefinitions] ([Id], [CertificateTemplateUrl], [CreatedBy], [CreationDate], [Description], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder], [Status])
    VALUES (1, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Member'', 1, 1),
    (2, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Bronze'', 2, 1),
    (3, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Silver'', 3, 1),
    (4, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Gold'', 4, 1),
    (5, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Platinum'', 5, 1),
    (6, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Diamond'', 6, 1),
    (7, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, NULL, N''Presidential'', 7, 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [IsActive], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (1, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''One-time bonus to the direct sponsor when a new ambassador or member signs up.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Sponsor Bonus'', 0, 14, 10.0, 0, CAST(0 AS bit), 0, 0.0E0, 10, 0, 0, 0),
    (2, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 30, N''FSB for personal signups in days 1–30 after the ambassador''''s own enrollment.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 1'', 0, 7, 50.0, 0, CAST(0 AS bit), 0, 0.0E0, 11, 0, 0, 1),
    (3, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 60, N''FSB for personal signups in days 31–60 after the ambassador''''s own enrollment.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 2'', 0, 7, 30.0, 0, CAST(0 AS bit), 0, 0.0E0, 11, 0, 0, 2),
    (4, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 90, N''FSB for personal signups in days 61–90 after the ambassador''''s own enrollment.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 3'', 0, 7, 20.0, 0, CAST(0 AS bit), 0, 0.0E0, 11, 0, 0, 3),
    (5, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Nightly binary team volume commission. Calculated from MemberStatisticEntity.DualTeamPoints.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Daily Residual – Binary'', 0, 0, 10.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 300, 0),
    (6, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Weekly bonus for ambassadors reaching the Gold threshold.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Boost Bonus – Gold'', 0, 3, 5.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 1000, 0),
    (7, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Weekly bonus for ambassadors reaching the Platinum threshold.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Boost Bonus – Platinum'', 0, 3, 8.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 3000, 0),
    (8, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Monthly bonus calculated on total organizational volume for Presidential-rank ambassadors.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Presidential Bonus'', 0, 7, 3.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (9, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Percentage of direct downline daily residual earnings, paid to the upline ambassador.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Matching Bonus'', 0, 0, 20.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (10, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Negative-amount reversal of the Sponsor Bonus when a signup cancels within 14 days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Sponsor Bonus Reversal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (11, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Negative-amount reversal of any Fast Start Bonus window when a signup cancels.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus Reversal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDownHistories_CountDownId] ON [CommissionCountDownHistories] ([CountDownId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDownHistories_MemberId1] ON [CommissionCountDownHistories] ([MemberId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDowns_MemberId1] ON [CommissionCountDowns] ([MemberId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_BeneficiaryMemberId_Status] ON [CommissionEarnings] ([BeneficiaryMemberId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_CommissionOperationTypeId] ON [CommissionEarnings] ([CommissionOperationTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_CommissionTypeId] ON [CommissionEarnings] ([CommissionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_PeriodDate] ON [CommissionEarnings] ([PeriodDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CommissionEarnings_SourceOrderId_CommissionTypeId] ON [CommissionEarnings] ([SourceOrderId], [CommissionTypeId]) WHERE [SourceOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CommissionTypes_CommissionCategoryId] ON [CommissionTypes] ([CommissionCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_HierarchyPath] ON [DualTeamTree] ([HierarchyPath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_MemberId] ON [DualTeamTree] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ErrorMessages_ErrorCode_Language] ON [ErrorMessages] ([ErrorCode], [Language]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_HierarchyPath] ON [GenealogyTree] ([HierarchyPath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_MemberId_CreationDate] ON [GenealogyTree] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GhostPoints_LegMemberId] ON [GhostPoints] ([LegMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GhostPoints_MemberId] ON [GhostPoints] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoyaltyPoints_MemberProfileId] ON [LoyaltyPoints] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MemberNotifications_MemberProfileId] ON [MemberNotifications] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_ActiveMembershipId] ON [MemberProfiles] ([ActiveMembershipId]) WHERE [ActiveMembershipId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_BinaryNodeId] ON [MemberProfiles] ([BinaryNodeId]) WHERE [BinaryNodeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_EnrollmentNodeId] ON [MemberProfiles] ([EnrollmentNodeId]) WHERE [EnrollmentNodeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberProfiles_MemberId] ON [MemberProfiles] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_ReplicateSiteSlug] ON [MemberProfiles] ([ReplicateSiteSlug]) WHERE [ReplicateSiteSlug] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberProfiles_UserId] ON [MemberProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MemberRankHistories_RankDefinitionId] ON [MemberRankHistories] ([RankDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MembershipLevelBenefits_MembershipLevelId] ON [MembershipLevelBenefits] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_LastOrderId1] ON [MembershipSubscriptions] ([LastOrderId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_MemberId] ON [MembershipSubscriptions] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_MembershipLevelId] ON [MembershipSubscriptions] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_MemberStatusHistories_MemberProfileId] ON [MemberStatusHistories] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_OrdersId] ON [OrderDetails] ([OrdersId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_PaymentHistories_OrdersId] ON [PaymentHistories] ([OrdersId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductCommissionPromos_CorporatePromoId1] ON [ProductCommissionPromos] ([CorporatePromoId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductCommissionPromos_ProductId1] ON [ProductCommissionPromos] ([ProductId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductCommissions_ProductId1] ON [ProductCommissions] ([ProductId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Products_MembershipLevelId] ON [Products] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RankRequirements_RankDefinitionId] ON [RankRequirements] ([RankDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketAttachments_TicketId] ON [TicketAttachments] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TicketComments_TicketId] ON [TicketComments] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CategoryId] ON [Tickets] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TokenBalances_MemberProfileId] ON [TokenBalances] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TokenTransactions_MemberId_CreationDate] ON [TokenTransactions] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_CommissionCountDowns_CountDownId] FOREIGN KEY ([CountDownId]) REFERENCES [CommissionCountDowns] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [CommissionCountDowns] ADD CONSTRAINT [FK_CommissionCountDowns_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [LoyaltyPoints] ADD CONSTRAINT [FK_LoyaltyPoints_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [MemberNotifications] ADD CONSTRAINT [FK_MemberNotifications_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD CONSTRAINT [FK_MemberProfiles_MembershipSubscriptions_ActiveMembershipId] FOREIGN KEY ([ActiveMembershipId]) REFERENCES [MembershipSubscriptions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316201542_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260316201542_InitialCreate', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    ALTER TABLE [CommissionTypes] ADD [FixedAmount] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionCategories] SET [Description] = N''One-time bonuses paid to the direct enroller on new member signup (Member Bonus).'', [Name] = N''Enrollment Bonuses''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionCategories] SET [Description] = N''3-window FSB paid when the enroller qualifies within each countdown window.'', [Name] = N''Fast Start Bonus''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionCategories] SET [Description] = N''Fixed daily earnings based on current binary rank qualification.'', [Name] = N''Dual Team Residuals''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionCategories] SET [Description] = N''Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).'', [Name] = N''Leadership Bonuses''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N'INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Negative-amount entries that reverse previously paid commissions within the chargeback window.'', CAST(1 AS bit), NULL, NULL, N''Reversals'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''One-time $20 bonus to the direct enroller when a VIP member signs up.'', [FixedAmount] = 20.0, [LevelNo] = 2, [Name] = N''Member Bonus – VIP'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ReverseId] = 29
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [DaysAfterJoining] = 0, [Description] = N''One-time $40 bonus to the direct enroller when an Elite member signs up.'', [FixedAmount] = 40.0, [IsSponsorBonus] = CAST(1 AS bit), [LevelNo] = 3, [Name] = N''Member Bonus – Elite'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ReverseId] = 30, [TriggerOrder] = 0
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [DaysAfterJoining] = 0, [Description] = N''One-time $80 bonus to the direct enroller when a Turbo member signs up.'', [FixedAmount] = 80.0, [IsSponsorBonus] = CAST(1 AS bit), [LevelNo] = 4, [Name] = N''Member Bonus – Turbo'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ReverseId] = 31, [TriggerOrder] = 0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [CommissionCategoryId] = 2, [DaysAfterJoining] = 14, [Description] = N''Earn $150 when enrolling within your first 14 days as an ambassador.'', [FixedAmount] = 150.0, [LevelNo] = 1, [Name] = N''Fast Start Bonus – Window 1'', [PaymentDelayDays] = 0, [Percentage] = 0.0, [ReverseId] = 32, [TriggerOrder] = 1
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [DaysAfterJoining] = 7, [Description] = N''Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).'', [FixedAmount] = 150.0, [IsPaidOnSignup] = CAST(1 AS bit), [IsRealTime] = CAST(1 AS bit), [LevelNo] = 1, [Name] = N''Fast Start Bonus – Window 2'', [Percentage] = 0.0, [ResidualBased] = CAST(0 AS bit), [ReverseId] = 32, [TeamPoints] = 0, [TriggerOrder] = 2
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [CommissionCategoryId] = 2, [DaysAfterJoining] = 7, [Description] = N''Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).'', [FixedAmount] = 150.0, [IsPaidOnSignup] = CAST(1 AS bit), [IsRealTime] = CAST(1 AS bit), [LevelNo] = 1, [Name] = N''Fast Start Bonus – Window 3'', [PaymentDelayDays] = 0, [Percentage] = 0.0, [ReverseId] = 32, [TeamPoints] = 0, [TriggerOrder] = 3
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).'', [FixedAmount] = 4.0, [Name] = N''DTR – Silver'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ResidualBased] = CAST(1 AS bit), [TeamPoints] = 18
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).'', [FixedAmount] = 10.0, [Name] = N''DTR – Gold'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ResidualBased] = CAST(1 AS bit), [TeamPoints] = 72
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).'', [FixedAmount] = 15.0, [Name] = N''DTR – Platinum'', [PaymentDelayDays] = 4, [Percentage] = 0.0, [ResidualBased] = CAST(1 AS bit), [TeamPoints] = 175
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [CommissionCategoryId] = 3, [Description] = N''Earn $25/day when qualifying at Titanium rank (350 Dual Team points).'', [FixedAmount] = 25.0, [IsRealTime] = CAST(0 AS bit), [Name] = N''DTR – Titanium'', [PaymentDelayDays] = 4, [ResidualBased] = CAST(1 AS bit), [TeamPoints] = 350
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [CommissionCategoryId] = 3, [Description] = N''Earn $40/day when qualifying at Jade rank (700 Dual Team points).'', [FixedAmount] = 40.0, [IsRealTime] = CAST(0 AS bit), [Name] = N''DTR – Jade'', [PaymentDelayDays] = 4, [ResidualBased] = CAST(1 AS bit), [TeamPoints] = 700
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (12, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).'', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Pearl'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 1500, 0),
    (13, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).'', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Emerald'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 3000, 0),
    (14, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).'', 0, 0, 300.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Ruby'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 6000, 0),
    (15, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).'', 0, 0, 500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Sapphire'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 10000, 0),
    (16, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).'', 0, 0, 750.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 15000, 0),
    (17, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).'', 0, 0, 1000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Double Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 20000, 0),
    (18, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).'', 0, 0, 1500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Triple Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 30000, 0),
    (19, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).'', 0, 0, 2000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Blue Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 60000, 0),
    (20, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).'', 0, 0, 3000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Black Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 120000, 0),
    (21, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $4,000/day when qualifying at Royal rank (200,000 DT points).'', 0, 0, 4000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 200000, 0),
    (22, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $5,000/day when qualifying at Double Royal (300,000 DT points).'', 0, 0, 5000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Double Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 300000, 0),
    (23, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).'', 0, 0, 7500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Triple Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 400000, 0),
    (24, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).'', 0, 0, 10000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Blue Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 500000, 0),
    (25, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $15,000/day when qualifying at Black Royal (700,000 DT points).'', 0, 0, 15000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Black Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 700000, 0),
    (26, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.'', 0, 0, 600.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 4, 0.5E0, 0.5E0, 0, N''Boost Bonus – Gold'', 6, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 1),
    (27, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.'', 0, 0, 1200.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 5, 0.5E0, 0.5E0, 0, N''Boost Bonus – Platinum'', 12, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 2),
    (28, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 6, 0.5E0, 0.5E0, 0, N''Presidential Bonus'', 0, 15, 20.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [MembershipLevels] SET [Description] = N''Annual business fee for team-building ambassadors. Qualifies for all commissions.'', [IsAutoRenew] = CAST(1 AS bit), [IsFree] = CAST(0 AS bit), [Name] = N''Lifestyle Ambassador'', [Price] = 99.0, [RenewalPrice] = 99.0
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [MembershipLevels] SET [Description] = N''Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.'', [Name] = N''Travel Advantage – VIP'', [Price] = 40.0, [RenewalPrice] = 40.0
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [MembershipLevels] SET [Description] = N''Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.'', [Name] = N''Travel Advantage – Elite'', [Price] = 99.0, [RenewalPrice] = 99.0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [MembershipLevels] SET [Description] = N''Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.'', [Name] = N''Travel Advantage – Turbo'', [Price] = 199.0, [RenewalPrice] = 199.0
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''18 ET points (3 Elite/Turbo members). DTR: $4/day.'', [Name] = N''Silver''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''72 ET points (12 Elite/Turbo members). DTR: $10/day.'', [Name] = N''Gold''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''175 ET points. DTR: $15/day. Boost Bonus unlocked.'', [Name] = N''Platinum''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''350 DT / 175 ET points. DTR: $25/day.'', [Name] = N''Titanium''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.'', [Name] = N''Jade''
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''1,500 DT / 750 ET points. DTR: $80/day.'', [Name] = N''Pearl''
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    EXEC(N'UPDATE [RankDefinitions] SET [Description] = N''3,000 DT / 1,500 ET points. DTR: $150/day.'', [Name] = N''Emerald''
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] ON;
    EXEC(N'INSERT INTO [RankDefinitions] ([Id], [CertificateTemplateUrl], [CreatedBy], [CreationDate], [Description], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder], [Status])
    VALUES (8, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''6,000 DT / 3,000 ET points. DTR: $300/day.'', NULL, NULL, N''Ruby'', 8, 1),
    (9, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''10,000 DT / 5,000 ET points. DTR: $500/day.'', NULL, NULL, N''Sapphire'', 9, 1),
    (10, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''15,000 DT / 7,500 ET points. DTR: $750/day.'', NULL, NULL, N''Diamond'', 10, 1),
    (11, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''20,000 DT / 10,000 ET points. DTR: $1,000/day.'', NULL, NULL, N''Double Diamond'', 11, 1),
    (12, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''30,000 DT / 15,000 ET points. DTR: $1,500/day.'', NULL, NULL, N''Triple Diamond'', 12, 1),
    (13, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''60,000 DT / 30,000 ET points. DTR: $2,000/day.'', NULL, NULL, N''Blue Diamond'', 13, 1),
    (14, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''120,000 DT / 60,000 ET points. DTR: $3,000/day.'', NULL, NULL, N''Black Diamond'', 14, 1),
    (15, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''200,000 DT / 100,000 ET points. DTR: $4,000/day.'', NULL, NULL, N''Royal'', 15, 1),
    (16, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''300,000 DT / 150,000 ET points. DTR: $5,000/day.'', NULL, NULL, N''Double Royal'', 16, 1),
    (17, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''400,000 DT / 200,000 ET points. DTR: $7,500/day.'', NULL, NULL, N''Triple Royal'', 17, 1),
    (18, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''500,000 DT / 250,000 ET points. DTR: $10,000/day.'', NULL, NULL, N''Blue Royal'', 18, 1),
    (19, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''700,000 DT / 350,000 ET points. DTR: $15,000/day.'', NULL, NULL, N''Black Royal'', 19, 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (29, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a VIP Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (30, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses an Elite Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (31, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Turbo Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (32, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses any FSB window earning when a signup cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Fast Start Bonus'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316204432_UpdateCompPlanSeeds'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260316204432_UpdateCompPlanSeeds', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    ALTER TABLE [CommissionTypes] ADD [IsEnrollmentBased] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(1 AS bit)
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(1 AS bit)
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(1 AS bit)
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 26;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 27;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 28;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 29;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 30;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 31;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [IsEnrollmentBased] = CAST(0 AS bit)
    WHERE [Id] = 32;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260316205302_AddIsEnrollmentBasedToCommissionType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260316205302_AddIsEnrollmentBasedToCommissionType', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260317171759_AddMemberFcmToken'
)
BEGIN
    CREATE TABLE [MemberFcmTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [DeviceId] nvarchar(max) NOT NULL,
        [Platform] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastUsedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberFcmTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260317171759_AddMemberFcmToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260317171759_AddMemberFcmToken', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [MemberProfileId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [RefreshToken] nvarchar(max) NULL,
        [RefreshTokenExpiry] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320004824_AddIdentityAndAuth'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320004824_AddIdentityAndAuth', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320132346_AddBuilderBonusAndDeductions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N'INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.'', CAST(1 AS bit), NULL, NULL, N''Builder Bonus''),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.'', CAST(1 AS bit), NULL, NULL, N''Builder Bonus Turbo''),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Administrative fee deductions and token-related deductions applied at payout or on token consumption.'', CAST(1 AS bit), NULL, NULL, N''Deductions'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320132346_AddBuilderBonusAndDeductions'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (41, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (42, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (43, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (44, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (45, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (46, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (33, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.'', 0, 0, 25.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – VIP'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 41, 0, 0, 0),
    (34, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.'', 0, 0, 60.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – Elite'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 42, 0, 0, 0),
    (35, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.'', 0, 0, 120.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – Turbo'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 43, 0, 0, 0),
    (36, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.'', 0, 0, 30.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – VIP'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 44, 0, 0, 0),
    (37, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.'', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Elite'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 45, 0, 0, 0),
    (38, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.'', 0, 0, 160.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Turbo'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 46, 0, 0, 0),
    (39, 8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Admin Fee'', 0, 0, 5.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (40, 8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.'', 0, 0, 1.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Token Deduction'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320132346_AddBuilderBonusAndDeductions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320132346_AddBuilderBonusAndDeductions', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTypes]') AND [c].[name] = N'TemplateUrl');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [TokenTypes] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [TokenTypes] ALTER COLUMN [TemplateUrl] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTypes]') AND [c].[name] = N'Name');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TokenTypes] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [TokenTypes] ALTER COLUMN [Name] nvarchar(200) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTypes]') AND [c].[name] = N'Description');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [TokenTypes] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [TokenTypes] ALTER COLUMN [Description] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    CREATE TABLE [TokenTypeCommissions] (
        [Id] int NOT NULL IDENTITY,
        [TokenTypeId] int NOT NULL,
        [CommissionTypeId] int NOT NULL,
        [CommissionPerToken] decimal(12,2) NOT NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypeCommissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    CREATE TABLE [TokenTypeProducts] (
        [Id] int NOT NULL IDENTITY,
        [TokenTypeId] int NOT NULL,
        [ProductId] nvarchar(36) NOT NULL,
        [QuantityGranted] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypeProducts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AchievementMessage', N'CertificateUrl', N'CreatedBy', N'CreationDate', N'CurrentRankDescription', N'DailyBonus', N'EnrollmentQualifiedTeamMembers', N'EnrollmentTeam', N'ExternalMembers', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifetimeHoldingDuration', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MonthlyBonus', N'PersonalPoints', N'PlacementQualifiedTeamMembers', N'RankBonus', N'RankDefinitionId', N'RankDescription', N'SalesVolume', N'SponsoredMembers', N'TeamPoints') AND [object_id] = OBJECT_ID(N'[RankRequirements]'))
        SET IDENTITY_INSERT [RankRequirements] ON;
    EXEC(N'INSERT INTO [RankRequirements] ([Id], [AchievementMessage], [CertificateUrl], [CreatedBy], [CreationDate], [CurrentRankDescription], [DailyBonus], [EnrollmentQualifiedTeamMembers], [EnrollmentTeam], [ExternalMembers], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifetimeHoldingDuration], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MonthlyBonus], [PersonalPoints], [PlacementQualifiedTeamMembers], [RankBonus], [RankDefinitionId], [RankDescription], [SalesVolume], [SponsoredMembers], [TeamPoints])
    VALUES (1, N''Congratulations! You have reached Silver rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.'', 4.0, 0, 3, 1, NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 100.0, 1, N''Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).'', 0.0, 1, 18),
    (2, N''Congratulations! You have reached Gold rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.'', 10.0, 0, 12, 1, NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 300.0, 2, N''Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).'', 0.0, 1, 72),
    (3, N''Congratulations! You have reached Platinum rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.'', 15.0, 0, 29, 1, NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 500.0, 3, N''Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.'', 0.0, 2, 175),
    (4, N''Congratulations! You have reached Titanium rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.'', 25.0, 0, 0, 1, NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 1000.0, 4, N''Qualify with 350 Dual Team points (max 50% per branch).'', 0.0, 2, 350),
    (5, N''Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.'', 40.0, 0, 0, 1, NULL, NULL, 5, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 2500.0, 5, N''Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked.'', 0.0, 3, 700),
    (6, N''Congratulations! You have reached Pearl rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.'', 80.0, 0, 0, 1, NULL, NULL, 6, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 5000.0, 6, N''Qualify with 1,500 Dual Team points (max 50% per branch).'', 0.0, 3, 1500),
    (7, N''Congratulations! You have reached Emerald rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.'', 150.0, 0, 0, 1, NULL, NULL, 7, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 10000.0, 7, N''Qualify with 3,000 Dual Team points (max 50% per branch).'', 0.0, 4, 3000),
    (8, N''Congratulations! You have reached Ruby rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.'', 300.0, 0, 0, 1, NULL, NULL, 8, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 25000.0, 8, N''Qualify with 6,000 Dual Team points (max 50% per branch).'', 0.0, 5, 6000),
    (9, N''Congratulations! You have reached Sapphire rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.'', 500.0, 0, 0, 1, NULL, NULL, 9, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 50000.0, 9, N''Qualify with 10,000 Dual Team points (max 50% per branch).'', 0.0, 5, 10000),
    (10, N''Congratulations! You have reached Diamond rank and unlocked the Car Bonus!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.'', 750.0, 0, 0, 1, NULL, NULL, 10, 0, 0.5E0, 0.5E0, 500.0, 1, 0, 100000.0, 10, N''Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked.'', 0.0, 6, 15000),
    (11, N''Congratulations! You have reached Double Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.'', 1000.0, 0, 0, 1, NULL, NULL, 11, 0, 0.5E0, 0.5E0, 750.0, 1, 0, 150000.0, 11, N''Qualify with 20,000 Dual Team points (max 50% per branch).'', 0.0, 6, 20000),
    (12, N''Congratulations! You have reached Triple Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.'', 1500.0, 0, 0, 1, NULL, NULL, 12, 0, 0.5E0, 0.5E0, 1000.0, 1, 0, 200000.0, 12, N''Qualify with 30,000 Dual Team points (max 50% per branch).'', 0.0, 7, 30000),
    (13, N''Congratulations! You have reached Blue Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.'', 2000.0, 0, 0, 1, NULL, NULL, 13, 0, 0.5E0, 0.5E0, 1500.0, 1, 0, 300000.0, 13, N''Qualify with 60,000 Dual Team points (max 50% per branch).'', 0.0, 8, 60000),
    (14, N''Congratulations! You have reached Black Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.'', 3000.0, 0, 0, 1, NULL, NULL, 14, 0, 0.5E0, 0.5E0, 2500.0, 1, 0, 500000.0, 14, N''Qualify with 120,000 Dual Team points (max 50% per branch).'', 0.0, 10, 120000),
    (15, N''Congratulations! You have reached Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.'', 4000.0, 0, 0, 1, NULL, NULL, 15, 0, 0.5E0, 0.5E0, 4000.0, 1, 0, 750000.0, 15, N''Qualify with 200,000 Dual Team points (max 50% per branch).'', 0.0, 12, 200000),
    (16, N''Congratulations! You have reached Double Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.'', 5000.0, 0, 0, 1, NULL, NULL, 16, 0, 0.5E0, 0.5E0, 5000.0, 1, 0, 1000000.0, 16, N''Qualify with 300,000 Dual Team points (max 50% per branch).'', 0.0, 15, 300000),
    (17, N''Congratulations! You have reached Triple Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.'', 7500.0, 0, 0, 1, NULL, NULL, 17, 0, 0.5E0, 0.5E0, 7500.0, 1, 0, 1500000.0, 17, N''Qualify with 400,000 Dual Team points (max 50% per branch).'', 0.0, 20, 400000),
    (18, N''Congratulations! You have reached Blue Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.'', 10000.0, 0, 0, 1, NULL, NULL, 18, 0, 0.5E0, 0.5E0, 10000.0, 1, 0, 2000000.0, 18, N''Qualify with 500,000 Dual Team points (max 50% per branch).'', 0.0, 25, 500000),
    (19, N''Congratulations! You have reached Black Royal — the highest rank in the company!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.'', 15000.0, 0, 0, 1, NULL, NULL, 19, 0, 0.5E0, 0.5E0, 15000.0, 1, 0, 3000000.0, 19, N''Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey.'', 0.0, 30, 700000)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AchievementMessage', N'CertificateUrl', N'CreatedBy', N'CreationDate', N'CurrentRankDescription', N'DailyBonus', N'EnrollmentQualifiedTeamMembers', N'EnrollmentTeam', N'ExternalMembers', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifetimeHoldingDuration', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MonthlyBonus', N'PersonalPoints', N'PlacementQualifiedTeamMembers', N'RankBonus', N'RankDefinitionId', N'RankDescription', N'SalesVolume', N'SponsoredMembers', N'TeamPoints') AND [object_id] = OBJECT_ID(N'[RankRequirements]'))
        SET IDENTITY_INSERT [RankRequirements] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] ON;
    EXEC(N'INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 1, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 2, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(1 AS bit), 0.0, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 3, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (4, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 4, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] ON;
    EXEC(N'INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Guest pass granting access to sign up as a VIP member. Triggers VIP Member Bonus and Builder Bonus commissions for the issuing ambassador.'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, N''VIP Guest Pass'', NULL),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Guest pass granting access to sign up as an Elite member. Triggers Elite Member Bonus and Builder Bonus commissions for the issuing ambassador.'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, N''Elite Guest Pass'', NULL),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Guest pass granting access to sign up as a Turbo member. Triggers Turbo Member Bonus and Builder Bonus commissions for the issuing ambassador.'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, N''Turbo Guest Pass'', NULL),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Internal credit token used to offset membership fees or purchase benefits. Does not trigger enrollment commissions.'', CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Ambassador Credit'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenTypeCommissions_TokenTypeId_CommissionTypeId] ON [TokenTypeCommissions] ([TokenTypeId], [CommissionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenTypeProducts_TokenTypeId_ProductId] ON [TokenTypeProducts] ([TokenTypeId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320134440_AddRankRequirementsAndTokenTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320134440_AddRankRequirementsAndTokenTypes', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypeCommissions] SET [CommissionTypeId] = 1
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypeCommissions] SET [CarBonusEligible] = CAST(0 AS bit), [CommissionTypeId] = 40, [PresidentialBonusEligible] = CAST(0 AS bit), [TriggerBoostBonus] = CAST(0 AS bit), [TriggerBuilderBonus] = CAST(0 AS bit), [TriggerBuilderBonusTurbo] = CAST(0 AS bit), [TriggerFastStartBonus] = CAST(0 AS bit), [TriggerSponsorBonus] = CAST(0 AS bit), [TriggerSponsorBonusTurbo] = CAST(0 AS bit)
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypeCommissions] SET [EligibleDailyResidual] = CAST(1 AS bit), [EligibleMembershipResidual] = CAST(1 AS bit)
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] ON;
    EXEC(N'INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (5, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 5, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (6, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 6, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (7, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 7, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (8, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 8, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (9, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 9, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (10, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 10, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (11, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 11, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (12, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 12, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (13, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 13, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (14, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 14, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (15, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 15, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (16, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 16, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (17, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 17, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (19, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 19, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (20, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 20, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (21, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 21, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (22, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 22, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (23, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 23, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (24, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 24, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (25, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 25, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (26, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 26, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (27, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 27, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (28, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 28, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (29, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 29, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (30, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 30, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (31, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 31, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (32, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 32, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (33, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 33, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (34, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 34, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (35, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 35, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (36, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 36, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (37, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 37, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (38, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 38, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (39, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 39, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (40, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 40, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (41, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 41, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (42, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 42, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (43, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 43, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (44, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 44, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (45, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 45, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (46, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 46, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (47, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 47, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (48, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 48, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (49, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 49, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (50, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 50, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (51, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 51, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (52, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 52, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (53, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 53, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (54, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 54, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (55, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 55, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (56, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 56, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (57, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 57, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (58, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 58, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (59, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 59, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (60, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 60, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (61, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 61, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (62, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 62, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (63, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 63, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (64, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 64, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (65, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 65, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (66, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 66, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (67, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 67, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (68, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 68, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (69, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 69, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (70, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 70, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (71, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 71, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (72, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 72, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (73, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 73, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (74, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 74, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (75, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 75, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (76, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 76, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (77, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 77, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (78, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 78, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (79, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 79, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (80, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 80, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (81, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 81, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (82, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 82, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (83, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 83, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (84, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 84, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (85, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 85, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (86, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 86, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (87, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 87, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (88, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 88, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (89, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 89, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (90, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 90, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (91, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 91, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (92, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 92, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (93, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 93, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (94, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 94, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (95, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 95, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (96, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 96, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (97, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 97, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (98, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 98, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (99, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 99, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Description] = NULL, [IsGuestPass] = CAST(0 AS bit), [Name] = N''Enrollment: Ambassador + Pro''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Description] = NULL, [Name] = N''Guest Member''
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Description] = NULL, [IsGuestPass] = CAST(0 AS bit), [Name] = N''Monthly: Elite''
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Description] = NULL, [Name] = N''Monthly: VIP''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] ON;
    EXEC(N'INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite'', NULL),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage Elite (Signup)'', NULL),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage Lite'', NULL),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador'', NULL),
    (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment Pro ($99.97 cost / no commission)'', NULL),
    (10, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual Fee'', NULL),
    (11, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event'', NULL),
    (12, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro + Event'', NULL),
    (13, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP Member'', NULL),
    (14, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Mobile App'', NULL),
    (15, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Pro Member'', NULL),
    (16, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member'', NULL),
    (17, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Elite'', NULL),
    (19, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Special'', NULL),
    (20, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N''--Available--'', NULL),
    (21, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual: VIP 365'', NULL),
    (22, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro'', NULL),
    (23, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual: Biz Center'', NULL),
    (24, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Legacy Biz Center Fee'', NULL),
    (25, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Mall'', NULL),
    (26, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite180'', NULL),
    (27, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite180 + Event'', NULL),
    (28, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro180'', NULL),
    (29, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro180 + Event'', NULL),
    (30, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus180'', NULL),
    (31, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus180 + Event'', NULL),
    (32, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite180 Member'', NULL),
    (33, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Pro180 Member'', NULL),
    (34, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus180 Member'', NULL),
    (35, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Pro'', NULL),
    (36, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Pro180'', NULL),
    (37, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Elite'', NULL),
    (38, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Elite180'', NULL),
    (39, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Pro180'', NULL),
    (40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Elite180'', NULL),
    (41, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro180 to Elite'', NULL),
    (42, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro180 to Elite180'', NULL),
    (43, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Elite to Elite180'', NULL),
    (44, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 Level 2 (79.97)'', NULL),
    (45, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 Level 3 (39.97)'', NULL),
    (46, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro180 Level 2'', NULL),
    (47, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro180 Level 3'', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (48, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Plus180'', NULL),
    (49, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus'', NULL),
    (50, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus + Event'', NULL),
    (51, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Plus'', NULL),
    (52, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus to Elite'', NULL),
    (53, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus to Elite180'', NULL),
    (54, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus Member'', NULL),
    (55, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 (59.97)'', NULL),
    (56, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP'', NULL),
    (57, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP 365'', NULL),
    (58, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Plus'', NULL),
    (59, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Elite'', NULL),
    (60, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Elite180'', NULL),
    (61, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Plus'', NULL),
    (62, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Elite'', NULL),
    (63, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Elite180'', NULL),
    (64, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP'', NULL),
    (65, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP + Event'', NULL),
    (66, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Elite to Turbo'', NULL),
    (67, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 365'', NULL),
    (68, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 365 + Event'', NULL),
    (69, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO'', NULL),
    (70, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event + TURBO'', NULL),
    (71, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Coupon)'', NULL),
    (72, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event (Coupon)'', NULL),
    (73, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event + TURBO (Coupon)'', NULL),
    (74, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Coupon)'', NULL),
    (75, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180'', NULL),
    (76, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180 + Event'', NULL),
    (77, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP 180'', NULL),
    (78, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Recurring: VIP 180'', NULL),
    (79, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP 180 Member'', NULL),
    (80, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member + TURBO'', NULL),
    (81, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite FREE'', NULL),
    (82, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Coupon) FREE'', NULL),
    (83, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO FREE'', NULL),
    (84, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Coupon) FREE'', NULL),
    (85, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus FREE'', NULL),
    (86, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP FREE'', NULL),
    (87, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180 FREE'', NULL),
    (88, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador FREE'', NULL),
    (89, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member FREE'', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (90, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member + TURBO FREE'', NULL),
    (91, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus Member FREE'', NULL),
    (92, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP Member FREE'', NULL),
    (93, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP 180 FREE'', NULL),
    (94, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Elite Ambassador SpecialPromo'', NULL),
    (95, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Plus Ambassador SpecialPromo'', NULL),
    (96, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Turbo Ambassador SpecialPromo'', NULL),
    (97, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus (Help a Friend)'', NULL),
    (98, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Help a Friend)'', NULL),
    (99, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Help a Friend)'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320160535_AddFullTokenTypeCatalog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320160535_AddFullTokenTypeCatalog', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    ALTER TABLE [ProductCommissionPromos] DROP CONSTRAINT [FK_ProductCommissionPromos_Products_ProductId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    ALTER TABLE [ProductCommissions] DROP CONSTRAINT [FK_ProductCommissions_Products_ProductId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DROP INDEX [IX_ProductCommissions_ProductId1] ON [ProductCommissions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DROP INDEX [IX_ProductCommissionPromos_ProductId1] ON [ProductCommissionPromos];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCommissions]') AND [c].[name] = N'ProductId1');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ProductCommissions] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [ProductCommissions] DROP COLUMN [ProductId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCommissionPromos]') AND [c].[name] = N'ProductId1');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ProductCommissionPromos] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [ProductCommissionPromos] DROP COLUMN [ProductId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductLoyaltySettings]') AND [c].[name] = N'ProductId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ProductLoyaltySettings] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [ProductLoyaltySettings] ALTER COLUMN [ProductId] nvarchar(36) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductLoyaltySettings]') AND [c].[name] = N'PointsPerUnit');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ProductLoyaltySettings] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [ProductLoyaltySettings] ALTER COLUMN [PointsPerUnit] decimal(10,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCommissions]') AND [c].[name] = N'ProductId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [ProductCommissions] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [ProductCommissions] ALTER COLUMN [ProductId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProductCommissionPromos]') AND [c].[name] = N'ProductId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [ProductCommissionPromos] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [ProductCommissionPromos] ALTER COLUMN [ProductId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'PointsPerUnit', N'ProductId', N'RequiredSuccessfulPayments') AND [object_id] = OBJECT_ID(N'[ProductLoyaltySettings]'))
        SET IDENTITY_INSERT [ProductLoyaltySettings] ON;
    EXEC(N'INSERT INTO [ProductLoyaltySettings] ([Id], [CreatedBy], [CreationDate], [IsActive], [LastUpdateBy], [LastUpdateDate], [PointsPerUnit], [ProductId], [RequiredSuccessfulPayments])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 3.0, N''00000002-prod-0000-0000-000000000002'', 1),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 6.0, N''00000003-prod-0000-0000-000000000003'', 1),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 6.0, N''00000004-prod-0000-0000-000000000004'', 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'PointsPerUnit', N'ProductId', N'RequiredSuccessfulPayments') AND [object_id] = OBJECT_ID(N'[ProductLoyaltySettings]'))
        SET IDENTITY_INSERT [ProductLoyaltySettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''00000001-prod-0000-0000-000000000001'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', NULL, 0.0, 0.0, N''Travel Advantage Guest Member'', 1, 0.0, 0.0, 0, 0, 0.0, 0.0),
    (N''00000002-prod-0000-0000-000000000002'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 2, 40.0, 0.0, N''Travel Advantage VIP'', 2, 0.0, 0.0, 3, 0, 0.0, 0.0),
    (N''00000003-prod-0000-0000-000000000003'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 3, 99.0, 0.0, N''Travel Advantage Elite'', 3, 0.0, 0.0, 6, 0, 0.0, 0.0),
    (N''00000004-prod-0000-0000-000000000004'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 4, 199.0, 0.0, N''Travel Advantage Turbo'', 4, 0.0, 0.0, 6, 0, 0.0, 0.0),
    (N''00000005-prod-0000-0000-000000000005'', 99.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 1, 0.0, 0.0, N''Subscription'', 5, 0.0, 0.0, 0, 0, 99.0, 0.0),
    (N''00000006-prod-0000-0000-000000000006'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', NULL, 0.0, 0.0, N''Monthly Subscription'', 6, 0.0, 0.0, 0, 0, 0.0, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'ProductId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[ProductCommissions]'))
        SET IDENTITY_INSERT [ProductCommissions] ON;
    EXEC(N'INSERT INTO [ProductCommissions] ([Id], [CarBonusEligible], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [ProductId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000002-prod-0000-0000-000000000002'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000003-prod-0000-0000-000000000003'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000004-prod-0000-0000-000000000004'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'ProductId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[ProductCommissions]'))
        SET IDENTITY_INSERT [ProductCommissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductLoyaltySettings_ProductId] ON [ProductLoyaltySettings] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCommissions_ProductId] ON [ProductCommissions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    CREATE INDEX [IX_ProductCommissionPromos_ProductId] ON [ProductCommissionPromos] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    ALTER TABLE [ProductCommissionPromos] ADD CONSTRAINT [FK_ProductCommissionPromos_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    ALTER TABLE [ProductCommissions] ADD CONSTRAINT [FK_ProductCommissions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320162107_AddProductSeeds'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320162107_AddProductSeeds', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325233343_R1_SignupWizardAndEmailField'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [Email] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260325233343_R1_SignupWizardAndEmailField'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260325233343_R1_SignupWizardAndEmailField', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    ALTER TABLE [Products] ADD [CorporateFee] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    ALTER TABLE [Products] ADD [JoinPageMembership] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [JoinPageMembership] = CAST(1 AS bit)
    WHERE [Id] = N''00000003-prod-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [JoinPageMembership] = CAST(1 AS bit)
    WHERE [Id] = N''00000004-prod-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [CorporateFee] = CAST(1 AS bit)
    WHERE [Id] = N''00000005-prod-0000-0000-000000000005'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326174752_AddProductJoinPageFlags'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326174752_AddProductJoinPageFlags', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326200000_RestoreEliteProduct'
)
BEGIN

                    MERGE INTO [Products] AS target
                    USING (SELECT '00000003-prod-0000-0000-000000000003' AS Id) AS source
                    ON target.[Id] = source.[Id]
                    WHEN MATCHED THEN
                        UPDATE SET
                            [Name]               = 'Travel Advantage Elite',
                            [Description]        = 'Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.',
                            [ImageUrl]           = '',
                            [MonthlyFee]         = 99.00,
                            [SetupFee]           = 0.00,
                            [Price90Days]        = 0.00,
                            [Price180Days]       = 0.00,
                            [AnnualPrice]        = 0.00,
                            [MonthlyFeePromo]    = 0.00,
                            [SetupFeePromo]      = 0.00,
                            [QualificationPoins] = 6,
                            [QualificationPoinsPromo] = 0,
                            [IsActive]           = 1,
                            [IsDeleted]          = 0,
                            [DeletedAt]          = NULL,
                            [DeletedBy]          = NULL,
                            [CorporateFee]       = 0,
                            [JoinPageMembership] = 1,
                            [OldSystemProductId] = 3,
                            [MembershipLevelId]  = 3,
                            [LastUpdateDate]     = '2026-03-16T00:00:00.000',
                            [LastUpdateBy]       = 'migration-restore'
                    WHEN NOT MATCHED THEN
                        INSERT ([Id],[Name],[Description],[ImageUrl],[MonthlyFee],[SetupFee],
                                [Price90Days],[Price180Days],[AnnualPrice],[MonthlyFeePromo],[SetupFeePromo],
                                [QualificationPoins],[QualificationPoinsPromo],
                                [IsActive],[IsDeleted],[DeletedAt],[DeletedBy],
                                [CorporateFee],[JoinPageMembership],[OldSystemProductId],[MembershipLevelId],
                                [CreationDate],[CreatedBy],[LastUpdateDate],[LastUpdateBy])
                        VALUES ('00000003-prod-0000-0000-000000000003',
                                'Travel Advantage Elite',
                                'Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.',
                                '', 99.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00,
                                6, 0, 1, 0, NULL, NULL, 0, 1, 3, 3,
                                '2026-03-16T00:00:00.000', 'migration-restore',
                                '2026-03-16T00:00:00.000', 'migration-restore');
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326200000_RestoreEliteProduct'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326200000_RestoreEliteProduct', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [MemberProfileId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [RefreshToken] nvarchar(max) NULL,
        [RefreshTokenExpiry] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AuditTracking] (
        [Id] bigint NOT NULL IDENTITY,
        [EntityName] nvarchar(max) NOT NULL,
        [EntityId] nvarchar(max) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [ChangedBy] nvarchar(max) NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditTracking] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionOperationType] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionOperationType] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CorporateEvents] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [EventDate] datetime2 NOT NULL,
        [Location] nvarchar(max) NULL,
        [ImageUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CorporateEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CorporatePromos] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [BannerUrl] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CorporatePromos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CreditCards] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [MaskedCardNumber] nvarchar(max) NOT NULL,
        [Last4] nvarchar(max) NOT NULL,
        [First6] nvarchar(max) NOT NULL,
        [CardBrand] nvarchar(max) NOT NULL,
        [ExpiryMonth] int NOT NULL,
        [ExpiryYear] int NOT NULL,
        [Gateway] nvarchar(max) NOT NULL,
        [GatewayToken] nvarchar(max) NOT NULL,
        [CardToken] nvarchar(max) NOT NULL,
        [IsExpired] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CreditCards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [DualTeamTree] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [ParentMemberId] nvarchar(max) NULL,
        [Side] int NOT NULL,
        [HierarchyPath] nvarchar(2000) NOT NULL,
        [LeftLegPoints] decimal(18,4) NOT NULL,
        [RightLegPoints] decimal(18,4) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_DualTeamTree] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [ErrorLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [ApiName] nvarchar(max) NOT NULL,
        [Endpoint] nvarchar(max) NOT NULL,
        [CodeSection] nvarchar(max) NOT NULL,
        [ErrorCode] nvarchar(max) NOT NULL,
        [TechnicalMessage] nvarchar(max) NOT NULL,
        [FullException] nvarchar(max) NULL,
        [InnerException] nvarchar(max) NULL,
        [MemberId] nvarchar(max) NULL,
        [RequestData] nvarchar(max) NULL,
        [TraceId] nvarchar(max) NULL,
        [Language] nvarchar(max) NOT NULL,
        [HttpStatusCode] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [IsResolved] bit NOT NULL,
        [ResolvedBy] nvarchar(max) NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ErrorLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [ErrorMessages] (
        [Id] int NOT NULL IDENTITY,
        [ErrorCode] nvarchar(100) NOT NULL,
        [Language] nvarchar(10) NOT NULL,
        [UserFriendlyMessage] nvarchar(500) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ErrorMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [GenealogyTree] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [ParentMemberId] nvarchar(max) NULL,
        [HierarchyPath] nvarchar(2000) NOT NULL,
        [Level] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_GenealogyTree] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [GhostPoints] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [LegMemberId] nvarchar(450) NOT NULL,
        [Side] int NOT NULL,
        [Points] decimal(18,4) NOT NULL,
        [AdminNote] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_GhostPoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberFcmTokens] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [Token] nvarchar(max) NOT NULL,
        [DeviceId] nvarchar(max) NOT NULL,
        [Platform] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastUsedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberFcmTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberIdentificationTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MemberIdentificationTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MembershipLevels] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Price] decimal(10,2) NOT NULL,
        [RenewalPrice] decimal(10,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsFree] bit NOT NULL,
        [IsAutoRenew] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MembershipLevels] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberStatistics] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [PersonalPoints] int NOT NULL,
        [ExternalCustomerPoints] int NOT NULL,
        [DualTeamSize] int NOT NULL,
        [EnrollmentTeamSize] int NOT NULL,
        [DualTeamPoints] int NOT NULL,
        [EnrollmentPoints] int NOT NULL,
        [QualifiedSponsoredMembers] int NOT NULL,
        [QualifiedSponsoredExternalCustomers] int NOT NULL,
        [EnrollmentTeamGrowth] int NOT NULL,
        [DualteamGrowth] int NOT NULL,
        [EnrollmentTeamPointsGrowth] int NOT NULL,
        [DualTeamPointsGrowth] int NOT NULL,
        [CurrentWeekIncomeGrowth] decimal(18,2) NOT NULL,
        [CurrentMonthIncomeGrowth] decimal(18,2) NOT NULL,
        [CurrentYearIncomeGrowth] decimal(18,2) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberStatistics] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [MembershipSubscriptionId] nvarchar(max) NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] int NOT NULL,
        [OrderDate] datetime2 NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CheckoutScreenshotUrl] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [PlacementLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [PlacedUnderMemberId] nvarchar(max) NOT NULL,
        [Side] int NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [Reason] nvarchar(max) NULL,
        [UnplacementCount] int NOT NULL,
        [FirstPlacementDate] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_PlacementLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [ProductLoyaltySettings] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] nvarchar(36) NOT NULL,
        [PointsPerUnit] decimal(10,2) NOT NULL,
        [RequiredSuccessfulPayments] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductLoyaltySettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [RankDefinitions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [Status] int NOT NULL,
        [CertificateTemplateUrl] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RankDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [SlaPolicies] (
        [Id] nvarchar(36) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [FirstResponseCriticalMinutes] int NOT NULL,
        [FirstResponseHighMinutes] int NOT NULL,
        [FirstResponseNormalMinutes] int NOT NULL,
        [FirstResponseLowMinutes] int NOT NULL,
        [ResolutionCriticalMinutes] int NOT NULL,
        [ResolutionHighMinutes] int NOT NULL,
        [ResolutionNormalMinutes] int NOT NULL,
        [ResolutionLowMinutes] int NOT NULL,
        [Timezone] nvarchar(100) NOT NULL,
        [WorkdaysJson] nvarchar(50) NOT NULL,
        [BusinessHoursStart] nvarchar(10) NOT NULL,
        [BusinessHoursEnd] nvarchar(10) NOT NULL,
        [WarningThresholdPercent] int NOT NULL,
        [NotifyAgentAtMinutes] int NOT NULL,
        [NotifySupervisorAtMinutes] int NOT NULL,
        [NotifyManagerAtMinutes] int NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SlaPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [SupportTeams] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SupervisorAgentId] nvarchar(450) NULL,
        [RoutingMethod] nvarchar(30) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_SupportTeams] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketMetrics] (
        [Id] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [TotalCreated] int NOT NULL,
        [TotalResolved] int NOT NULL,
        [TotalClosed] int NOT NULL,
        [AvgFirstResponseMinutes] float NOT NULL,
        [AvgResolutionMinutes] float NOT NULL,
        [FirstContactResolutionRate] float NOT NULL,
        [SlaComplianceRate] float NOT NULL,
        [CsatAverage] float NOT NULL,
        [CsatResponseCount] int NOT NULL,
        [FrtBreaches] int NOT NULL,
        [ResolutionBreaches] int NOT NULL,
        [DeflectionAttempts] int NOT NULL,
        [DeflectionSuccesses] int NOT NULL,
        [TicketsByPriorityJson] nvarchar(2000) NOT NULL,
        [TicketsByCategoryJson] nvarchar(2000) NOT NULL,
        [TicketsByChannelJson] nvarchar(1000) NOT NULL,
        [TicketsByAgentJson] nvarchar(4000) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TicketMetrics] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketSequences] (
        [Date] date NOT NULL,
        [LastSequence] int NOT NULL,
        CONSTRAINT [PK_TicketSequences] PRIMARY KEY ([Date])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TokenTransactions] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(450) NOT NULL,
        [TokenTypeId] int NOT NULL,
        [TransactionType] int NOT NULL,
        [Quantity] int NOT NULL,
        [DistributedToMemberId] nvarchar(max) NULL,
        [UsedByMemberId] nvarchar(max) NULL,
        [UsedAt] datetime2 NULL,
        [GeneratedPdfUrl] nvarchar(max) NULL,
        [ReferenceId] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TokenTransactions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TokenTypeCommissions] (
        [Id] int NOT NULL IDENTITY,
        [TokenTypeId] int NOT NULL,
        [CommissionTypeId] int NOT NULL,
        [CommissionPerToken] decimal(12,2) NOT NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypeCommissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TokenTypeProducts] (
        [Id] int NOT NULL IDENTITY,
        [TokenTypeId] int NOT NULL,
        [ProductId] nvarchar(36) NOT NULL,
        [QuantityGranted] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypeProducts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TokenTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsGuestPass] bit NOT NULL,
        [TemplateUrl] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TokenTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [WalletHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [WalletId] nvarchar(max) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [WalletType] int NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [ChangeReason] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_WalletHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [Wallets] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [WalletType] int NOT NULL,
        [Status] int NOT NULL,
        [AccountIdentifier] nvarchar(max) NULL,
        [eWalletPasswordEncrypted] nvarchar(max) NULL,
        [IsPreferred] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_Wallets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionTypes] (
        [Id] int NOT NULL IDENTITY,
        [CommissionCategoryId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Percentage] decimal(10,2) NOT NULL,
        [FixedAmount] decimal(12,2) NULL,
        [PaymentDelayDays] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsRealTime] bit NOT NULL,
        [IsPaidOnSignup] bit NOT NULL,
        [IsPaidOnRenewal] bit NOT NULL,
        [Cummulative] bit NOT NULL,
        [TriggerOrder] int NOT NULL,
        [NewMembers] int NOT NULL,
        [DaysAfterJoining] int NOT NULL,
        [MembersRebill] int NOT NULL,
        [LifeTimeRank] int NOT NULL,
        [CurrentRank] int NOT NULL,
        [LevelNo] int NOT NULL,
        [ResidualBased] bit NOT NULL,
        [ResidualOverCommissionType] int NOT NULL,
        [ResidualPercentage] float NOT NULL,
        [PersonalPoints] int NOT NULL,
        [TeamPoints] int NOT NULL,
        [IsEnrollmentBased] bit NOT NULL,
        [MaxTeamPointsPerBranch] float NOT NULL,
        [EnrollmentTeam] int NOT NULL,
        [MaxEnrollmentTeamPointsPerBranch] float NOT NULL,
        [ExternalMembers] int NOT NULL,
        [SponsoredMembers] int NOT NULL,
        [IsSponsorBonus] bit NOT NULL,
        [ReverseId] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommissionTypes_CommissionCategories_CommissionCategoryId] FOREIGN KEY ([CommissionCategoryId]) REFERENCES [CommissionCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MembershipLevelBenefits] (
        [Id] int NOT NULL IDENTITY,
        [MembershipLevelId] int NOT NULL,
        [BenefitName] nvarchar(max) NOT NULL,
        [BenefitDescription] nvarchar(max) NULL,
        [BenefitValue] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_MembershipLevelBenefits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MembershipLevelBenefits_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [ImageUrl] nvarchar(500) NOT NULL,
        [MonthlyFee] decimal(10,2) NOT NULL,
        [SetupFee] decimal(10,2) NOT NULL,
        [Price90Days] decimal(10,2) NOT NULL,
        [Price180Days] decimal(10,2) NOT NULL,
        [AnnualPrice] decimal(10,2) NOT NULL,
        [DescriptionPromo] nvarchar(2000) NULL,
        [MonthlyFeePromo] decimal(10,2) NOT NULL,
        [SetupFeePromo] decimal(10,2) NOT NULL,
        [ImageUrlPromo] nvarchar(500) NULL,
        [QualificationPoinsPromo] int NOT NULL,
        [QualificationPoins] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CorporateFee] bit NOT NULL DEFAULT CAST(0 AS bit),
        [JoinPageMembership] bit NOT NULL DEFAULT CAST(0 AS bit),
        [OldSystemProductId] int NOT NULL,
        [MembershipLevelId] int NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Products_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [OrderDetails] (
        [Id] bigint NOT NULL IDENTITY,
        [OrderId] nvarchar(max) NOT NULL,
        [ProductId] nvarchar(max) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        [OrdersId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderDetails_Orders_OrdersId] FOREIGN KEY ([OrdersId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [PaymentHistories] (
        [Id] nvarchar(450) NOT NULL,
        [OrderId] nvarchar(max) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [GatewayName] nvarchar(max) NOT NULL,
        [GatewayTransactionId] nvarchar(max) NULL,
        [TransactionStatus] int NOT NULL,
        [FailureReason] nvarchar(max) NULL,
        [ProcessedAt] datetime2 NULL,
        [OrdersId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_PaymentHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentHistories_Orders_OrdersId] FOREIGN KEY ([OrdersId]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberRankHistories] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [RankDefinitionId] int NOT NULL,
        [PreviousRankId] int NULL,
        [AchievedAt] datetime2 NOT NULL,
        [GeneratedCertificateUrl] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_MemberRankHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MemberRankHistories_RankDefinitions_RankDefinitionId] FOREIGN KEY ([RankDefinitionId]) REFERENCES [RankDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [RankRequirements] (
        [Id] int NOT NULL IDENTITY,
        [RankDefinitionId] int NOT NULL,
        [LevelNo] int NOT NULL,
        [PersonalPoints] int NOT NULL,
        [TeamPoints] int NOT NULL,
        [MaxTeamPointsPerBranch] float NOT NULL,
        [EnrollmentTeam] int NOT NULL,
        [PlacementQualifiedTeamMembers] int NOT NULL,
        [EnrollmentQualifiedTeamMembers] int NOT NULL,
        [MaxEnrollmentTeamPointsPerBranch] float NOT NULL,
        [ExternalMembers] int NOT NULL,
        [SponsoredMembers] int NOT NULL,
        [SalesVolume] decimal(10,2) NOT NULL,
        [RankBonus] decimal(10,2) NOT NULL,
        [DailyBonus] decimal(10,2) NOT NULL,
        [MonthlyBonus] decimal(10,2) NOT NULL,
        [LifetimeHoldingDuration] int NOT NULL,
        [RankDescription] nvarchar(1000) NOT NULL,
        [CurrentRankDescription] nvarchar(1000) NOT NULL,
        [AchievementMessage] nvarchar(1500) NULL,
        [CertificateUrl] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_RankRequirements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RankRequirements_RankDefinitions_RankDefinitionId] FOREIGN KEY ([RankDefinitionId]) REFERENCES [RankDefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CannedResponses] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [Category] nvarchar(100) NULL,
        [TagsJson] nvarchar(500) NOT NULL,
        [Scope] nvarchar(20) NOT NULL,
        [OwnerAgentId] nvarchar(450) NULL,
        [TeamId] int NULL,
        [UsageCount] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CannedResponses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CannedResponses_SupportTeams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [SupportAgents] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(50) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NOT NULL,
        [TeamId] int NULL,
        [Tier] int NOT NULL,
        [SkillsJson] nvarchar(1000) NOT NULL,
        [LanguagesJson] nvarchar(200) NOT NULL,
        [MaxConcurrentTickets] int NOT NULL,
        [CurrentTicketCount] int NOT NULL,
        [Availability] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SupportAgents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SupportAgents_SupportTeams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketCategories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [ParentCategoryId] int NULL,
        [DefaultTeamId] int NULL,
        [DefaultPriority] nvarchar(20) NOT NULL,
        [DefaultSlaPolicyId] nvarchar(36) NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TicketCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId] FOREIGN KEY ([DefaultSlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TicketCategories_SupportTeams_DefaultTeamId] FOREIGN KEY ([DefaultTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_TicketCategories_TicketCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [TicketCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionEarnings] (
        [Id] nvarchar(450) NOT NULL,
        [BeneficiaryMemberId] nvarchar(450) NOT NULL,
        [SourceMemberId] nvarchar(max) NULL,
        [SourceOrderId] nvarchar(450) NULL,
        [CommissionTypeId] int NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Status] int NOT NULL,
        [EarnedDate] datetime2 NOT NULL,
        [PaymentDate] datetime2 NOT NULL,
        [PeriodDate] datetime2 NULL,
        [CommissionOperationTypeId] int NULL,
        [IsManualEntry] bit NOT NULL,
        [CsvImportBatchId] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CommissionEarnings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommissionEarnings_CommissionOperationType_CommissionOperationTypeId] FOREIGN KEY ([CommissionOperationTypeId]) REFERENCES [CommissionOperationType] ([Id]),
        CONSTRAINT [FK_CommissionEarnings_CommissionTypes_CommissionTypeId] FOREIGN KEY ([CommissionTypeId]) REFERENCES [CommissionTypes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [ProductCommissionPromos] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] nvarchar(450) NOT NULL,
        [CorporatePromoId] bigint NOT NULL,
        [CorporatePromoId1] nvarchar(450) NOT NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCommissionPromos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCommissionPromos_CorporatePromos_CorporatePromoId1] FOREIGN KEY ([CorporatePromoId1]) REFERENCES [CorporatePromos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductCommissionPromos_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [ProductCommissions] (
        [Id] int NOT NULL IDENTITY,
        [ProductId] nvarchar(450) NOT NULL,
        [TriggerSponsorBonus] bit NOT NULL,
        [TriggerBuilderBonus] bit NOT NULL,
        [TriggerSponsorBonusTurbo] bit NOT NULL,
        [TriggerBuilderBonusTurbo] bit NOT NULL,
        [TriggerFastStartBonus] bit NOT NULL,
        [TriggerBoostBonus] bit NOT NULL,
        [CarBonusEligible] bit NOT NULL,
        [PresidentialBonusEligible] bit NOT NULL,
        [EligibleMembershipResidual] bit NOT NULL,
        [EligibleDailyResidual] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductCommissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductCommissions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [KbArticles] (
        [Id] nvarchar(450) NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Slug] nvarchar(300) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [CategoryId] int NOT NULL,
        [TagsJson] nvarchar(1000) NOT NULL,
        [Visibility] int NOT NULL,
        [AuthorAgentId] nvarchar(max) NOT NULL,
        [ViewCount] int NOT NULL,
        [HelpfulCount] int NOT NULL,
        [NotHelpfulCount] int NOT NULL,
        [SourceTicketId] nvarchar(36) NULL,
        [PublishedAt] datetime2 NULL,
        [Version] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_KbArticles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KbArticles_TicketCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] nvarchar(450) NOT NULL,
        [TicketNumber] nvarchar(20) NOT NULL,
        [MemberId] nvarchar(450) NOT NULL,
        [AssignedToUserId] nvarchar(450) NULL,
        [AssignedTeamId] int NULL,
        [CategoryId] int NOT NULL,
        [Subcategory] nvarchar(100) NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [Channel] int NOT NULL,
        [EscalationLevel] int NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [MergedIntoTicketId] nvarchar(36) NULL,
        [Language] nvarchar(10) NULL,
        [CustomerTier] nvarchar(50) NULL,
        [SlaPolicyId] nvarchar(36) NULL,
        [SlaFirstResponseDue] datetime2 NULL,
        [SlaFirstResponseAt] datetime2 NULL,
        [SlaResolutionDue] datetime2 NULL,
        [IsSlaFirstResponseBreached] bit NOT NULL,
        [IsSlaResolutionBreached] bit NOT NULL,
        [IsSlaPaused] bit NOT NULL,
        [SlaPausedAt] datetime2 NULL,
        [SlaPausedMinutes] float NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionSummary] nvarchar(2000) NULL,
        [ResolvedByAgentId] nvarchar(450) NULL,
        [CsatScore] int NULL,
        [CsatComment] nvarchar(1000) NULL,
        [CsatSubmittedAt] datetime2 NULL,
        [FollowUpSent] bit NOT NULL,
        [ClosedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Tickets_SlaPolicies_SlaPolicyId] FOREIGN KEY ([SlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_SupportTeams_AssignedTeamId] FOREIGN KEY ([AssignedTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Tickets_TicketCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [TicketCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [KbArticleVersions] (
        [Id] bigint NOT NULL IDENTITY,
        [ArticleId] nvarchar(450) NOT NULL,
        [VersionNumber] int NOT NULL,
        [BodySnapshot] nvarchar(max) NOT NULL,
        [EditedByAgentId] nvarchar(max) NOT NULL,
        [ChangeNote] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_KbArticleVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KbArticleVersions_KbArticles_ArticleId] FOREIGN KEY ([ArticleId]) REFERENCES [KbArticles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [SlaBreaches] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [SlaPolicyId] nvarchar(max) NOT NULL,
        [MetricType] int NOT NULL,
        [DueAt] datetime2 NOT NULL,
        [BreachedAt] datetime2 NOT NULL,
        [BreachDurationMinutes] int NOT NULL,
        [AssignedAgentId] nvarchar(450) NULL,
        [AssignedTeamId] int NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_SlaBreaches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SlaBreaches_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketAttachments] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [FileUrl] nvarchar(max) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketAttachments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketComments] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [AuthorId] nvarchar(max) NOT NULL,
        [AuthorType] nvarchar(20) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [IsInternal] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketComments_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TicketHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [TicketId] nvarchar(450) NOT NULL,
        [Field] nvarchar(100) NOT NULL,
        [OldValue] nvarchar(500) NULL,
        [NewValue] nvarchar(500) NULL,
        [ChangedByType] nvarchar(20) NOT NULL,
        [ChangedById] nvarchar(450) NULL,
        [ChangeReason] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TicketHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketHistories_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionCountDownHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [CountDownId] nvarchar(450) NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberId1] nvarchar(450) NULL,
        [FastStartBonus1Start] datetime2 NOT NULL,
        [FastStartBonus1End] datetime2 NOT NULL,
        [FastStartBonus2Start] datetime2 NOT NULL,
        [FastStartBonus2End] datetime2 NOT NULL,
        [FastStartBonus3Start] datetime2 NOT NULL,
        [FastStartBonus3End] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_CommissionCountDownHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [CommissionCountDowns] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] uniqueidentifier NOT NULL,
        [MemberId1] nvarchar(450) NULL,
        [FastStartBonus1Start] datetime2 NOT NULL,
        [FastStartBonus1End] datetime2 NOT NULL,
        [FastStartBonus2Start] datetime2 NOT NULL,
        [FastStartBonus2End] datetime2 NOT NULL,
        [FastStartBonus3Start] datetime2 NOT NULL,
        [FastStartBonus3End] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CommissionCountDowns] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [LoyaltyPoints] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [ProductId] nvarchar(max) NOT NULL,
        [OrderId] nvarchar(max) NOT NULL,
        [PointsEarned] decimal(18,2) NOT NULL,
        [IsLocked] bit NOT NULL,
        [MissedPayment] bit NOT NULL,
        [NumberOfSuccessPayments] int NOT NULL,
        [MonthNo] int NOT NULL,
        [YearNo] int NOT NULL,
        [UnlockedAt] datetime2 NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_LoyaltyPoints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberNotifications] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [EventType] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Body] nvarchar(max) NOT NULL,
        [FirebaseMessageId] nvarchar(max) NULL,
        [IsDelivered] bit NOT NULL,
        [DeliveredAt] datetime2 NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberNotifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberProfiles] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [MemberId] nvarchar(20) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [DateOfBirth] datetime2 NOT NULL,
        [Phone] nvarchar(30) NULL,
        [WhatsApp] nvarchar(max) NULL,
        [Country] nvarchar(100) NOT NULL,
        [State] nvarchar(100) NULL,
        [City] nvarchar(100) NULL,
        [Address] nvarchar(500) NULL,
        [ZipCode] nvarchar(max) NULL,
        [BusinessName] nvarchar(max) NULL,
        [ShowBusinessName] bit NOT NULL,
        [MemberType] int NOT NULL,
        [Status] int NOT NULL,
        [EnrollDate] datetime2 NOT NULL,
        [SponsorMemberId] nvarchar(max) NULL,
        [ReplicateSiteSlug] nvarchar(100) NULL,
        [ProfilePhotoUrl] nvarchar(max) NULL,
        [IsNamePublic] bit NOT NULL,
        [IsEmailPublic] bit NOT NULL,
        [IsPhonePublic] bit NOT NULL,
        [ActiveMembershipId] nvarchar(450) NULL,
        [EnrollmentNodeId] nvarchar(450) NULL,
        [BinaryNodeId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_MemberProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [AK_MemberProfiles_MemberId] UNIQUE ([MemberId]),
        CONSTRAINT [FK_MemberProfiles_DualTeamTree_BinaryNodeId] FOREIGN KEY ([BinaryNodeId]) REFERENCES [DualTeamTree] ([Id]),
        CONSTRAINT [FK_MemberProfiles_GenealogyTree_EnrollmentNodeId] FOREIGN KEY ([EnrollmentNodeId]) REFERENCES [GenealogyTree] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MembershipSubscriptions] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(20) NOT NULL,
        [MembershipLevelId] int NOT NULL,
        [PreviousMembershipLevelId] int NULL,
        [ChangeReason] int NOT NULL,
        [SubscriptionStatus] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [RenewalDate] datetime2 NULL,
        [HoldDate] datetime2 NULL,
        [CancellationDate] datetime2 NULL,
        [IsFree] bit NOT NULL,
        [IsAutoRenew] bit NOT NULL,
        [LastOrderId] nvarchar(max) NULL,
        [LastOrderId1] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_MembershipSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [MemberProfiles] ([MemberId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MembershipSubscriptions_Orders_LastOrderId1] FOREIGN KEY ([LastOrderId1]) REFERENCES [Orders] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [MemberStatusHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [OldStatus] int NOT NULL,
        [NewStatus] int NOT NULL,
        [Reason] nvarchar(max) NULL,
        [ChangedAt] datetime2 NOT NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MemberStatusHistories_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE TABLE [TokenBalances] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(max) NOT NULL,
        [TokenTypeId] int NOT NULL,
        [Balance] int NOT NULL,
        [MemberProfileId] nvarchar(450) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_TokenBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TokenBalances_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N'INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''One-time bonuses paid to the direct enroller on new member signup (Member Bonus).'', CAST(1 AS bit), NULL, NULL, N''Enrollment Bonuses''),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''3-window FSB paid when the enroller qualifies within each countdown window.'', CAST(1 AS bit), NULL, NULL, N''Fast Start Bonus''),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Fixed daily earnings based on current binary rank qualification.'', CAST(1 AS bit), NULL, NULL, N''Dual Team Residuals''),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).'', CAST(1 AS bit), NULL, NULL, N''Leadership Bonuses''),
    (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Negative-amount entries that reverse previously paid commissions within the chargeback window.'', CAST(1 AS bit), NULL, NULL, N''Reversals''),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.'', CAST(1 AS bit), NULL, NULL, N''Builder Bonus''),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.'', CAST(1 AS bit), NULL, NULL, N''Builder Bonus Turbo''),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Administrative fee deductions and token-related deductions applied at payout or on token consumption.'', CAST(1 AS bit), NULL, NULL, N''Deductions'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'ErrorCode', N'IsActive', N'Language', N'LastUpdateBy', N'LastUpdateDate', N'UserFriendlyMessage') AND [object_id] = OBJECT_ID(N'[ErrorMessages]'))
        SET IDENTITY_INSERT [ErrorMessages] ON;
    EXEC(N'INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DEFAULT'', CAST(1 AS bit), N''en'', NULL, NULL, N''Something went wrong. Please try again or contact support.''),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DEFAULT'', CAST(1 AS bit), N''es'', NULL, NULL, N''Algo salió mal. Intente de nuevo o comuníquese con soporte.''),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INTERNAL_ERROR'', CAST(1 AS bit), N''en'', NULL, NULL, N''An unexpected error occurred. Please try again later.''),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INTERNAL_ERROR'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ocurrió un error inesperado. Por favor intente más tarde.''),
    (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''ORDER_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The requested order could not be found.''),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''ORDER_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la orden solicitada.''),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The member account could not be found.''),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la cuenta del miembro.''),
    (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_ALREADY_EXISTS'', CAST(1 AS bit), N''en'', NULL, NULL, N''An account with this information already exists.''),
    (10, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBER_ALREADY_EXISTS'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ya existe una cuenta con esta información.''),
    (11, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''SPONSOR_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The sponsor ID you entered could not be found. Please verify and try again.''),
    (12, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''SPONSOR_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''El ID de patrocinador ingresado no fue encontrado. Verifíquelo e intente de nuevo.''),
    (13, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DUPLICATE_REPLICATE_SITE'', CAST(1 AS bit), N''en'', NULL, NULL, N''This website address is already taken. Please choose a different one.''),
    (14, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''DUPLICATE_REPLICATE_SITE'', CAST(1 AS bit), N''es'', NULL, NULL, N''Esta dirección de sitio web ya está en uso. Por favor elija una diferente.''),
    (15, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_LEVEL_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The selected membership plan is not available.''),
    (16, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_LEVEL_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''El plan de membresía seleccionado no está disponible.''),
    (17, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PRODUCT_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''One or more of the selected products are not available.''),
    (18, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PRODUCT_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''Uno o más de los productos seleccionados no están disponibles.''),
    (19, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MINIMUM_AGE_REQUIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''You must be at least 18 years old to register.''),
    (20, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MINIMUM_AGE_REQUIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Debes tener al menos 18 años para registrarte.''),
    (21, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The 30-day placement window has expired for this member.''),
    (22, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''El período de 30 días para colocar a este miembro ha expirado.''),
    (23, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_LIMIT_EXCEEDED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The maximum number of placement changes for this member has been reached.''),
    (24, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_LIMIT_EXCEEDED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Se alcanzó el límite máximo de cambios de posición para este miembro.''),
    (25, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The 72-hour unplacement window has expired.''),
    (26, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNPLACEMENT_WINDOW_EXPIRED'', CAST(1 AS bit), N''es'', NULL, NULL, N''El período de 72 horas para retirar la posición ha expirado.''),
    (27, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_CHANGE_NOT_ALLOWED'', CAST(1 AS bit), N''en'', NULL, NULL, N''This membership change is not permitted at this time.''),
    (28, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_CHANGE_NOT_ALLOWED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Este cambio de membresía no está permitido en este momento.''),
    (29, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''No active membership was found for this account.''),
    (30, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''MEMBERSHIP_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró una membresía activa para esta cuenta.''),
    (31, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_SPONSOR_BONUS_TYPE'', CAST(1 AS bit), N''en'', NULL, NULL, N''The system could not process the bonus at this time. Please contact support.''),
    (32, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_SPONSOR_BONUS_TYPE'', CAST(1 AS bit), N''es'', NULL, NULL, N''El sistema no pudo procesar el bono en este momento. Contacte soporte.''),
    (33, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_REVERSE_TYPE'', CAST(1 AS bit), N''en'', NULL, NULL, N''The reversal could not be processed. Please contact support.''),
    (34, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''NO_REVERSE_TYPE'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reverso. Por favor contacte soporte.''),
    (35, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REVERSE_TYPE_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The reversal could not be processed. Please contact support.''),
    (36, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REVERSE_TYPE_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reverso. Por favor contacte soporte.''),
    (37, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''COMMISSION_PERIOD_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''Commission data for the requested period could not be found.''),
    (38, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''COMMISSION_PERIOD_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontraron datos de comisión para el período solicitado.''),
    (39, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INSUFFICIENT_TOKEN_BALANCE'', CAST(1 AS bit), N''en'', NULL, NULL, N''You do not have enough tokens to complete this action.''),
    (40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''INSUFFICIENT_TOKEN_BALANCE'', CAST(1 AS bit), N''es'', NULL, NULL, N''No tienes suficientes tokens para completar esta acción.''),
    (41, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''No payment method was found for this account.''),
    (42, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró un método de pago para esta cuenta.'');
    INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (43, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_PASSWORD_NOT_ENCRYPTED'', CAST(1 AS bit), N''en'', NULL, NULL, N''A security error occurred. Please contact support immediately.''),
    (44, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''WALLET_PASSWORD_NOT_ENCRYPTED'', CAST(1 AS bit), N''es'', NULL, NULL, N''Ocurrió un error de seguridad. Contacte soporte inmediatamente.''),
    (45, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''RANK_NOT_FOUND'', CAST(1 AS bit), N''en'', NULL, NULL, N''The requested rank information could not be found.''),
    (46, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''RANK_NOT_FOUND'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se encontró la información del rango solicitado.''),
    (47, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PAYMENT_FAILED'', CAST(1 AS bit), N''en'', NULL, NULL, N''Your payment could not be processed. Please verify your payment details.''),
    (48, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''PAYMENT_FAILED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar tu pago. Por favor verifica tus datos de pago.''),
    (49, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REFUND_FAILED'', CAST(1 AS bit), N''en'', NULL, NULL, N''The refund could not be processed at this time. Please contact support.''),
    (50, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''REFUND_FAILED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No se pudo procesar el reembolso en este momento. Contacte soporte.''),
    (51, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNAUTHORIZED'', CAST(1 AS bit), N''en'', NULL, NULL, N''You are not authorized to perform this action.''),
    (52, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''UNAUTHORIZED'', CAST(1 AS bit), N''es'', NULL, NULL, N''No tienes autorización para realizar esta acción.''),
    (53, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''VALIDATION_ERROR'', CAST(1 AS bit), N''en'', NULL, NULL, N''The information you provided is invalid. Please review and try again.''),
    (54, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''VALIDATION_ERROR'', CAST(1 AS bit), N''es'', NULL, NULL, N''La información proporcionada no es válida. Por favor revísela e intente de nuevo.'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'ErrorCode', N'IsActive', N'Language', N'LastUpdateBy', N'LastUpdateDate', N'UserFriendlyMessage') AND [object_id] = OBJECT_ID(N'[ErrorMessages]'))
        SET IDENTITY_INSERT [ErrorMessages] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsAutoRenew', N'IsFree', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'Price', N'RenewalPrice', N'SortOrder') AND [object_id] = OBJECT_ID(N'[MembershipLevels]'))
        SET IDENTITY_INSERT [MembershipLevels] ON;
    EXEC(N'INSERT INTO [MembershipLevels] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsAutoRenew], [IsFree], [LastUpdateBy], [LastUpdateDate], [Name], [Price], [RenewalPrice], [SortOrder])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Annual business fee for team-building ambassadors. Qualifies for all commissions.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Lifestyle Ambassador'', 99.0, 99.0, 1),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage – VIP'', 40.0, 40.0, 2),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage – Elite'', 99.0, 99.0, 3),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage – Turbo'', 199.0, 199.0, 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsAutoRenew', N'IsFree', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'Price', N'RenewalPrice', N'SortOrder') AND [object_id] = OBJECT_ID(N'[MembershipLevels]'))
        SET IDENTITY_INSERT [MembershipLevels] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'PointsPerUnit', N'ProductId', N'RequiredSuccessfulPayments') AND [object_id] = OBJECT_ID(N'[ProductLoyaltySettings]'))
        SET IDENTITY_INSERT [ProductLoyaltySettings] ON;
    EXEC(N'INSERT INTO [ProductLoyaltySettings] ([Id], [CreatedBy], [CreationDate], [IsActive], [LastUpdateBy], [LastUpdateDate], [PointsPerUnit], [ProductId], [RequiredSuccessfulPayments])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 3.0, N''00000002-prod-0000-0000-000000000002'', 1),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 6.0, N''00000003-prod-0000-0000-000000000003'', 1),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), NULL, NULL, 6.0, N''00000004-prod-0000-0000-000000000004'', 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'PointsPerUnit', N'ProductId', N'RequiredSuccessfulPayments') AND [object_id] = OBJECT_ID(N'[ProductLoyaltySettings]'))
        SET IDENTITY_INSERT [ProductLoyaltySettings] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''00000001-prod-0000-0000-000000000001'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', NULL, 0.0, 0.0, N''Travel Advantage Guest Member'', 1, 0.0, 0.0, 0, 0, 0.0, 0.0),
    (N''00000006-prod-0000-0000-000000000006'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', NULL, 0.0, 0.0, N''Monthly Subscription'', 6, 0.0, 0.0, 0, 0, 0.0, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] ON;
    EXEC(N'INSERT INTO [RankDefinitions] ([Id], [CertificateTemplateUrl], [CreatedBy], [CreationDate], [Description], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder], [Status])
    VALUES (1, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''18 ET points (3 Elite/Turbo members). DTR: $4/day.'', NULL, NULL, N''Silver'', 1, 1),
    (2, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''72 ET points (12 Elite/Turbo members). DTR: $10/day.'', NULL, NULL, N''Gold'', 2, 1),
    (3, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''175 ET points. DTR: $15/day. Boost Bonus unlocked.'', NULL, NULL, N''Platinum'', 3, 1),
    (4, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''350 DT / 175 ET points. DTR: $25/day.'', NULL, NULL, N''Titanium'', 4, 1),
    (5, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.'', NULL, NULL, N''Jade'', 5, 1),
    (6, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''1,500 DT / 750 ET points. DTR: $80/day.'', NULL, NULL, N''Pearl'', 6, 1),
    (7, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''3,000 DT / 1,500 ET points. DTR: $150/day.'', NULL, NULL, N''Emerald'', 7, 1),
    (8, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''6,000 DT / 3,000 ET points. DTR: $300/day.'', NULL, NULL, N''Ruby'', 8, 1),
    (9, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''10,000 DT / 5,000 ET points. DTR: $500/day.'', NULL, NULL, N''Sapphire'', 9, 1),
    (10, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''15,000 DT / 7,500 ET points. DTR: $750/day.'', NULL, NULL, N''Diamond'', 10, 1),
    (11, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''20,000 DT / 10,000 ET points. DTR: $1,000/day.'', NULL, NULL, N''Double Diamond'', 11, 1),
    (12, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''30,000 DT / 15,000 ET points. DTR: $1,500/day.'', NULL, NULL, N''Triple Diamond'', 12, 1),
    (13, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''60,000 DT / 30,000 ET points. DTR: $2,000/day.'', NULL, NULL, N''Blue Diamond'', 13, 1),
    (14, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''120,000 DT / 60,000 ET points. DTR: $3,000/day.'', NULL, NULL, N''Black Diamond'', 14, 1),
    (15, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''200,000 DT / 100,000 ET points. DTR: $4,000/day.'', NULL, NULL, N''Royal'', 15, 1),
    (16, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''300,000 DT / 150,000 ET points. DTR: $5,000/day.'', NULL, NULL, N''Double Royal'', 16, 1),
    (17, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''400,000 DT / 200,000 ET points. DTR: $7,500/day.'', NULL, NULL, N''Triple Royal'', 17, 1),
    (18, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''500,000 DT / 250,000 ET points. DTR: $10,000/day.'', NULL, NULL, N''Blue Royal'', 18, 1),
    (19, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''700,000 DT / 350,000 ET points. DTR: $15,000/day.'', NULL, NULL, N''Black Royal'', 19, 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] ON;
    EXEC(N'INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 1, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 2, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 3, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (4, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 4, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (5, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 5, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (6, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 6, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (7, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 7, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (8, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 8, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (9, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 9, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (10, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 10, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (11, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 11, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (12, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 12, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (13, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 13, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (14, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 14, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (15, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 15, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (16, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 16, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (17, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 17, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (19, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 19, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (20, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 20, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (21, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 21, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (22, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 22, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (23, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 23, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (24, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 24, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (25, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 25, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (26, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 26, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (27, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 27, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (28, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 28, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (29, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 29, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (30, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 30, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (31, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 31, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (32, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 32, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (33, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 33, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (34, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 34, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (35, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 35, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (36, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 36, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (37, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 37, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (38, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 38, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (39, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 39, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (40, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 40, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (41, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 41, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (42, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 42, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (43, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 43, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (44, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 44, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (45, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 45, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (46, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 46, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (47, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 47, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (48, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 48, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (49, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 49, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (50, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 50, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (51, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 51, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (52, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 52, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (53, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 53, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (54, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 54, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (55, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 55, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (56, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 56, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (57, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 57, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (58, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 58, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (59, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 59, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (60, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 60, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (61, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 61, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (62, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 62, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (63, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 63, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (64, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 64, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (65, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 65, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (66, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 66, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (67, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 67, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (68, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 68, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (69, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 69, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (70, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 70, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (71, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 71, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (72, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 72, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (73, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 73, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (74, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 74, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (75, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 75, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (76, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 76, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (77, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 77, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (78, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 78, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (79, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 79, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (80, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 80, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (81, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 81, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (82, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 82, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (83, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 83, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (84, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 84, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (85, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 85, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (86, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 86, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (87, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 87, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (88, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 88, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (89, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 89, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (90, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 90, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (91, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 91, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (92, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 92, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (93, CAST(0 AS bit), 0.0, 40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 93, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (94, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 94, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (95, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 95, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (96, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 96, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (97, CAST(1 AS bit), 0.0, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 97, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (98, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 98, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (99, CAST(1 AS bit), 0.0, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 99, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CommissionPerToken', N'CommissionTypeId', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'TokenTypeId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[TokenTypeCommissions]'))
        SET IDENTITY_INSERT [TokenTypeCommissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] ON;
    EXEC(N'INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro'', NULL),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, N''Guest Member'', NULL),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite'', NULL),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: VIP'', NULL),
    (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite'', NULL),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage Elite (Signup)'', NULL),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Travel Advantage Lite'', NULL),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador'', NULL),
    (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment Pro ($99.97 cost / no commission)'', NULL),
    (10, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual Fee'', NULL),
    (11, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event'', NULL),
    (12, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro + Event'', NULL),
    (13, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP Member'', NULL),
    (14, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Mobile App'', NULL),
    (15, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Pro Member'', NULL),
    (16, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member'', NULL),
    (17, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Elite'', NULL),
    (19, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Special'', NULL),
    (20, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N''--Available--'', NULL),
    (21, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual: VIP 365'', NULL),
    (22, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro'', NULL),
    (23, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Annual: Biz Center'', NULL),
    (24, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Legacy Biz Center Fee'', NULL),
    (25, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Mall'', NULL),
    (26, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite180'', NULL),
    (27, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite180 + Event'', NULL),
    (28, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro180'', NULL),
    (29, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Pro180 + Event'', NULL),
    (30, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus180'', NULL),
    (31, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus180 + Event'', NULL),
    (32, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite180 Member'', NULL),
    (33, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Pro180 Member'', NULL),
    (34, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus180 Member'', NULL),
    (35, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Pro'', NULL),
    (36, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Pro180'', NULL),
    (37, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Elite'', NULL),
    (38, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus180 to Elite180'', NULL),
    (39, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Pro180'', NULL),
    (40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro to Elite180'', NULL),
    (41, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro180 to Elite'', NULL),
    (42, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Pro180 to Elite180'', NULL),
    (43, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Elite to Elite180'', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (44, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 Level 2 (79.97)'', NULL),
    (45, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 Level 3 (39.97)'', NULL),
    (46, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro180 Level 2'', NULL),
    (47, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Pro180 Level 3'', NULL),
    (48, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Plus180'', NULL),
    (49, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus'', NULL),
    (50, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus + Event'', NULL),
    (51, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Plus'', NULL),
    (52, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus to Elite'', NULL),
    (53, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Plus to Elite180'', NULL),
    (54, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus Member'', NULL),
    (55, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Monthly: Elite180 (59.97)'', NULL),
    (56, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP'', NULL),
    (57, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP 365'', NULL),
    (58, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Plus'', NULL),
    (59, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Elite'', NULL),
    (60, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to Elite180'', NULL),
    (61, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Plus'', NULL),
    (62, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Elite'', NULL),
    (63, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: VIP to Elite180'', NULL),
    (64, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP'', NULL),
    (65, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP + Event'', NULL),
    (66, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Elite to Turbo'', NULL),
    (67, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 365'', NULL),
    (68, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 365 + Event'', NULL),
    (69, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO'', NULL),
    (70, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event + TURBO'', NULL),
    (71, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Coupon)'', NULL),
    (72, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event (Coupon)'', NULL),
    (73, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + Event + TURBO (Coupon)'', NULL),
    (74, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Coupon)'', NULL),
    (75, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180'', NULL),
    (76, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180 + Event'', NULL),
    (77, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Upgrade: Guest to VIP 180'', NULL),
    (78, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Recurring: VIP 180'', NULL),
    (79, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP 180 Member'', NULL),
    (80, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member + TURBO'', NULL),
    (81, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite FREE'', NULL),
    (82, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Coupon) FREE'', NULL),
    (83, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO FREE'', NULL),
    (84, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Coupon) FREE'', NULL),
    (85, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus FREE'', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (86, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP FREE'', NULL),
    (87, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + VIP 180 FREE'', NULL),
    (88, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador FREE'', NULL),
    (89, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member FREE'', NULL),
    (90, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Elite Member + TURBO FREE'', NULL),
    (91, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Plus Member FREE'', NULL),
    (92, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP Member FREE'', NULL),
    (93, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: VIP 180 FREE'', NULL),
    (94, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Elite Ambassador SpecialPromo'', NULL),
    (95, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Plus Ambassador SpecialPromo'', NULL),
    (96, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Turbo Ambassador SpecialPromo'', NULL),
    (97, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Plus (Help a Friend)'', NULL),
    (98, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite (Help a Friend)'', NULL),
    (99, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''Enrollment: Ambassador + Elite + TURBO (Help a Friend)'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'IsGuestPass', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'TemplateUrl') AND [object_id] = OBJECT_ID(N'[TokenTypes]'))
        SET IDENTITY_INSERT [TokenTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (1, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''One-time $20 bonus to the direct enroller when a VIP member signs up.'', 0, 0, 20.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''Member Bonus – VIP'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 29, 0, 0, 0),
    (2, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''One-time $40 bonus to the direct enroller when an Elite member signs up.'', 0, 0, 40.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''Member Bonus – Elite'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 30, 0, 0, 0),
    (3, 1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''One-time $80 bonus to the direct enroller when a Turbo member signs up.'', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''Member Bonus – Turbo'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 31, 0, 0, 0),
    (4, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 14, N''Earn $150 when enrolling within your first 14 days as an ambassador.'', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 1'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 1),
    (5, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 7, N''Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).'', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 2'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 2),
    (6, 2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 7, N''Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).'', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''Fast Start Bonus – Window 3'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 3),
    (7, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).'', 0, 0, 4.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Silver'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 18, 0),
    (8, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).'', 0, 0, 10.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Gold'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 72, 0),
    (9, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).'', 0, 0, 15.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Platinum'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 175, 0),
    (10, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $25/day when qualifying at Titanium rank (350 Dual Team points).'', 0, 0, 25.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Titanium'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 350, 0),
    (11, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $40/day when qualifying at Jade rank (700 Dual Team points).'', 0, 0, 40.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Jade'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 700, 0),
    (12, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).'', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Pearl'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 1500, 0),
    (13, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).'', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Emerald'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 3000, 0),
    (14, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).'', 0, 0, 300.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Ruby'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 6000, 0),
    (15, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).'', 0, 0, 500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Sapphire'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 10000, 0),
    (16, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).'', 0, 0, 750.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 15000, 0),
    (17, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).'', 0, 0, 1000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Double Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 20000, 0),
    (18, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).'', 0, 0, 1500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Triple Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 30000, 0),
    (19, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).'', 0, 0, 2000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Blue Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 60000, 0),
    (20, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).'', 0, 0, 3000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Black Diamond'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 120000, 0),
    (21, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $4,000/day when qualifying at Royal rank (200,000 DT points).'', 0, 0, 4000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 200000, 0),
    (22, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $5,000/day when qualifying at Double Royal (300,000 DT points).'', 0, 0, 5000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Double Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 300000, 0),
    (23, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).'', 0, 0, 7500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Triple Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 400000, 0),
    (24, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).'', 0, 0, 10000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Blue Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 500000, 0),
    (25, 3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $15,000/day when qualifying at Black Royal (700,000 DT points).'', 0, 0, 15000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''DTR – Black Royal'', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 700000, 0),
    (26, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.'', 0, 0, 600.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 4, 0.5E0, 0.5E0, 0, N''Boost Bonus – Gold'', 6, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 1),
    (27, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.'', 0, 0, 1200.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 5, 0.5E0, 0.5E0, 0, N''Boost Bonus – Platinum'', 12, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 2),
    (28, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 6, 0.5E0, 0.5E0, 0, N''Presidential Bonus'', 0, 15, 20.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (29, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a VIP Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (30, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses an Elite Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (31, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Turbo Member Bonus when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Member Bonus Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (32, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses any FSB window earning when a signup cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Fast Start Bonus'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (33, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.'', 0, 0, 25.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – VIP'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 41, 0, 0, 0),
    (34, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.'', 0, 0, 60.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – Elite'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 42, 0, 0, 0),
    (35, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.'', 0, 0, 120.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus – Turbo'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 43, 0, 0, 0),
    (36, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.'', 0, 0, 30.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – VIP'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 44, 0, 0, 0),
    (37, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.'', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Elite'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 45, 0, 0, 0),
    (38, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.'', 0, 0, 160.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Turbo'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 46, 0, 0, 0),
    (39, 8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Admin Fee'', 0, 0, 5.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (40, 8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.'', 0, 0, 1.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Token Deduction'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (41, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (42, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0);
    INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (43, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (44, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo VIP'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (45, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo Elite'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (46, 5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.'', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Reversal – Builder Bonus Turbo Turbo'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''00000002-prod-0000-0000-000000000002'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 2, 40.0, 0.0, N''Travel Advantage VIP'', 2, 0.0, 0.0, 3, 0, 0.0, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'JoinPageMembership', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [JoinPageMembership], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''00000003-prod-0000-0000-000000000003'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 3, 99.0, 0.0, N''Travel Advantage Elite'', 3, 0.0, 0.0, 6, 0, 0.0, 0.0),
    (N''00000004-prod-0000-0000-000000000004'', 0.0, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 4, 199.0, 0.0, N''Travel Advantage Turbo'', 4, 0.0, 0.0, 6, 0, 0.0, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'JoinPageMembership', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CorporateFee', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N'INSERT INTO [Products] ([Id], [AnnualPrice], [CorporateFee], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''00000005-prod-0000-0000-000000000005'', 99.0, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'', NULL, N'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''2026-03-16T00:00:00.0000000Z'', 1, 0.0, 0.0, N''Subscription'', 5, 0.0, 0.0, 0, 0, 99.0, 0.0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AnnualPrice', N'CorporateFee', N'CreatedBy', N'CreationDate', N'DeletedAt', N'DeletedBy', N'Description', N'DescriptionPromo', N'ImageUrl', N'ImageUrlPromo', N'IsActive', N'IsDeleted', N'LastUpdateBy', N'LastUpdateDate', N'MembershipLevelId', N'MonthlyFee', N'MonthlyFeePromo', N'Name', N'OldSystemProductId', N'Price180Days', N'Price90Days', N'QualificationPoins', N'QualificationPoinsPromo', N'SetupFee', N'SetupFeePromo') AND [object_id] = OBJECT_ID(N'[Products]'))
        SET IDENTITY_INSERT [Products] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AchievementMessage', N'CertificateUrl', N'CreatedBy', N'CreationDate', N'CurrentRankDescription', N'DailyBonus', N'EnrollmentQualifiedTeamMembers', N'EnrollmentTeam', N'ExternalMembers', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifetimeHoldingDuration', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MonthlyBonus', N'PersonalPoints', N'PlacementQualifiedTeamMembers', N'RankBonus', N'RankDefinitionId', N'RankDescription', N'SalesVolume', N'SponsoredMembers', N'TeamPoints') AND [object_id] = OBJECT_ID(N'[RankRequirements]'))
        SET IDENTITY_INSERT [RankRequirements] ON;
    EXEC(N'INSERT INTO [RankRequirements] ([Id], [AchievementMessage], [CertificateUrl], [CreatedBy], [CreationDate], [CurrentRankDescription], [DailyBonus], [EnrollmentQualifiedTeamMembers], [EnrollmentTeam], [ExternalMembers], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifetimeHoldingDuration], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MonthlyBonus], [PersonalPoints], [PlacementQualifiedTeamMembers], [RankBonus], [RankDefinitionId], [RankDescription], [SalesVolume], [SponsoredMembers], [TeamPoints])
    VALUES (1, N''Congratulations! You have reached Silver rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.'', 4.0, 0, 3, 1, NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 100.0, 1, N''Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).'', 0.0, 1, 18),
    (2, N''Congratulations! You have reached Gold rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.'', 10.0, 0, 12, 1, NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 300.0, 2, N''Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).'', 0.0, 1, 72),
    (3, N''Congratulations! You have reached Platinum rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.'', 15.0, 0, 29, 1, NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 500.0, 3, N''Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.'', 0.0, 2, 175),
    (4, N''Congratulations! You have reached Titanium rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.'', 25.0, 0, 0, 1, NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 1000.0, 4, N''Qualify with 350 Dual Team points (max 50% per branch).'', 0.0, 2, 350),
    (5, N''Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.'', 40.0, 0, 0, 1, NULL, NULL, 5, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 2500.0, 5, N''Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked.'', 0.0, 3, 700),
    (6, N''Congratulations! You have reached Pearl rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.'', 80.0, 0, 0, 1, NULL, NULL, 6, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 5000.0, 6, N''Qualify with 1,500 Dual Team points (max 50% per branch).'', 0.0, 3, 1500),
    (7, N''Congratulations! You have reached Emerald rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.'', 150.0, 0, 0, 1, NULL, NULL, 7, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 10000.0, 7, N''Qualify with 3,000 Dual Team points (max 50% per branch).'', 0.0, 4, 3000),
    (8, N''Congratulations! You have reached Ruby rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.'', 300.0, 0, 0, 1, NULL, NULL, 8, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 25000.0, 8, N''Qualify with 6,000 Dual Team points (max 50% per branch).'', 0.0, 5, 6000),
    (9, N''Congratulations! You have reached Sapphire rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.'', 500.0, 0, 0, 1, NULL, NULL, 9, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 50000.0, 9, N''Qualify with 10,000 Dual Team points (max 50% per branch).'', 0.0, 5, 10000),
    (10, N''Congratulations! You have reached Diamond rank and unlocked the Car Bonus!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.'', 750.0, 0, 0, 1, NULL, NULL, 10, 0, 0.5E0, 0.5E0, 500.0, 1, 0, 100000.0, 10, N''Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked.'', 0.0, 6, 15000),
    (11, N''Congratulations! You have reached Double Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.'', 1000.0, 0, 0, 1, NULL, NULL, 11, 0, 0.5E0, 0.5E0, 750.0, 1, 0, 150000.0, 11, N''Qualify with 20,000 Dual Team points (max 50% per branch).'', 0.0, 6, 20000),
    (12, N''Congratulations! You have reached Triple Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.'', 1500.0, 0, 0, 1, NULL, NULL, 12, 0, 0.5E0, 0.5E0, 1000.0, 1, 0, 200000.0, 12, N''Qualify with 30,000 Dual Team points (max 50% per branch).'', 0.0, 7, 30000),
    (13, N''Congratulations! You have reached Blue Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.'', 2000.0, 0, 0, 1, NULL, NULL, 13, 0, 0.5E0, 0.5E0, 1500.0, 1, 0, 300000.0, 13, N''Qualify with 60,000 Dual Team points (max 50% per branch).'', 0.0, 8, 60000),
    (14, N''Congratulations! You have reached Black Diamond rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.'', 3000.0, 0, 0, 1, NULL, NULL, 14, 0, 0.5E0, 0.5E0, 2500.0, 1, 0, 500000.0, 14, N''Qualify with 120,000 Dual Team points (max 50% per branch).'', 0.0, 10, 120000),
    (15, N''Congratulations! You have reached Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.'', 4000.0, 0, 0, 1, NULL, NULL, 15, 0, 0.5E0, 0.5E0, 4000.0, 1, 0, 750000.0, 15, N''Qualify with 200,000 Dual Team points (max 50% per branch).'', 0.0, 12, 200000),
    (16, N''Congratulations! You have reached Double Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.'', 5000.0, 0, 0, 1, NULL, NULL, 16, 0, 0.5E0, 0.5E0, 5000.0, 1, 0, 1000000.0, 16, N''Qualify with 300,000 Dual Team points (max 50% per branch).'', 0.0, 15, 300000),
    (17, N''Congratulations! You have reached Triple Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.'', 7500.0, 0, 0, 1, NULL, NULL, 17, 0, 0.5E0, 0.5E0, 7500.0, 1, 0, 1500000.0, 17, N''Qualify with 400,000 Dual Team points (max 50% per branch).'', 0.0, 20, 400000),
    (18, N''Congratulations! You have reached Blue Royal rank!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.'', 10000.0, 0, 0, 1, NULL, NULL, 18, 0, 0.5E0, 0.5E0, 10000.0, 1, 0, 2000000.0, 18, N''Qualify with 500,000 Dual Team points (max 50% per branch).'', 0.0, 25, 500000),
    (19, N''Congratulations! You have reached Black Royal — the highest rank in the company!'', NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.'', 15000.0, 0, 0, 1, NULL, NULL, 19, 0, 0.5E0, 0.5E0, 15000.0, 1, 0, 3000000.0, 19, N''Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey.'', 0.0, 30, 700000)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AchievementMessage', N'CertificateUrl', N'CreatedBy', N'CreationDate', N'CurrentRankDescription', N'DailyBonus', N'EnrollmentQualifiedTeamMembers', N'EnrollmentTeam', N'ExternalMembers', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifetimeHoldingDuration', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MonthlyBonus', N'PersonalPoints', N'PlacementQualifiedTeamMembers', N'RankBonus', N'RankDefinitionId', N'RankDescription', N'SalesVolume', N'SponsoredMembers', N'TeamPoints') AND [object_id] = OBJECT_ID(N'[RankRequirements]'))
        SET IDENTITY_INSERT [RankRequirements] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'ProductId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[ProductCommissions]'))
        SET IDENTITY_INSERT [ProductCommissions] ON;
    EXEC(N'INSERT INTO [ProductCommissions] ([Id], [CarBonusEligible], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [ProductId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000002-prod-0000-0000-000000000002'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000003-prod-0000-0000-000000000003'', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(1 AS bit), N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''00000004-prod-0000-0000-000000000004'', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CarBonusEligible', N'CreatedBy', N'CreationDate', N'EligibleDailyResidual', N'EligibleMembershipResidual', N'LastUpdateBy', N'LastUpdateDate', N'PresidentialBonusEligible', N'ProductId', N'TriggerBoostBonus', N'TriggerBuilderBonus', N'TriggerBuilderBonusTurbo', N'TriggerFastStartBonus', N'TriggerSponsorBonus', N'TriggerSponsorBonusTurbo') AND [object_id] = OBJECT_ID(N'[ProductCommissions]'))
        SET IDENTITY_INSERT [ProductCommissions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CannedResponses_TeamId] ON [CannedResponses] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDownHistories_CountDownId] ON [CommissionCountDownHistories] ([CountDownId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDownHistories_MemberId1] ON [CommissionCountDownHistories] ([MemberId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionCountDowns_MemberId1] ON [CommissionCountDowns] ([MemberId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_BeneficiaryMemberId_Status] ON [CommissionEarnings] ([BeneficiaryMemberId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_CommissionOperationTypeId] ON [CommissionEarnings] ([CommissionOperationTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_CommissionTypeId] ON [CommissionEarnings] ([CommissionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionEarnings_PeriodDate] ON [CommissionEarnings] ([PeriodDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CommissionEarnings_SourceOrderId_CommissionTypeId] ON [CommissionEarnings] ([SourceOrderId], [CommissionTypeId]) WHERE [SourceOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_CommissionTypes_CommissionCategoryId] ON [CommissionTypes] ([CommissionCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_HierarchyPath] ON [DualTeamTree] ([HierarchyPath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_MemberId] ON [DualTeamTree] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ErrorMessages_ErrorCode_Language] ON [ErrorMessages] ([ErrorCode], [Language]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_HierarchyPath] ON [GenealogyTree] ([HierarchyPath]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_MemberId_CreationDate] ON [GenealogyTree] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_GhostPoints_LegMemberId] ON [GhostPoints] ([LegMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_GhostPoints_MemberId] ON [GhostPoints] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_KbArticles_CategoryId] ON [KbArticles] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_KbArticles_HelpfulCount] ON [KbArticles] ([HelpfulCount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_KbArticles_Slug] ON [KbArticles] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_KbArticles_Visibility_CategoryId] ON [KbArticles] ([Visibility], [CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_KbArticleVersions_ArticleId] ON [KbArticleVersions] ([ArticleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_LoyaltyPoints_MemberProfileId] ON [LoyaltyPoints] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MemberNotifications_MemberProfileId] ON [MemberNotifications] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_ActiveMembershipId] ON [MemberProfiles] ([ActiveMembershipId]) WHERE [ActiveMembershipId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_BinaryNodeId] ON [MemberProfiles] ([BinaryNodeId]) WHERE [BinaryNodeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_EnrollmentNodeId] ON [MemberProfiles] ([EnrollmentNodeId]) WHERE [EnrollmentNodeId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberProfiles_MemberId] ON [MemberProfiles] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_ReplicateSiteSlug] ON [MemberProfiles] ([ReplicateSiteSlug]) WHERE [ReplicateSiteSlug] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberProfiles_UserId] ON [MemberProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MemberRankHistories_RankDefinitionId] ON [MemberRankHistories] ([RankDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MembershipLevelBenefits_MembershipLevelId] ON [MembershipLevelBenefits] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_LastOrderId1] ON [MembershipSubscriptions] ([LastOrderId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_MemberId] ON [MembershipSubscriptions] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_MembershipLevelId] ON [MembershipSubscriptions] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_MemberStatusHistories_MemberProfileId] ON [MemberStatusHistories] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_OrdersId] ON [OrderDetails] ([OrdersId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_PaymentHistories_OrdersId] ON [PaymentHistories] ([OrdersId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_ProductCommissionPromos_CorporatePromoId1] ON [ProductCommissionPromos] ([CorporatePromoId1]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_ProductCommissionPromos_ProductId] ON [ProductCommissionPromos] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductCommissions_ProductId] ON [ProductCommissions] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProductLoyaltySettings_ProductId] ON [ProductLoyaltySettings] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Products_MembershipLevelId] ON [Products] ([MembershipLevelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_RankRequirements_RankDefinitionId] ON [RankRequirements] ([RankDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_SlaBreaches_BreachedAt] ON [SlaBreaches] ([BreachedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_SlaBreaches_TicketId] ON [SlaBreaches] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_SupportAgents_TeamId] ON [SupportAgents] ([TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SupportAgents_UserId] ON [SupportAgents] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketAttachments_TicketId] ON [TicketAttachments] ([TicketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketCategories_DefaultSlaPolicyId] ON [TicketCategories] ([DefaultSlaPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketCategories_DefaultTeamId] ON [TicketCategories] ([DefaultTeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketCategories_ParentCategoryId] ON [TicketCategories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketComments_IsInternal] ON [TicketComments] ([IsInternal]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketComments_TicketId_CreationDate] ON [TicketComments] ([TicketId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TicketHistories_TicketId_CreationDate] ON [TicketHistories] ([TicketId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TicketMetrics_Date] ON [TicketMetrics] ([Date]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_AssignedTeamId_Status] ON [Tickets] ([AssignedTeamId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_AssignedToUserId_Status] ON [Tickets] ([AssignedToUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_CategoryId] ON [Tickets] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_MemberId_CreationDate] ON [Tickets] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_SlaPolicyId] ON [Tickets] ([SlaPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_Tickets_Status_LastUpdateDate] ON [Tickets] ([Status], [LastUpdateDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tickets_TicketNumber] ON [Tickets] ([TicketNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TokenBalances_MemberProfileId] ON [TokenBalances] ([MemberProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE INDEX [IX_TokenTransactions_MemberId_CreationDate] ON [TokenTransactions] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenTypeCommissions_TokenTypeId_CommissionTypeId] ON [TokenTypeCommissions] ([TokenTypeId], [CommissionTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenTypeProducts_TokenTypeId_ProductId] ON [TokenTypeProducts] ([TokenTypeId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_CommissionCountDowns_CountDownId] FOREIGN KEY ([CountDownId]) REFERENCES [CommissionCountDowns] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [CommissionCountDowns] ADD CONSTRAINT [FK_CommissionCountDowns_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [LoyaltyPoints] ADD CONSTRAINT [FK_LoyaltyPoints_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [MemberNotifications] ADD CONSTRAINT [FK_MemberNotifications_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD CONSTRAINT [FK_MemberProfiles_MembershipSubscriptions_ActiveMembershipId] FOREIGN KEY ([ActiveMembershipId]) REFERENCES [MembershipSubscriptions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260405130705_HelpdeskModule', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'DescriptionPromo');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [Products] ALTER COLUMN [DescriptionPromo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Description');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [Products] ALTER COLUMN [Description] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    ALTER TABLE [Products] ADD [ThemeClass] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = N''theme-product-guest''
    WHERE [Id] = N''00000001-prod-0000-0000-000000000001'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = N''theme-product-vip''
    WHERE [Id] = N''00000002-prod-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = N''theme-product-elite''
    WHERE [Id] = N''00000003-prod-0000-0000-000000000003'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = N''theme-product-turbo''
    WHERE [Id] = N''00000004-prod-0000-0000-000000000004'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = NULL
    WHERE [Id] = N''00000005-prod-0000-0000-000000000005'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [ThemeClass] = NULL
    WHERE [Id] = N''00000006-prod-0000-0000-000000000006'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260406183238_AddProductThemeClass'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260406183238_AddProductThemeClass', N'10.0.5');
END;

COMMIT;
GO


BEGIN TRANSACTION;
GO

-- Migration: 20260411013728_AddEmailTemplates
-- Note: Schema DDL is included in AddCountries migration (same EF batch)
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411013728_AddEmailTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411013728_AddEmailTemplates', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

-- Migration: 20260411014909_AddCountries
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [Countries] (
        [Id]                  int           NOT NULL IDENTITY,
        [Iso2]                nchar(2)      NOT NULL,
        [Iso3]                nchar(3)      NOT NULL,
        [NameEn]              nvarchar(100) NOT NULL,
        [NameNative]          nvarchar(100) NOT NULL,
        [DefaultLanguageCode] nvarchar(10)  NOT NULL,
        [FlagEmoji]           nvarchar(10)  NOT NULL,
        [PhoneCode]           nvarchar(10)  NULL,
        [IsActive]            bit           NOT NULL,
        [SortOrder]           int           NOT NULL,
        [CreationDate]        datetime2     NOT NULL,
        [CreatedBy]           nvarchar(100) NOT NULL,
        [LastUpdateDate]      datetime2     NULL,
        [LastUpdateBy]        nvarchar(100) NULL,
        CONSTRAINT [PK_Countries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplates] (
        [Id]            int            NOT NULL IDENTITY,
        [Name]          nvarchar(200)  NOT NULL,
        [EventType]     nvarchar(100)  NOT NULL,
        [Category]      nvarchar(100)  NOT NULL,
        [Description]   nvarchar(500)  NULL,
        [IsActive]      bit            NOT NULL,
        [CreationDate]  datetime2      NOT NULL,
        [CreatedBy]     nvarchar(100)  NOT NULL,
        [LastUpdateDate] datetime2     NULL,
        [LastUpdateBy]  nvarchar(100)  NULL,
        CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplateLocalizations] (
        [Id]              int            NOT NULL IDENTITY,
        [EmailTemplateId] int            NOT NULL,
        [LanguageCode]    nvarchar(10)   NOT NULL,
        [Subject]         nvarchar(500)  NOT NULL,
        [HtmlBody]        nvarchar(max)  NOT NULL,
        [TextBody]        nvarchar(max)  NULL,
        [CreationDate]    datetime2      NOT NULL,
        [CreatedBy]       nvarchar(100)  NOT NULL,
        [LastUpdateDate]  datetime2      NULL,
        [LastUpdateBy]    nvarchar(100)  NULL,
        CONSTRAINT [PK_EmailTemplateLocalizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailTemplateLocalizations_EmailTemplates_EmailTemplateId]
            FOREIGN KEY ([EmailTemplateId]) REFERENCES [EmailTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplateVariables] (
        [Id]              int            NOT NULL IDENTITY,
        [EmailTemplateId] int            NOT NULL,
        [Name]            nvarchar(100)  NOT NULL,
        [Description]     nvarchar(500)  NULL,
        [IsRequired]      bit            NOT NULL,
        [CreationDate]    datetime2      NOT NULL,
        [CreatedBy]       nvarchar(100)  NOT NULL,
        [LastUpdateDate]  datetime2      NULL,
        [LastUpdateBy]    nvarchar(100)  NULL,
        CONSTRAINT [PK_EmailTemplateVariables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailTemplateVariables_EmailTemplates_EmailTemplateId]
            FOREIGN KEY ([EmailTemplateId]) REFERENCES [EmailTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE INDEX [IX_Countries_IsActive] ON [Countries] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Countries_Iso2] ON [Countries] ([Iso2]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Countries_Iso3] ON [Countries] ([Iso3]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmailTemplateLocalizations_EmailTemplateId_LanguageCode]
        ON [EmailTemplateLocalizations] ([EmailTemplateId], [LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE INDEX [IX_EmailTemplates_EventType] ON [EmailTemplates] ([EventType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EmailTemplateVariables_EmailTemplateId_Name]
        ON [EmailTemplateVariables] ([EmailTemplateId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411014909_AddCountries', N'10.0.5');
END;

COMMIT;
GO

BEGIN TRANSACTION;
GO

-- Migration: 20260411020839_AddCountryProductMapping
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE TABLE [CountryProducts] (
        [Id]            int            NOT NULL IDENTITY,
        [CountryId]     int            NOT NULL,
        [ProductId]     nvarchar(450)  NOT NULL,
        [IsActive]      bit            NOT NULL,
        [CreationDate]  datetime2      NOT NULL,
        [CreatedBy]     nvarchar(100)  NOT NULL,
        [LastUpdateDate] datetime2     NULL,
        [LastUpdateBy]  nvarchar(100)  NULL,
        CONSTRAINT [PK_CountryProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CountryProducts_Countries_CountryId]
            FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CountryProducts_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_CountryId]
        ON [CountryProducts] ([CountryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CountryProducts_CountryId_ProductId]
        ON [CountryProducts] ([CountryId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_IsActive]
        ON [CountryProducts] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_ProductId]
        ON [CountryProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411020839_AddCountryProductMapping', N'10.0.5');
END;

COMMIT;
GO
