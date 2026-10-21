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
    VALUES (N'20260316201542_InitialCreate', N'10.0.11');
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
    VALUES (N'20260316204432_UpdateCompPlanSeeds', N'10.0.11');
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
    VALUES (N'20260316205302_AddIsEnrollmentBasedToCommissionType', N'10.0.11');
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
    VALUES (N'20260317171759_AddMemberFcmToken', N'10.0.11');
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
    VALUES (N'20260320004824_AddIdentityAndAuth', N'10.0.11');
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
    VALUES (N'20260320132346_AddBuilderBonusAndDeductions', N'10.0.11');
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
    VALUES (N'20260320134440_AddRankRequirementsAndTokenTypes', N'10.0.11');
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
    VALUES (N'20260320160535_AddFullTokenTypeCatalog', N'10.0.11');
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
    VALUES (N'20260320162107_AddProductSeeds', N'10.0.11');
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
    VALUES (N'20260325233343_R1_SignupWizardAndEmailField', N'10.0.11');
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
    VALUES (N'20260326174752_AddProductJoinPageFlags', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326200000_RestoreEliteProduct'
)
BEGIN
    EXEC(N'
                    MERGE INTO [Products] AS target
                    USING (SELECT ''00000003-prod-0000-0000-000000000003'' AS Id) AS source
                    ON target.[Id] = source.[Id]
                    WHEN MATCHED THEN
                        UPDATE SET
                            [Name]               = ''Travel Advantage Elite'',
                            [Description]        = ''Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.'',
                            [ImageUrl]           = '''',
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
                            [LastUpdateDate]     = ''2026-03-16T00:00:00.000'',
                            [LastUpdateBy]       = ''migration-restore''
                    WHEN NOT MATCHED THEN
                        INSERT ([Id],[Name],[Description],[ImageUrl],[MonthlyFee],[SetupFee],
                                [Price90Days],[Price180Days],[AnnualPrice],[MonthlyFeePromo],[SetupFeePromo],
                                [QualificationPoins],[QualificationPoinsPromo],
                                [IsActive],[IsDeleted],[DeletedAt],[DeletedBy],
                                [CorporateFee],[JoinPageMembership],[OldSystemProductId],[MembershipLevelId],
                                [CreationDate],[CreatedBy],[LastUpdateDate],[LastUpdateBy])
                        VALUES (''00000003-prod-0000-0000-000000000003'',
                                ''Travel Advantage Elite'',
                                ''Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.'',
                                '''', 99.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00,
                                6, 0, 1, 0, NULL, NULL, 0, 1, 3, 3,
                                ''2026-03-16T00:00:00.000'', ''migration-restore'',
                                ''2026-03-16T00:00:00.000'', ''migration-restore'');
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260326200000_RestoreEliteProduct'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260326200000_RestoreEliteProduct', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'
    IF OBJECT_ID(N''dbo.__PlanQ_TablasPrevias'') IS NOT NULL DROP TABLE dbo.__PlanQ_TablasPrevias;
    SELECT name COLLATE DATABASE_DEFAULT AS Nombre INTO dbo.__PlanQ_TablasPrevias FROM sys.tables WHERE schema_id = SCHEMA_ID(N''dbo'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetRoles')
        EXEC(N'CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUsers')
        EXEC(N'CREATE TABLE [AspNetUsers] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AuditTracking')
        EXEC(N'CREATE TABLE [AuditTracking] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCategories')
        EXEC(N'CREATE TABLE [CommissionCategories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionOperationType')
        EXEC(N'CREATE TABLE [CommissionOperationType] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CommissionOperationType] PRIMARY KEY ([Id])
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CorporateEvents')
        EXEC(N'CREATE TABLE [CorporateEvents] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CorporatePromos')
        EXEC(N'CREATE TABLE [CorporatePromos] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CreditCards')
        EXEC(N'CREATE TABLE [CreditCards] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'DualTeamTree')
        EXEC(N'CREATE TABLE [DualTeamTree] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ErrorLogs')
        EXEC(N'CREATE TABLE [ErrorLogs] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ErrorMessages')
        EXEC(N'CREATE TABLE [ErrorMessages] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GenealogyTree')
        EXEC(N'CREATE TABLE [GenealogyTree] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GhostPoints')
        EXEC(N'CREATE TABLE [GhostPoints] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberFcmTokens')
        EXEC(N'CREATE TABLE [MemberFcmTokens] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberIdentificationTypes')
        EXEC(N'CREATE TABLE [MemberIdentificationTypes] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipLevels')
        EXEC(N'CREATE TABLE [MembershipLevels] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberStatistics')
        EXEC(N'CREATE TABLE [MemberStatistics] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Orders')
        EXEC(N'CREATE TABLE [Orders] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'PlacementLogs')
        EXEC(N'CREATE TABLE [PlacementLogs] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductLoyaltySettings')
        EXEC(N'CREATE TABLE [ProductLoyaltySettings] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'RankDefinitions')
        EXEC(N'CREATE TABLE [RankDefinitions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SlaPolicies')
        EXEC(N'CREATE TABLE [SlaPolicies] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SupportTeams')
        EXEC(N'CREATE TABLE [SupportTeams] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketMetrics')
        EXEC(N'CREATE TABLE [TicketMetrics] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketSequences')
        EXEC(N'CREATE TABLE [TicketSequences] (
        [Date] date NOT NULL,
        [LastSequence] int NOT NULL,
        CONSTRAINT [PK_TicketSequences] PRIMARY KEY ([Date])
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTransactions')
        EXEC(N'CREATE TABLE [TokenTransactions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypeCommissions')
        EXEC(N'CREATE TABLE [TokenTypeCommissions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypeProducts')
        EXEC(N'CREATE TABLE [TokenTypeProducts] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypes')
        EXEC(N'CREATE TABLE [TokenTypes] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'WalletHistories')
        EXEC(N'CREATE TABLE [WalletHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Wallets')
        EXEC(N'CREATE TABLE [Wallets] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetRoleClaims')
        EXEC(N'CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserClaims')
        EXEC(N'CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserLogins')
        EXEC(N'CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserRoles')
        EXEC(N'CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserTokens')
        EXEC(N'CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionTypes')
        EXEC(N'CREATE TABLE [CommissionTypes] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipLevelBenefits')
        EXEC(N'CREATE TABLE [MembershipLevelBenefits] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'CREATE TABLE [Products] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'OrderDetails')
        EXEC(N'CREATE TABLE [OrderDetails] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'PaymentHistories')
        EXEC(N'CREATE TABLE [PaymentHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberRankHistories')
        EXEC(N'CREATE TABLE [MemberRankHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'RankRequirements')
        EXEC(N'CREATE TABLE [RankRequirements] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CannedResponses')
        EXEC(N'CREATE TABLE [CannedResponses] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SupportAgents')
        EXEC(N'CREATE TABLE [SupportAgents] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketCategories')
        EXEC(N'CREATE TABLE [TicketCategories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'CREATE TABLE [CommissionEarnings] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissionPromos')
        EXEC(N'CREATE TABLE [ProductCommissionPromos] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissions')
        EXEC(N'CREATE TABLE [ProductCommissions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticles')
        EXEC(N'CREATE TABLE [KbArticles] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE TABLE [Tickets] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticleVersions')
        EXEC(N'CREATE TABLE [KbArticleVersions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SlaBreaches')
        EXEC(N'CREATE TABLE [SlaBreaches] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketAttachments')
        EXEC(N'CREATE TABLE [TicketAttachments] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketComments')
        EXEC(N'CREATE TABLE [TicketComments] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketHistories')
        EXEC(N'CREATE TABLE [TicketHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDownHistories')
        EXEC(N'CREATE TABLE [CommissionCountDownHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDowns')
        EXEC(N'CREATE TABLE [CommissionCountDowns] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'LoyaltyPoints')
        EXEC(N'CREATE TABLE [LoyaltyPoints] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberNotifications')
        EXEC(N'CREATE TABLE [MemberNotifications] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'CREATE TABLE [MemberProfiles] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipSubscriptions')
        EXEC(N'CREATE TABLE [MembershipSubscriptions] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberStatusHistories')
        EXEC(N'CREATE TABLE [MemberStatusHistories] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenBalances')
        EXEC(N'CREATE TABLE [TokenBalances] (
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
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCategories')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'') AND [object_id] = OBJECT_ID(N''[CommissionCategories]''))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N''INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''One-time bonuses paid to the direct enroller on new member signup (Member Bonus).'''', CAST(1 AS bit), NULL, NULL, N''''Enrollment Bonuses''''),
    (2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''3-window FSB paid when the enroller qualifies within each countdown window.'''', CAST(1 AS bit), NULL, NULL, N''''Fast Start Bonus''''),
    (3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Fixed daily earnings based on current binary rank qualification.'''', CAST(1 AS bit), NULL, NULL, N''''Dual Team Residuals''''),
    (4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Boost Bonus (weekly), Presidential Bonus (monthly), Car Bonus (monthly).'''', CAST(1 AS bit), NULL, NULL, N''''Leadership Bonuses''''),
    (5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Negative-amount entries that reverse previously paid commissions within the chargeback window.'''', CAST(1 AS bit), NULL, NULL, N''''Reversals''''),
    (6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Standard sponsor bonus paid on top of Member Bonus when a qualifying ambassador enrolls a new member.'''', CAST(1 AS bit), NULL, NULL, N''''Builder Bonus''''),
    (7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Enhanced sponsor bonus program with elevated payout rates, completely separate from standard Builder Bonus.'''', CAST(1 AS bit), NULL, NULL, N''''Builder Bonus Turbo''''),
    (8, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Administrative fee deductions and token-related deductions applied at payout or on token consumption.'''', CAST(1 AS bit), NULL, NULL, N''''Deductions'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'') AND [object_id] = OBJECT_ID(N''[CommissionCategories]''))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ErrorMessages')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''ErrorCode'', N''IsActive'', N''Language'', N''LastUpdateBy'', N''LastUpdateDate'', N''UserFriendlyMessage'') AND [object_id] = OBJECT_ID(N''[ErrorMessages]''))
        SET IDENTITY_INSERT [ErrorMessages] ON;
    EXEC(N''INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''DEFAULT'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''Something went wrong. Please try again or contact support.''''),
    (2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''DEFAULT'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Algo salió mal. Intente de nuevo o comuníquese con soporte.''''),
    (3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''INTERNAL_ERROR'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''An unexpected error occurred. Please try again later.''''),
    (4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''INTERNAL_ERROR'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Ocurrió un error inesperado. Por favor intente más tarde.''''),
    (5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''ORDER_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The requested order could not be found.''''),
    (6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''ORDER_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontró la orden solicitada.''''),
    (7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBER_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The member account could not be found.''''),
    (8, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBER_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontró la cuenta del miembro.''''),
    (9, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBER_ALREADY_EXISTS'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''An account with this information already exists.''''),
    (10, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBER_ALREADY_EXISTS'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Ya existe una cuenta con esta información.''''),
    (11, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''SPONSOR_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The sponsor ID you entered could not be found. Please verify and try again.''''),
    (12, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''SPONSOR_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''El ID de patrocinador ingresado no fue encontrado. Verifíquelo e intente de nuevo.''''),
    (13, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''DUPLICATE_REPLICATE_SITE'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''This website address is already taken. Please choose a different one.''''),
    (14, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''DUPLICATE_REPLICATE_SITE'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Esta dirección de sitio web ya está en uso. Por favor elija una diferente.''''),
    (15, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_LEVEL_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The selected membership plan is not available.''''),
    (16, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_LEVEL_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''El plan de membresía seleccionado no está disponible.''''),
    (17, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PRODUCT_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''One or more of the selected products are not available.''''),
    (18, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PRODUCT_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Uno o más de los productos seleccionados no están disponibles.''''),
    (19, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MINIMUM_AGE_REQUIRED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''You must be at least 18 years old to register.''''),
    (20, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MINIMUM_AGE_REQUIRED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Debes tener al menos 18 años para registrarte.''''),
    (21, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PLACEMENT_WINDOW_EXPIRED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The 30-day placement window has expired for this member.''''),
    (22, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PLACEMENT_WINDOW_EXPIRED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''El período de 30 días para colocar a este miembro ha expirado.''''),
    (23, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNPLACEMENT_LIMIT_EXCEEDED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The maximum number of placement changes for this member has been reached.''''),
    (24, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNPLACEMENT_LIMIT_EXCEEDED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Se alcanzó el límite máximo de cambios de posición para este miembro.''''),
    (25, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNPLACEMENT_WINDOW_EXPIRED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The 72-hour unplacement window has expired.''''),
    (26, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNPLACEMENT_WINDOW_EXPIRED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''El período de 72 horas para retirar la posición ha expirado.''''),
    (27, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_CHANGE_NOT_ALLOWED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''This membership change is not permitted at this time.''''),
    (28, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_CHANGE_NOT_ALLOWED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Este cambio de membresía no está permitido en este momento.''''),
    (29, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''No active membership was found for this account.''''),
    (30, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''MEMBERSHIP_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontró una membresía activa para esta cuenta.''''),
    (31, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''NO_SPONSOR_BONUS_TYPE'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The system could not process the bonus at this time. Please contact support.''''),
    (32, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''NO_SPONSOR_BONUS_TYPE'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''El sistema no pudo procesar el bono en este momento. Contacte soporte.''''),
    (33, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''NO_REVERSE_TYPE'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The reversal could not be processed. Please contact support.''''),
    (34, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''NO_REVERSE_TYPE'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se pudo procesar el reverso. Por favor contacte soporte.''''),
    (35, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''REVERSE_TYPE_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The reversal could not be processed. Please contact support.''''),
    (36, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''REVERSE_TYPE_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se pudo procesar el reverso. Por favor contacte soporte.''''),
    (37, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''COMMISSION_PERIOD_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''Commission data for the requested period could not be found.''''),
    (38, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''COMMISSION_PERIOD_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontraron datos de comisión para el período solicitado.''''),
    (39, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''INSUFFICIENT_TOKEN_BALANCE'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''You do not have enough tokens to complete this action.''''),
    (40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''INSUFFICIENT_TOKEN_BALANCE'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No tienes suficientes tokens para completar esta acción.''''),
    (41, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''WALLET_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''No payment method was found for this account.''''),
    (42, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''WALLET_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontró un método de pago para esta cuenta.'''');
    INSERT INTO [ErrorMessages] ([Id], [CreatedBy], [CreationDate], [ErrorCode], [IsActive], [Language], [LastUpdateBy], [LastUpdateDate], [UserFriendlyMessage])
    VALUES (43, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''WALLET_PASSWORD_NOT_ENCRYPTED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''A security error occurred. Please contact support immediately.''''),
    (44, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''WALLET_PASSWORD_NOT_ENCRYPTED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''Ocurrió un error de seguridad. Contacte soporte inmediatamente.''''),
    (45, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''RANK_NOT_FOUND'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The requested rank information could not be found.''''),
    (46, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''RANK_NOT_FOUND'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se encontró la información del rango solicitado.''''),
    (47, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PAYMENT_FAILED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''Your payment could not be processed. Please verify your payment details.''''),
    (48, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''PAYMENT_FAILED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se pudo procesar tu pago. Por favor verifica tus datos de pago.''''),
    (49, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''REFUND_FAILED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The refund could not be processed at this time. Please contact support.''''),
    (50, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''REFUND_FAILED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No se pudo procesar el reembolso en este momento. Contacte soporte.''''),
    (51, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNAUTHORIZED'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''You are not authorized to perform this action.''''),
    (52, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''UNAUTHORIZED'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''No tienes autorización para realizar esta acción.''''),
    (53, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''VALIDATION_ERROR'''', CAST(1 AS bit), N''''en'''', NULL, NULL, N''''The information you provided is invalid. Please review and try again.''''),
    (54, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''VALIDATION_ERROR'''', CAST(1 AS bit), N''''es'''', NULL, NULL, N''''La información proporcionada no es válida. Por favor revísela e intente de nuevo.'''')'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''ErrorCode'', N''IsActive'', N''Language'', N''LastUpdateBy'', N''LastUpdateDate'', N''UserFriendlyMessage'') AND [object_id] = OBJECT_ID(N''[ErrorMessages]''))
        SET IDENTITY_INSERT [ErrorMessages] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipLevels')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''IsAutoRenew'', N''IsFree'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''Price'', N''RenewalPrice'', N''SortOrder'') AND [object_id] = OBJECT_ID(N''[MembershipLevels]''))
        SET IDENTITY_INSERT [MembershipLevels] ON;
    EXEC(N''INSERT INTO [MembershipLevels] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsAutoRenew], [IsFree], [LastUpdateBy], [LastUpdateDate], [Name], [Price], [RenewalPrice], [SortOrder])
    VALUES (1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Annual business fee for team-building ambassadors. Qualifies for all commissions.'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Lifestyle Ambassador'''', 99.0, 99.0, 1),
    (2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Entry-level travel membership. 1 qualification point/month. Triggers $20 Member Bonus to enroller.'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Travel Advantage – VIP'''', 40.0, 40.0, 2),
    (3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Full travel membership. 6 qualification points/month. Triggers $40 Member Bonus to enroller.'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Travel Advantage – Elite'''', 99.0, 99.0, 3),
    (4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''Premium travel membership. 6 qualification points/month. Triggers $80 Member Bonus to enroller.'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Travel Advantage – Turbo'''', 199.0, 199.0, 4)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''IsAutoRenew'', N''IsFree'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''Price'', N''RenewalPrice'', N''SortOrder'') AND [object_id] = OBJECT_ID(N''[MembershipLevels]''))
        SET IDENTITY_INSERT [MembershipLevels] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductLoyaltySettings')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''IsActive'', N''LastUpdateBy'', N''LastUpdateDate'', N''PointsPerUnit'', N''ProductId'', N''RequiredSuccessfulPayments'') AND [object_id] = OBJECT_ID(N''[ProductLoyaltySettings]''))
        SET IDENTITY_INSERT [ProductLoyaltySettings] ON;
    EXEC(N''INSERT INTO [ProductLoyaltySettings] ([Id], [CreatedBy], [CreationDate], [IsActive], [LastUpdateBy], [LastUpdateDate], [PointsPerUnit], [ProductId], [RequiredSuccessfulPayments])
    VALUES (1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), NULL, NULL, 3.0, N''''00000002-prod-0000-0000-000000000002'''', 1),
    (2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), NULL, NULL, 6.0, N''''00000003-prod-0000-0000-000000000003'''', 1),
    (3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), NULL, NULL, 6.0, N''''00000004-prod-0000-0000-000000000004'''', 1)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''IsActive'', N''LastUpdateBy'', N''LastUpdateDate'', N''PointsPerUnit'', N''ProductId'', N''RequiredSuccessfulPayments'') AND [object_id] = OBJECT_ID(N''[ProductLoyaltySettings]''))
        SET IDENTITY_INSERT [ProductLoyaltySettings] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N''INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''''00000001-prod-0000-0000-000000000001'''', 0.0, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Free guest access to the Travel Advantage platform. No qualification points. No commissions triggered. Upgrade required to earn full benefits.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', NULL, 0.0, 0.0, N''''Travel Advantage Guest Member'''', 1, 0.0, 0.0, 0, 0, 0.0, 0.0),
    (N''''00000006-prod-0000-0000-000000000006'''', 0.0, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Generic recurring monthly subscription. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', NULL, 0.0, 0.0, N''''Monthly Subscription'''', 6, 0.0, 0.0, 0, 0, 0.0, 0.0)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'RankDefinitions')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CertificateTemplateUrl'', N''CreatedBy'', N''CreationDate'', N''Description'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''SortOrder'', N''Status'') AND [object_id] = OBJECT_ID(N''[RankDefinitions]''))
        SET IDENTITY_INSERT [RankDefinitions] ON;
    EXEC(N''INSERT INTO [RankDefinitions] ([Id], [CertificateTemplateUrl], [CreatedBy], [CreationDate], [Description], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder], [Status])
    VALUES (1, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''18 ET points (3 Elite/Turbo members). DTR: $4/day.'''', NULL, NULL, N''''Silver'''', 1, 1),
    (2, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''72 ET points (12 Elite/Turbo members). DTR: $10/day.'''', NULL, NULL, N''''Gold'''', 2, 1),
    (3, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''175 ET points. DTR: $15/day. Boost Bonus unlocked.'''', NULL, NULL, N''''Platinum'''', 3, 1),
    (4, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''350 DT / 175 ET points. DTR: $25/day.'''', NULL, NULL, N''''Titanium'''', 4, 1),
    (5, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''700 DT / 350 ET points. DTR: $40/day. Presidential unlocked.'''', NULL, NULL, N''''Jade'''', 5, 1),
    (6, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''1,500 DT / 750 ET points. DTR: $80/day.'''', NULL, NULL, N''''Pearl'''', 6, 1),
    (7, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''3,000 DT / 1,500 ET points. DTR: $150/day.'''', NULL, NULL, N''''Emerald'''', 7, 1),
    (8, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''6,000 DT / 3,000 ET points. DTR: $300/day.'''', NULL, NULL, N''''Ruby'''', 8, 1),
    (9, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''10,000 DT / 5,000 ET points. DTR: $500/day.'''', NULL, NULL, N''''Sapphire'''', 9, 1),
    (10, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''15,000 DT / 7,500 ET points. DTR: $750/day.'''', NULL, NULL, N''''Diamond'''', 10, 1),
    (11, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''20,000 DT / 10,000 ET points. DTR: $1,000/day.'''', NULL, NULL, N''''Double Diamond'''', 11, 1),
    (12, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''30,000 DT / 15,000 ET points. DTR: $1,500/day.'''', NULL, NULL, N''''Triple Diamond'''', 12, 1),
    (13, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''60,000 DT / 30,000 ET points. DTR: $2,000/day.'''', NULL, NULL, N''''Blue Diamond'''', 13, 1),
    (14, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''120,000 DT / 60,000 ET points. DTR: $3,000/day.'''', NULL, NULL, N''''Black Diamond'''', 14, 1),
    (15, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''200,000 DT / 100,000 ET points. DTR: $4,000/day.'''', NULL, NULL, N''''Royal'''', 15, 1),
    (16, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''300,000 DT / 150,000 ET points. DTR: $5,000/day.'''', NULL, NULL, N''''Double Royal'''', 16, 1),
    (17, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''400,000 DT / 200,000 ET points. DTR: $7,500/day.'''', NULL, NULL, N''''Triple Royal'''', 17, 1),
    (18, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''500,000 DT / 250,000 ET points. DTR: $10,000/day.'''', NULL, NULL, N''''Blue Royal'''', 18, 1),
    (19, NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''700,000 DT / 350,000 ET points. DTR: $15,000/day.'''', NULL, NULL, N''''Black Royal'''', 19, 1)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CertificateTemplateUrl'', N''CreatedBy'', N''CreationDate'', N''Description'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''SortOrder'', N''Status'') AND [object_id] = OBJECT_ID(N''[RankDefinitions]''))
        SET IDENTITY_INSERT [RankDefinitions] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypeCommissions')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CarBonusEligible'', N''CommissionPerToken'', N''CommissionTypeId'', N''CreatedBy'', N''CreationDate'', N''EligibleDailyResidual'', N''EligibleMembershipResidual'', N''LastUpdateBy'', N''LastUpdateDate'', N''PresidentialBonusEligible'', N''TokenTypeId'', N''TriggerBoostBonus'', N''TriggerBuilderBonus'', N''TriggerBuilderBonusTurbo'', N''TriggerFastStartBonus'', N''TriggerSponsorBonus'', N''TriggerSponsorBonusTurbo'') AND [object_id] = OBJECT_ID(N''[TokenTypeCommissions]''))
        SET IDENTITY_INSERT [TokenTypeCommissions] ON;
    EXEC(N''INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 1, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 2, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 3, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (4, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 4, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (5, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 5, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (6, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 6, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (7, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 7, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (8, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 8, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (9, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 9, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (10, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 10, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (11, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 11, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (12, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 12, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (13, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 13, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (14, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 14, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (15, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 15, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (16, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 16, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (17, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 17, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (19, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 19, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (20, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 20, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (21, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 21, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (22, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 22, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (23, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 23, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (24, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 24, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (25, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 25, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (26, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 26, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (27, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 27, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (28, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 28, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (29, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 29, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (30, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 30, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (31, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 31, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (32, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 32, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (33, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 33, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (34, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 34, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (35, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 35, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (36, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 36, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (37, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 37, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (38, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 38, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (39, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 39, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (40, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 40, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (41, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 41, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (42, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 42, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (43, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 43, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (44, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 44, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (45, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 45, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (46, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 46, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (47, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 47, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (48, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 48, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (49, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 49, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (50, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 50, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (51, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 51, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (52, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 52, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (53, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 53, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (54, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 54, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (55, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 55, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (56, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 56, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (57, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 57, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (58, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 58, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (59, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 59, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (60, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 60, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (61, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 61, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (62, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 62, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (63, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 63, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (64, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 64, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (65, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 65, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (66, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 66, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (67, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 67, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (68, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 68, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (69, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 69, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (70, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 70, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (71, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 71, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (72, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 72, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (73, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 73, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (74, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 74, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (75, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 75, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (76, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 76, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (77, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 77, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (78, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(0 AS bit), 78, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (79, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 79, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (80, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 80, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (81, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 81, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (82, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 82, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (83, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 83, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (84, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 84, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (85, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 85, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit));
    INSERT INTO [TokenTypeCommissions] ([Id], [CarBonusEligible], [CommissionPerToken], [CommissionTypeId], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [TokenTypeId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (86, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 86, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (87, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 87, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (88, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 88, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (89, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 89, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (90, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 90, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (91, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 91, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (92, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 92, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (93, CAST(0 AS bit), 0.0, 40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, CAST(0 AS bit), 93, CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit)),
    (94, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 94, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (95, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 95, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (96, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 96, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit)),
    (97, CAST(1 AS bit), 0.0, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 97, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (98, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 98, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (99, CAST(1 AS bit), 0.0, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), 99, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CarBonusEligible'', N''CommissionPerToken'', N''CommissionTypeId'', N''CreatedBy'', N''CreationDate'', N''EligibleDailyResidual'', N''EligibleMembershipResidual'', N''LastUpdateBy'', N''LastUpdateDate'', N''PresidentialBonusEligible'', N''TokenTypeId'', N''TriggerBoostBonus'', N''TriggerBuilderBonus'', N''TriggerBuilderBonusTurbo'', N''TriggerFastStartBonus'', N''TriggerSponsorBonus'', N''TriggerSponsorBonusTurbo'') AND [object_id] = OBJECT_ID(N''[TokenTypeCommissions]''))
        SET IDENTITY_INSERT [TokenTypeCommissions] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypes')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''IsGuestPass'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''TemplateUrl'') AND [object_id] = OBJECT_ID(N''[TokenTypes]''))
        SET IDENTITY_INSERT [TokenTypes] ON;
    EXEC(N''INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Pro'''', NULL),
    (2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, N''''Guest Member'''', NULL),
    (3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Elite'''', NULL),
    (4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: VIP'''', NULL),
    (5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite'''', NULL),
    (6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Travel Advantage Elite (Signup)'''', NULL),
    (7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Travel Advantage Lite'''', NULL),
    (8, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador'''', NULL),
    (9, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment Pro ($99.97 cost / no commission)'''', NULL),
    (10, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Annual Fee'''', NULL),
    (11, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + Event'''', NULL),
    (12, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Pro + Event'''', NULL),
    (13, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: VIP Member'''', NULL),
    (14, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Mobile App'''', NULL),
    (15, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Pro Member'''', NULL),
    (16, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite Member'''', NULL),
    (17, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Pro to Elite'''', NULL),
    (19, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite Special'''', NULL),
    (20, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N''''--Available--'''', NULL),
    (21, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Annual: VIP 365'''', NULL),
    (22, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Pro'''', NULL),
    (23, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Annual: Biz Center'''', NULL),
    (24, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Legacy Biz Center Fee'''', NULL),
    (25, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Mall'''', NULL),
    (26, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite180'''', NULL),
    (27, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite180 + Event'''', NULL),
    (28, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Pro180'''', NULL),
    (29, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Pro180 + Event'''', NULL),
    (30, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus180'''', NULL),
    (31, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus180 + Event'''', NULL),
    (32, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite180 Member'''', NULL),
    (33, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Pro180 Member'''', NULL),
    (34, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Plus180 Member'''', NULL),
    (35, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus180 to Pro'''', NULL),
    (36, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus180 to Pro180'''', NULL),
    (37, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus180 to Elite'''', NULL),
    (38, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus180 to Elite180'''', NULL),
    (39, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Pro to Pro180'''', NULL),
    (40, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Pro to Elite180'''', NULL),
    (41, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Pro180 to Elite'''', NULL),
    (42, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Pro180 to Elite180'''', NULL),
    (43, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Elite to Elite180'''', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (44, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Elite180 Level 2 (79.97)'''', NULL),
    (45, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Elite180 Level 3 (39.97)'''', NULL),
    (46, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Pro180 Level 2'''', NULL),
    (47, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Pro180 Level 3'''', NULL),
    (48, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Plus180'''', NULL),
    (49, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus'''', NULL),
    (50, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus + Event'''', NULL),
    (51, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Plus'''', NULL),
    (52, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus to Elite'''', NULL),
    (53, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Plus to Elite180'''', NULL),
    (54, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Plus Member'''', NULL),
    (55, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Monthly: Elite180 (59.97)'''', NULL),
    (56, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to VIP'''', NULL),
    (57, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to VIP 365'''', NULL),
    (58, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to Plus'''', NULL),
    (59, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to Elite'''', NULL),
    (60, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to Elite180'''', NULL),
    (61, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: VIP to Plus'''', NULL),
    (62, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: VIP to Elite'''', NULL),
    (63, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: VIP to Elite180'''', NULL),
    (64, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP'''', NULL),
    (65, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP + Event'''', NULL),
    (66, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Elite to Turbo'''', NULL),
    (67, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP 365'''', NULL),
    (68, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP 365 + Event'''', NULL),
    (69, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + TURBO'''', NULL),
    (70, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + Event + TURBO'''', NULL),
    (71, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite (Coupon)'''', NULL),
    (72, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + Event (Coupon)'''', NULL),
    (73, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + Event + TURBO (Coupon)'''', NULL),
    (74, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + TURBO (Coupon)'''', NULL),
    (75, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP 180'''', NULL),
    (76, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP 180 + Event'''', NULL),
    (77, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Upgrade: Guest to VIP 180'''', NULL),
    (78, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Recurring: VIP 180'''', NULL),
    (79, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: VIP 180 Member'''', NULL),
    (80, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite Member + TURBO'''', NULL),
    (81, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite FREE'''', NULL),
    (82, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite (Coupon) FREE'''', NULL),
    (83, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + TURBO FREE'''', NULL),
    (84, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + TURBO (Coupon) FREE'''', NULL),
    (85, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus FREE'''', NULL);
    INSERT INTO [TokenTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [IsGuestPass], [LastUpdateBy], [LastUpdateDate], [Name], [TemplateUrl])
    VALUES (86, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP FREE'''', NULL),
    (87, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + VIP 180 FREE'''', NULL),
    (88, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador FREE'''', NULL),
    (89, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite Member FREE'''', NULL),
    (90, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Elite Member + TURBO FREE'''', NULL),
    (91, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Plus Member FREE'''', NULL),
    (92, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: VIP Member FREE'''', NULL),
    (93, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: VIP 180 FREE'''', NULL),
    (94, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Elite Ambassador SpecialPromo'''', NULL),
    (95, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Plus Ambassador SpecialPromo'''', NULL),
    (96, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Turbo Ambassador SpecialPromo'''', NULL),
    (97, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Plus (Help a Friend)'''', NULL),
    (98, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite (Help a Friend)'''', NULL),
    (99, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, N''''Enrollment: Ambassador + Elite + TURBO (Help a Friend)'''', NULL)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CreatedBy'', N''CreationDate'', N''Description'', N''IsActive'', N''IsGuestPass'', N''LastUpdateBy'', N''LastUpdateDate'', N''Name'', N''TemplateUrl'') AND [object_id] = OBJECT_ID(N''[TokenTypes]''))
        SET IDENTITY_INSERT [TokenTypes] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionTypes')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CommissionCategoryId'', N''CreatedBy'', N''CreationDate'', N''Cummulative'', N''CurrentRank'', N''DaysAfterJoining'', N''Description'', N''EnrollmentTeam'', N''ExternalMembers'', N''FixedAmount'', N''IsActive'', N''IsEnrollmentBased'', N''IsPaidOnRenewal'', N''IsPaidOnSignup'', N''IsRealTime'', N''IsSponsorBonus'', N''LastUpdateBy'', N''LastUpdateDate'', N''LevelNo'', N''LifeTimeRank'', N''MaxEnrollmentTeamPointsPerBranch'', N''MaxTeamPointsPerBranch'', N''MembersRebill'', N''Name'', N''NewMembers'', N''PaymentDelayDays'', N''Percentage'', N''PersonalPoints'', N''ResidualBased'', N''ResidualOverCommissionType'', N''ResidualPercentage'', N''ReverseId'', N''SponsoredMembers'', N''TeamPoints'', N''TriggerOrder'') AND [object_id] = OBJECT_ID(N''[CommissionTypes]''))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N''INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (1, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''One-time $20 bonus to the direct enroller when a VIP member signs up.'''', 0, 0, 20.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''''Member Bonus – VIP'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 29, 0, 0, 0),
    (2, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''One-time $40 bonus to the direct enroller when an Elite member signs up.'''', 0, 0, 40.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''''Member Bonus – Elite'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 30, 0, 0, 0),
    (3, 1, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''One-time $80 bonus to the direct enroller when a Turbo member signs up.'''', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''''Member Bonus – Turbo'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 31, 0, 0, 0),
    (4, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 14, N''''Earn $150 when enrolling within your first 14 days as an ambassador.'''', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''''Fast Start Bonus – Window 1'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 1),
    (5, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 7, N''''Earn $150 within 7 days of triggering Reset 1 (after earning Window 1 bonus).'''', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''''Fast Start Bonus – Window 2'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 2),
    (6, 2, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 7, N''''Earn $150 within 7 days of triggering Reset 2 (after earning Window 2 bonus).'''', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0, N''''Fast Start Bonus – Window 3'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 32, 0, 0, 3),
    (7, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $4/day when qualifying at Silver rank (18 Enrollment Team points).'''', 0, 0, 4.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Silver'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 18, 0),
    (8, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $10/day when qualifying at Gold rank (72 Enrollment Team points).'''', 0, 0, 10.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Gold'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 72, 0),
    (9, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $15/day when qualifying at Platinum rank (175 Enrollment Team points).'''', 0, 0, 15.0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Platinum'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 175, 0),
    (10, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $25/day when qualifying at Titanium rank (350 Dual Team points).'''', 0, 0, 25.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Titanium'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 350, 0),
    (11, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $40/day when qualifying at Jade rank (700 Dual Team points).'''', 0, 0, 40.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Jade'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 700, 0),
    (12, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $80/day when qualifying at Pearl rank (1,500 Dual Team points).'''', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Pearl'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 1500, 0),
    (13, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $150/day when qualifying at Emerald rank (3,000 Dual Team points).'''', 0, 0, 150.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Emerald'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 3000, 0),
    (14, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $300/day when qualifying at Ruby rank (6,000 Dual Team points).'''', 0, 0, 300.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Ruby'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 6000, 0),
    (15, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $500/day when qualifying at Sapphire rank (10,000 Dual Team points).'''', 0, 0, 500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Sapphire'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 10000, 0),
    (16, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $750/day when qualifying at Diamond rank (15,000 Dual Team points).'''', 0, 0, 750.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Diamond'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 15000, 0),
    (17, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $1,000/day when qualifying at Double Diamond (20,000 DT points).'''', 0, 0, 1000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Double Diamond'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 20000, 0),
    (18, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $1,500/day when qualifying at Triple Diamond (30,000 DT points).'''', 0, 0, 1500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Triple Diamond'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 30000, 0),
    (19, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $2,000/day when qualifying at Blue Diamond (60,000 DT points).'''', 0, 0, 2000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Blue Diamond'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 60000, 0),
    (20, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $3,000/day when qualifying at Black Diamond (120,000 DT points).'''', 0, 0, 3000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Black Diamond'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 120000, 0),
    (21, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $4,000/day when qualifying at Royal rank (200,000 DT points).'''', 0, 0, 4000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Royal'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 200000, 0),
    (22, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $5,000/day when qualifying at Double Royal (300,000 DT points).'''', 0, 0, 5000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Double Royal'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 300000, 0),
    (23, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $7,500/day when qualifying at Triple Royal (400,000 DT points).'''', 0, 0, 7500.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Triple Royal'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 400000, 0),
    (24, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $10,000/day when qualifying at Blue Royal (500,000 DT points).'''', 0, 0, 10000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Blue Royal'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 500000, 0),
    (25, 3, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $15,000/day when qualifying at Black Royal (700,000 DT points).'''', 0, 0, 15000.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''DTR – Black Royal'''', 0, 4, 0.0, 0, CAST(1 AS bit), 0, 0.0E0, 0, 0, 700000, 0),
    (26, 4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $600 when 6+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Gold.'''', 0, 0, 600.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 4, 0.5E0, 0.5E0, 0, N''''Boost Bonus – Gold'''', 6, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 1),
    (27, 4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn $1,200 when 12+ new Elite/Turbo members join your Enrollment Team in a week. Based on Lifetime Rank Platinum. Supersedes Gold if both qualify.'''', 0, 0, 1200.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 5, 0.5E0, 0.5E0, 0, N''''Boost Bonus – Platinum'''', 12, 15, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 2),
    (28, 4, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Earn 20% of your Dual Team second-leg volume monthly. Unlocked at Jade rank (Lifetime Rank). Paid on the 15th.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 6, 0.5E0, 0.5E0, 0, N''''Presidential Bonus'''', 0, 15, 20.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (29, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a VIP Member Bonus when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Member Bonus VIP'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (30, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses an Elite Member Bonus when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Member Bonus Elite'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (31, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Turbo Member Bonus when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Member Bonus Turbo'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (32, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses any FSB window earning when a signup cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Fast Start Bonus'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (33, 6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Additional sponsor bonus paid when enrolling a VIP member. Stacks with Member Bonus.'''', 0, 0, 25.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus – VIP'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 41, 0, 0, 0),
    (34, 6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Additional sponsor bonus paid when enrolling an Elite member. Stacks with Member Bonus.'''', 0, 0, 60.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus – Elite'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 42, 0, 0, 0),
    (35, 6, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Additional sponsor bonus paid when enrolling a Turbo member. Stacks with Member Bonus.'''', 0, 0, 120.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus – Turbo'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 43, 0, 0, 0),
    (36, 7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Enhanced Builder Bonus (Turbo program) paid when enrolling a VIP member.'''', 0, 0, 30.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus Turbo – VIP'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 44, 0, 0, 0),
    (37, 7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Enhanced Builder Bonus (Turbo program) paid when enrolling an Elite member.'''', 0, 0, 80.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus Turbo – Elite'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 45, 0, 0, 0),
    (38, 7, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Enhanced Builder Bonus (Turbo program) paid when enrolling a Turbo member.'''', 0, 0, 160.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0, N''''Builder Bonus Turbo – Turbo'''', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 46, 0, 0, 0),
    (39, 8, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Administrative fee deducted from gross commission payout. Default: 5% of payout total. Adjust via admin panel per comp plan version.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Admin Fee'''', 0, 0, 5.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (40, 8, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; FixedAmount here is the platform default.'''', 0, 0, 1.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Token Deduction'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (41, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus VIP (ID 33) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus VIP'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (42, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus Elite (ID 34) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus Elite'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0);
    INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (43, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus Turbo (ID 35) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus Turbo'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (44, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus Turbo VIP (ID 36) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus Turbo VIP'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (45, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus Turbo Elite (ID 37) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus Turbo Elite'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (46, 5, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(0 AS bit), 0, 0, N''''Reverses a Builder Bonus Turbo Turbo (ID 38) when the member cancels within 14 days.'''', 0, 0, NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''''Reversal – Builder Bonus Turbo Turbo'''', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CommissionCategoryId'', N''CreatedBy'', N''CreationDate'', N''Cummulative'', N''CurrentRank'', N''DaysAfterJoining'', N''Description'', N''EnrollmentTeam'', N''ExternalMembers'', N''FixedAmount'', N''IsActive'', N''IsEnrollmentBased'', N''IsPaidOnRenewal'', N''IsPaidOnSignup'', N''IsRealTime'', N''IsSponsorBonus'', N''LastUpdateBy'', N''LastUpdateDate'', N''LevelNo'', N''LifeTimeRank'', N''MaxEnrollmentTeamPointsPerBranch'', N''MaxTeamPointsPerBranch'', N''MembersRebill'', N''Name'', N''NewMembers'', N''PaymentDelayDays'', N''Percentage'', N''PersonalPoints'', N''ResidualBased'', N''ResidualOverCommissionType'', N''ResidualPercentage'', N''ReverseId'', N''SponsoredMembers'', N''TeamPoints'', N''TriggerOrder'') AND [object_id] = OBJECT_ID(N''[CommissionTypes]''))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N''INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''''00000002-prod-0000-0000-000000000002'''', 0.0, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Entry-level Travel Advantage membership. Earns 3 qualification points per billing cycle. Triggers VIP Member Bonus ($20) and all standard enrollment commissions.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', 2, 40.0, 0.0, N''''Travel Advantage VIP'''', 2, 0.0, 0.0, 3, 0, 0.0, 0.0)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''JoinPageMembership'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N''INSERT INTO [Products] ([Id], [AnnualPrice], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [JoinPageMembership], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''''00000003-prod-0000-0000-000000000003'''', 0.0, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Full Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Elite Member Bonus ($40) and all standard enrollment commissions.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', 3, 99.0, 0.0, N''''Travel Advantage Elite'''', 3, 0.0, 0.0, 6, 0, 0.0, 0.0),
    (N''''00000004-prod-0000-0000-000000000004'''', 0.0, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Premium Travel Advantage membership. Earns 6 qualification points per billing cycle. Triggers Turbo Member Bonus ($80), full commissions, and Builder Bonus Turbo program.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', 4, 199.0, 0.0, N''''Travel Advantage Turbo'''', 4, 0.0, 0.0, 6, 0, 0.0, 0.0)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''JoinPageMembership'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CorporateFee'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] ON;
    EXEC(N''INSERT INTO [Products] ([Id], [AnnualPrice], [CorporateFee], [CreatedBy], [CreationDate], [DeletedAt], [DeletedBy], [Description], [DescriptionPromo], [ImageUrl], [ImageUrlPromo], [IsActive], [IsDeleted], [LastUpdateBy], [LastUpdateDate], [MembershipLevelId], [MonthlyFee], [MonthlyFeePromo], [Name], [OldSystemProductId], [Price180Days], [Price90Days], [QualificationPoins], [QualificationPoinsPromo], [SetupFee], [SetupFeePromo])
    VALUES (N''''00000005-prod-0000-0000-000000000005'''', 99.0, CAST(1 AS bit), N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', NULL, NULL, N''''Annual ambassador business fee. Operational/administrative product. Does not earn qualification points and does not trigger commissions.'''', NULL, N'''''''', NULL, CAST(1 AS bit), CAST(0 AS bit), NULL, ''''2026-03-16T00:00:00.0000000Z'''', 1, 0.0, 0.0, N''''Subscription'''', 5, 0.0, 0.0, 0, 0, 99.0, 0.0)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AnnualPrice'', N''CorporateFee'', N''CreatedBy'', N''CreationDate'', N''DeletedAt'', N''DeletedBy'', N''Description'', N''DescriptionPromo'', N''ImageUrl'', N''ImageUrlPromo'', N''IsActive'', N''IsDeleted'', N''LastUpdateBy'', N''LastUpdateDate'', N''MembershipLevelId'', N''MonthlyFee'', N''MonthlyFeePromo'', N''Name'', N''OldSystemProductId'', N''Price180Days'', N''Price90Days'', N''QualificationPoins'', N''QualificationPoinsPromo'', N''SetupFee'', N''SetupFeePromo'') AND [object_id] = OBJECT_ID(N''[Products]''))
        SET IDENTITY_INSERT [Products] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'RankRequirements')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AchievementMessage'', N''CertificateUrl'', N''CreatedBy'', N''CreationDate'', N''CurrentRankDescription'', N''DailyBonus'', N''EnrollmentQualifiedTeamMembers'', N''EnrollmentTeam'', N''ExternalMembers'', N''LastUpdateBy'', N''LastUpdateDate'', N''LevelNo'', N''LifetimeHoldingDuration'', N''MaxEnrollmentTeamPointsPerBranch'', N''MaxTeamPointsPerBranch'', N''MonthlyBonus'', N''PersonalPoints'', N''PlacementQualifiedTeamMembers'', N''RankBonus'', N''RankDefinitionId'', N''RankDescription'', N''SalesVolume'', N''SponsoredMembers'', N''TeamPoints'') AND [object_id] = OBJECT_ID(N''[RankRequirements]''))
        SET IDENTITY_INSERT [RankRequirements] ON;
    EXEC(N''INSERT INTO [RankRequirements] ([Id], [AchievementMessage], [CertificateUrl], [CreatedBy], [CreationDate], [CurrentRankDescription], [DailyBonus], [EnrollmentQualifiedTeamMembers], [EnrollmentTeam], [ExternalMembers], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifetimeHoldingDuration], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MonthlyBonus], [PersonalPoints], [PlacementQualifiedTeamMembers], [RankBonus], [RankDefinitionId], [RankDescription], [SalesVolume], [SponsoredMembers], [TeamPoints])
    VALUES (1, N''''Congratulations! You have reached Silver rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Silver Ambassador. Earn $4/day in Dual Team Residuals.'''', 4.0, 0, 3, 1, NULL, NULL, 1, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 100.0, 1, N''''Qualify with 18 Enrollment Team points (3 Elite/Turbo members, max 50% per branch).'''', 0.0, 1, 18),
    (2, N''''Congratulations! You have reached Gold rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Gold Ambassador. Earn $10/day in Dual Team Residuals.'''', 10.0, 0, 12, 1, NULL, NULL, 2, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 300.0, 2, N''''Qualify with 72 Enrollment Team points (12 Elite/Turbo members, max 50% per branch).'''', 0.0, 1, 72),
    (3, N''''Congratulations! You have reached Platinum rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Platinum Ambassador. Earn $15/day in Dual Team Residuals.'''', 15.0, 0, 29, 1, NULL, NULL, 3, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 500.0, 3, N''''Qualify with 175 Enrollment Team points (max 50% per branch). Boost Bonus unlocked.'''', 0.0, 2, 175),
    (4, N''''Congratulations! You have reached Titanium rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Titanium Ambassador. Earn $25/day in Dual Team Residuals.'''', 25.0, 0, 0, 1, NULL, NULL, 4, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 1000.0, 4, N''''Qualify with 350 Dual Team points (max 50% per branch).'''', 0.0, 2, 350),
    (5, N''''Congratulations! You have reached Jade rank and unlocked the Presidential Bonus!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Jade Ambassador. Earn $40/day in Dual Team Residuals.'''', 40.0, 0, 0, 1, NULL, NULL, 5, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 2500.0, 5, N''''Qualify with 700 Dual Team points (max 50% per branch). Presidential Bonus unlocked.'''', 0.0, 3, 700),
    (6, N''''Congratulations! You have reached Pearl rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Pearl Ambassador. Earn $80/day in Dual Team Residuals.'''', 80.0, 0, 0, 1, NULL, NULL, 6, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 5000.0, 6, N''''Qualify with 1,500 Dual Team points (max 50% per branch).'''', 0.0, 3, 1500),
    (7, N''''Congratulations! You have reached Emerald rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are an Emerald Ambassador. Earn $150/day in Dual Team Residuals.'''', 150.0, 0, 0, 1, NULL, NULL, 7, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 10000.0, 7, N''''Qualify with 3,000 Dual Team points (max 50% per branch).'''', 0.0, 4, 3000),
    (8, N''''Congratulations! You have reached Ruby rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Ruby Ambassador. Earn $300/day in Dual Team Residuals.'''', 300.0, 0, 0, 1, NULL, NULL, 8, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 25000.0, 8, N''''Qualify with 6,000 Dual Team points (max 50% per branch).'''', 0.0, 5, 6000),
    (9, N''''Congratulations! You have reached Sapphire rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Sapphire Ambassador. Earn $500/day in Dual Team Residuals.'''', 500.0, 0, 0, 1, NULL, NULL, 9, 0, 0.5E0, 0.5E0, 0.0, 1, 0, 50000.0, 9, N''''Qualify with 10,000 Dual Team points (max 50% per branch).'''', 0.0, 5, 10000),
    (10, N''''Congratulations! You have reached Diamond rank and unlocked the Car Bonus!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Diamond Ambassador. Earn $750/day in Dual Team Residuals.'''', 750.0, 0, 0, 1, NULL, NULL, 10, 0, 0.5E0, 0.5E0, 500.0, 1, 0, 100000.0, 10, N''''Qualify with 15,000 Dual Team points (max 50% per branch). Car Bonus unlocked.'''', 0.0, 6, 15000),
    (11, N''''Congratulations! You have reached Double Diamond rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Double Diamond Ambassador. Earn $1,000/day in Dual Team Residuals.'''', 1000.0, 0, 0, 1, NULL, NULL, 11, 0, 0.5E0, 0.5E0, 750.0, 1, 0, 150000.0, 11, N''''Qualify with 20,000 Dual Team points (max 50% per branch).'''', 0.0, 6, 20000),
    (12, N''''Congratulations! You have reached Triple Diamond rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Triple Diamond Ambassador. Earn $1,500/day in Dual Team Residuals.'''', 1500.0, 0, 0, 1, NULL, NULL, 12, 0, 0.5E0, 0.5E0, 1000.0, 1, 0, 200000.0, 12, N''''Qualify with 30,000 Dual Team points (max 50% per branch).'''', 0.0, 7, 30000),
    (13, N''''Congratulations! You have reached Blue Diamond rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Blue Diamond Ambassador. Earn $2,000/day in Dual Team Residuals.'''', 2000.0, 0, 0, 1, NULL, NULL, 13, 0, 0.5E0, 0.5E0, 1500.0, 1, 0, 300000.0, 13, N''''Qualify with 60,000 Dual Team points (max 50% per branch).'''', 0.0, 8, 60000),
    (14, N''''Congratulations! You have reached Black Diamond rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Black Diamond Ambassador. Earn $3,000/day in Dual Team Residuals.'''', 3000.0, 0, 0, 1, NULL, NULL, 14, 0, 0.5E0, 0.5E0, 2500.0, 1, 0, 500000.0, 14, N''''Qualify with 120,000 Dual Team points (max 50% per branch).'''', 0.0, 10, 120000),
    (15, N''''Congratulations! You have reached Royal rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Royal Ambassador. Earn $4,000/day in Dual Team Residuals.'''', 4000.0, 0, 0, 1, NULL, NULL, 15, 0, 0.5E0, 0.5E0, 4000.0, 1, 0, 750000.0, 15, N''''Qualify with 200,000 Dual Team points (max 50% per branch).'''', 0.0, 12, 200000),
    (16, N''''Congratulations! You have reached Double Royal rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Double Royal Ambassador. Earn $5,000/day in Dual Team Residuals.'''', 5000.0, 0, 0, 1, NULL, NULL, 16, 0, 0.5E0, 0.5E0, 5000.0, 1, 0, 1000000.0, 16, N''''Qualify with 300,000 Dual Team points (max 50% per branch).'''', 0.0, 15, 300000),
    (17, N''''Congratulations! You have reached Triple Royal rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Triple Royal Ambassador. Earn $7,500/day in Dual Team Residuals.'''', 7500.0, 0, 0, 1, NULL, NULL, 17, 0, 0.5E0, 0.5E0, 7500.0, 1, 0, 1500000.0, 17, N''''Qualify with 400,000 Dual Team points (max 50% per branch).'''', 0.0, 20, 400000),
    (18, N''''Congratulations! You have reached Blue Royal rank!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Blue Royal Ambassador. Earn $10,000/day in Dual Team Residuals.'''', 10000.0, 0, 0, 1, NULL, NULL, 18, 0, 0.5E0, 0.5E0, 10000.0, 1, 0, 2000000.0, 18, N''''Qualify with 500,000 Dual Team points (max 50% per branch).'''', 0.0, 25, 500000),
    (19, N''''Congratulations! You have reached Black Royal — the highest rank in the company!'''', NULL, N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', N''''You are a Black Royal Ambassador. Earn $15,000/day in Dual Team Residuals.'''', 15000.0, 0, 0, 1, NULL, NULL, 19, 0, 0.5E0, 0.5E0, 15000.0, 1, 0, 3000000.0, 19, N''''Qualify with 700,000 Dual Team points (max 50% per branch). The pinnacle of the Ambassador journey.'''', 0.0, 30, 700000)'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''AchievementMessage'', N''CertificateUrl'', N''CreatedBy'', N''CreationDate'', N''CurrentRankDescription'', N''DailyBonus'', N''EnrollmentQualifiedTeamMembers'', N''EnrollmentTeam'', N''ExternalMembers'', N''LastUpdateBy'', N''LastUpdateDate'', N''LevelNo'', N''LifetimeHoldingDuration'', N''MaxEnrollmentTeamPointsPerBranch'', N''MaxTeamPointsPerBranch'', N''MonthlyBonus'', N''PersonalPoints'', N''PlacementQualifiedTeamMembers'', N''RankBonus'', N''RankDefinitionId'', N''RankDescription'', N''SalesVolume'', N''SponsoredMembers'', N''TeamPoints'') AND [object_id] = OBJECT_ID(N''[RankRequirements]''))
        SET IDENTITY_INSERT [RankRequirements] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissions')
        EXEC(N'IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CarBonusEligible'', N''CreatedBy'', N''CreationDate'', N''EligibleDailyResidual'', N''EligibleMembershipResidual'', N''LastUpdateBy'', N''LastUpdateDate'', N''PresidentialBonusEligible'', N''ProductId'', N''TriggerBoostBonus'', N''TriggerBuilderBonus'', N''TriggerBuilderBonusTurbo'', N''TriggerFastStartBonus'', N''TriggerSponsorBonus'', N''TriggerSponsorBonusTurbo'') AND [object_id] = OBJECT_ID(N''[ProductCommissions]''))
        SET IDENTITY_INSERT [ProductCommissions] ON;
    EXEC(N''INSERT INTO [ProductCommissions] ([Id], [CarBonusEligible], [CreatedBy], [CreationDate], [EligibleDailyResidual], [EligibleMembershipResidual], [LastUpdateBy], [LastUpdateDate], [PresidentialBonusEligible], [ProductId], [TriggerBoostBonus], [TriggerBuilderBonus], [TriggerBuilderBonusTurbo], [TriggerFastStartBonus], [TriggerSponsorBonus], [TriggerSponsorBonusTurbo])
    VALUES (1, CAST(1 AS bit), N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''''00000002-prod-0000-0000-000000000002'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (2, CAST(1 AS bit), N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''''00000003-prod-0000-0000-000000000003'''', CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit)),
    (3, CAST(1 AS bit), N''''seed'''', ''''2026-03-16T00:00:00.0000000Z'''', CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, CAST(1 AS bit), N''''00000004-prod-0000-0000-000000000004'''', CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit))'');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N''Id'', N''CarBonusEligible'', N''CreatedBy'', N''CreationDate'', N''EligibleDailyResidual'', N''EligibleMembershipResidual'', N''LastUpdateBy'', N''LastUpdateDate'', N''PresidentialBonusEligible'', N''ProductId'', N''TriggerBoostBonus'', N''TriggerBuilderBonus'', N''TriggerBuilderBonusTurbo'', N''TriggerFastStartBonus'', N''TriggerSponsorBonus'', N''TriggerSponsorBonusTurbo'') AND [object_id] = OBJECT_ID(N''[ProductCommissions]''))
        SET IDENTITY_INSERT [ProductCommissions] OFF;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetRoleClaims')
        EXEC(N'CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetRoles')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserClaims')
        EXEC(N'CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserLogins')
        EXEC(N'CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUserRoles')
        EXEC(N'CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUsers')
        EXEC(N'CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'AspNetUsers')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CannedResponses')
        EXEC(N'CREATE INDEX [IX_CannedResponses_TeamId] ON [CannedResponses] ([TeamId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDownHistories')
        EXEC(N'CREATE INDEX [IX_CommissionCountDownHistories_CountDownId] ON [CommissionCountDownHistories] ([CountDownId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDownHistories')
        EXEC(N'CREATE INDEX [IX_CommissionCountDownHistories_MemberId1] ON [CommissionCountDownHistories] ([MemberId1]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDowns')
        EXEC(N'CREATE INDEX [IX_CommissionCountDowns_MemberId1] ON [CommissionCountDowns] ([MemberId1]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'CREATE INDEX [IX_CommissionEarnings_BeneficiaryMemberId_Status] ON [CommissionEarnings] ([BeneficiaryMemberId], [Status]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'CREATE INDEX [IX_CommissionEarnings_CommissionOperationTypeId] ON [CommissionEarnings] ([CommissionOperationTypeId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'CREATE INDEX [IX_CommissionEarnings_CommissionTypeId] ON [CommissionEarnings] ([CommissionTypeId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'CREATE INDEX [IX_CommissionEarnings_PeriodDate] ON [CommissionEarnings] ([PeriodDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionEarnings')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_CommissionEarnings_SourceOrderId_CommissionTypeId] ON [CommissionEarnings] ([SourceOrderId], [CommissionTypeId]) WHERE [SourceOrderId] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionTypes')
        EXEC(N'CREATE INDEX [IX_CommissionTypes_CommissionCategoryId] ON [CommissionTypes] ([CommissionCategoryId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'DualTeamTree')
        EXEC(N'CREATE INDEX [IX_DualTeamTree_HierarchyPath] ON [DualTeamTree] ([HierarchyPath]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'DualTeamTree')
        EXEC(N'CREATE INDEX [IX_DualTeamTree_MemberId] ON [DualTeamTree] ([MemberId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ErrorMessages')
        EXEC(N'CREATE UNIQUE INDEX [IX_ErrorMessages_ErrorCode_Language] ON [ErrorMessages] ([ErrorCode], [Language]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GenealogyTree')
        EXEC(N'CREATE INDEX [IX_GenealogyTree_HierarchyPath] ON [GenealogyTree] ([HierarchyPath]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GenealogyTree')
        EXEC(N'CREATE INDEX [IX_GenealogyTree_MemberId_CreationDate] ON [GenealogyTree] ([MemberId], [CreationDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GhostPoints')
        EXEC(N'CREATE INDEX [IX_GhostPoints_LegMemberId] ON [GhostPoints] ([LegMemberId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'GhostPoints')
        EXEC(N'CREATE INDEX [IX_GhostPoints_MemberId] ON [GhostPoints] ([MemberId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticles')
        EXEC(N'CREATE INDEX [IX_KbArticles_CategoryId] ON [KbArticles] ([CategoryId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticles')
        EXEC(N'CREATE INDEX [IX_KbArticles_HelpfulCount] ON [KbArticles] ([HelpfulCount]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticles')
        EXEC(N'CREATE UNIQUE INDEX [IX_KbArticles_Slug] ON [KbArticles] ([Slug]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticles')
        EXEC(N'CREATE INDEX [IX_KbArticles_Visibility_CategoryId] ON [KbArticles] ([Visibility], [CategoryId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'KbArticleVersions')
        EXEC(N'CREATE INDEX [IX_KbArticleVersions_ArticleId] ON [KbArticleVersions] ([ArticleId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'LoyaltyPoints')
        EXEC(N'CREATE INDEX [IX_LoyaltyPoints_MemberProfileId] ON [LoyaltyPoints] ([MemberProfileId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberNotifications')
        EXEC(N'CREATE INDEX [IX_MemberNotifications_MemberProfileId] ON [MemberNotifications] ([MemberProfileId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_MemberProfiles_ActiveMembershipId] ON [MemberProfiles] ([ActiveMembershipId]) WHERE [ActiveMembershipId] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_MemberProfiles_BinaryNodeId] ON [MemberProfiles] ([BinaryNodeId]) WHERE [BinaryNodeId] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_MemberProfiles_EnrollmentNodeId] ON [MemberProfiles] ([EnrollmentNodeId]) WHERE [EnrollmentNodeId] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_MemberId] ON [MemberProfiles] ([MemberId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'EXEC(N''CREATE UNIQUE INDEX [IX_MemberProfiles_ReplicateSiteSlug] ON [MemberProfiles] ([ReplicateSiteSlug]) WHERE [ReplicateSiteSlug] IS NOT NULL'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'CREATE UNIQUE INDEX [IX_MemberProfiles_UserId] ON [MemberProfiles] ([UserId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberRankHistories')
        EXEC(N'CREATE INDEX [IX_MemberRankHistories_RankDefinitionId] ON [MemberRankHistories] ([RankDefinitionId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipLevelBenefits')
        EXEC(N'CREATE INDEX [IX_MembershipLevelBenefits_MembershipLevelId] ON [MembershipLevelBenefits] ([MembershipLevelId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipSubscriptions')
        EXEC(N'CREATE INDEX [IX_MembershipSubscriptions_LastOrderId1] ON [MembershipSubscriptions] ([LastOrderId1]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipSubscriptions')
        EXEC(N'CREATE INDEX [IX_MembershipSubscriptions_MemberId] ON [MembershipSubscriptions] ([MemberId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MembershipSubscriptions')
        EXEC(N'CREATE INDEX [IX_MembershipSubscriptions_MembershipLevelId] ON [MembershipSubscriptions] ([MembershipLevelId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberStatusHistories')
        EXEC(N'CREATE INDEX [IX_MemberStatusHistories_MemberProfileId] ON [MemberStatusHistories] ([MemberProfileId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'OrderDetails')
        EXEC(N'CREATE INDEX [IX_OrderDetails_OrdersId] ON [OrderDetails] ([OrdersId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'PaymentHistories')
        EXEC(N'CREATE INDEX [IX_PaymentHistories_OrdersId] ON [PaymentHistories] ([OrdersId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissionPromos')
        EXEC(N'CREATE INDEX [IX_ProductCommissionPromos_CorporatePromoId1] ON [ProductCommissionPromos] ([CorporatePromoId1]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissionPromos')
        EXEC(N'CREATE INDEX [IX_ProductCommissionPromos_ProductId] ON [ProductCommissionPromos] ([ProductId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductCommissions')
        EXEC(N'CREATE UNIQUE INDEX [IX_ProductCommissions_ProductId] ON [ProductCommissions] ([ProductId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'ProductLoyaltySettings')
        EXEC(N'CREATE UNIQUE INDEX [IX_ProductLoyaltySettings_ProductId] ON [ProductLoyaltySettings] ([ProductId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Products')
        EXEC(N'CREATE INDEX [IX_Products_MembershipLevelId] ON [Products] ([MembershipLevelId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'RankRequirements')
        EXEC(N'CREATE INDEX [IX_RankRequirements_RankDefinitionId] ON [RankRequirements] ([RankDefinitionId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SlaBreaches')
        EXEC(N'CREATE INDEX [IX_SlaBreaches_BreachedAt] ON [SlaBreaches] ([BreachedAt]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SlaBreaches')
        EXEC(N'CREATE INDEX [IX_SlaBreaches_TicketId] ON [SlaBreaches] ([TicketId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SupportAgents')
        EXEC(N'CREATE INDEX [IX_SupportAgents_TeamId] ON [SupportAgents] ([TeamId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'SupportAgents')
        EXEC(N'CREATE UNIQUE INDEX [IX_SupportAgents_UserId] ON [SupportAgents] ([UserId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketAttachments')
        EXEC(N'CREATE INDEX [IX_TicketAttachments_TicketId] ON [TicketAttachments] ([TicketId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketCategories')
        EXEC(N'CREATE INDEX [IX_TicketCategories_DefaultSlaPolicyId] ON [TicketCategories] ([DefaultSlaPolicyId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketCategories')
        EXEC(N'CREATE INDEX [IX_TicketCategories_DefaultTeamId] ON [TicketCategories] ([DefaultTeamId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketCategories')
        EXEC(N'CREATE INDEX [IX_TicketCategories_ParentCategoryId] ON [TicketCategories] ([ParentCategoryId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketComments')
        EXEC(N'CREATE INDEX [IX_TicketComments_IsInternal] ON [TicketComments] ([IsInternal]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketComments')
        EXEC(N'CREATE INDEX [IX_TicketComments_TicketId_CreationDate] ON [TicketComments] ([TicketId], [CreationDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketHistories')
        EXEC(N'CREATE INDEX [IX_TicketHistories_TicketId_CreationDate] ON [TicketHistories] ([TicketId], [CreationDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TicketMetrics')
        EXEC(N'CREATE UNIQUE INDEX [IX_TicketMetrics_Date] ON [TicketMetrics] ([Date]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_AssignedTeamId_Status] ON [Tickets] ([AssignedTeamId], [Status]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_AssignedToUserId_Status] ON [Tickets] ([AssignedToUserId], [Status]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_CategoryId] ON [Tickets] ([CategoryId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_MemberId_CreationDate] ON [Tickets] ([MemberId], [CreationDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_SlaPolicyId] ON [Tickets] ([SlaPolicyId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE INDEX [IX_Tickets_Status_LastUpdateDate] ON [Tickets] ([Status], [LastUpdateDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'Tickets')
        EXEC(N'CREATE UNIQUE INDEX [IX_Tickets_TicketNumber] ON [Tickets] ([TicketNumber]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenBalances')
        EXEC(N'CREATE INDEX [IX_TokenBalances_MemberProfileId] ON [TokenBalances] ([MemberProfileId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTransactions')
        EXEC(N'CREATE INDEX [IX_TokenTransactions_MemberId_CreationDate] ON [TokenTransactions] ([MemberId], [CreationDate]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypeCommissions')
        EXEC(N'CREATE UNIQUE INDEX [IX_TokenTypeCommissions_TokenTypeId_CommissionTypeId] ON [TokenTypeCommissions] ([TokenTypeId], [CommissionTypeId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'TokenTypeProducts')
        EXEC(N'CREATE UNIQUE INDEX [IX_TokenTypeProducts_TokenTypeId_ProductId] ON [TokenTypeProducts] ([TokenTypeId], [ProductId]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDownHistories')
        EXEC(N'ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_CommissionCountDowns_CountDownId] FOREIGN KEY ([CountDownId]) REFERENCES [CommissionCountDowns] ([Id]) ON DELETE CASCADE;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDownHistories')
        EXEC(N'ALTER TABLE [CommissionCountDownHistories] ADD CONSTRAINT [FK_CommissionCountDownHistories_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'CommissionCountDowns')
        EXEC(N'ALTER TABLE [CommissionCountDowns] ADD CONSTRAINT [FK_CommissionCountDowns_MemberProfiles_MemberId1] FOREIGN KEY ([MemberId1]) REFERENCES [MemberProfiles] ([Id]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'LoyaltyPoints')
        EXEC(N'ALTER TABLE [LoyaltyPoints] ADD CONSTRAINT [FK_LoyaltyPoints_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberNotifications')
        EXEC(N'ALTER TABLE [MemberNotifications] ADD CONSTRAINT [FK_MemberNotifications_MemberProfiles_MemberProfileId] FOREIGN KEY ([MemberProfileId]) REFERENCES [MemberProfiles] ([Id]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N'MemberProfiles')
        EXEC(N'ALTER TABLE [MemberProfiles] ADD CONSTRAINT [FK_MemberProfiles_MembershipSubscriptions_ActiveMembershipId] FOREIGN KEY ([ActiveMembershipId]) REFERENCES [MembershipSubscriptions] ([Id]);
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM dbo.[__PlanQ_TablasPrevias] WHERE Nombre = N''TicketCategories'')
    BEGIN
        IF COL_LENGTH(N''dbo.TicketCategories'', N''Description'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [Description] nvarchar(500) NULL;'');
        IF COL_LENGTH(N''dbo.TicketCategories'', N''DefaultPriority'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [DefaultPriority] nvarchar(20) NOT NULL DEFAULT ''''Normal'''';'');
        IF COL_LENGTH(N''dbo.TicketCategories'', N''DefaultSlaPolicyId'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [DefaultSlaPolicyId] nvarchar(36) NULL;'');
        IF COL_LENGTH(N''dbo.TicketCategories'', N''DefaultTeamId'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [DefaultTeamId] int NULL;'');
        IF COL_LENGTH(N''dbo.TicketCategories'', N''ParentCategoryId'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [ParentCategoryId] int NULL;'');
        IF COL_LENGTH(N''dbo.TicketCategories'', N''SortOrder'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD [SortOrder] int NOT NULL DEFAULT 0;'');

        IF OBJECT_ID(N''dbo.FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId'', N''F'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SlaPolicies_DefaultSlaPolicyId]
                  FOREIGN KEY ([DefaultSlaPolicyId]) REFERENCES [SlaPolicies] ([Id]) ON DELETE SET NULL;'');
        IF OBJECT_ID(N''dbo.FK_TicketCategories_SupportTeams_DefaultTeamId'', N''F'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_SupportTeams_DefaultTeamId]
                  FOREIGN KEY ([DefaultTeamId]) REFERENCES [SupportTeams] ([Id]) ON DELETE SET NULL;'');
        IF OBJECT_ID(N''dbo.FK_TicketCategories_TicketCategories_ParentCategoryId'', N''F'') IS NULL
            EXEC(N''ALTER TABLE [TicketCategories] ADD CONSTRAINT [FK_TicketCategories_TicketCategories_ParentCategoryId]
                  FOREIGN KEY ([ParentCategoryId]) REFERENCES [TicketCategories] ([Id]);'');

        IF INDEXPROPERTY(OBJECT_ID(N''dbo.TicketCategories''), N''IX_TicketCategories_DefaultSlaPolicyId'', ''IndexID'') IS NULL
            EXEC(N''CREATE INDEX [IX_TicketCategories_DefaultSlaPolicyId] ON [TicketCategories] ([DefaultSlaPolicyId]);'');
        IF INDEXPROPERTY(OBJECT_ID(N''dbo.TicketCategories''), N''IX_TicketCategories_DefaultTeamId'', ''IndexID'') IS NULL
            EXEC(N''CREATE INDEX [IX_TicketCategories_DefaultTeamId] ON [TicketCategories] ([DefaultTeamId]);'');
        IF INDEXPROPERTY(OBJECT_ID(N''dbo.TicketCategories''), N''IX_TicketCategories_ParentCategoryId'', ''IndexID'') IS NULL
            EXEC(N''CREATE INDEX [IX_TicketCategories_ParentCategoryId] ON [TicketCategories] ([ParentCategoryId]);'');
    END;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    EXEC(N'DROP TABLE IF EXISTS dbo.__PlanQ_TablasPrevias;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260405130705_HelpdeskModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260405130705_HelpdeskModule', N'10.0.11');
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
    VALUES (N'20260406183238_AddProductThemeClass', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411013728_AddEmailTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411013728_AddEmailTemplates', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [Countries] (
        [Id] int NOT NULL IDENTITY,
        [Iso2] nchar(2) NOT NULL,
        [Iso3] nchar(3) NOT NULL,
        [NameEn] nvarchar(100) NOT NULL,
        [NameNative] nvarchar(100) NOT NULL,
        [DefaultLanguageCode] nvarchar(10) NOT NULL,
        [FlagEmoji] nvarchar(10) NOT NULL,
        [PhoneCode] nvarchar(10) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Countries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_EmailTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplateLocalizations] (
        [Id] int NOT NULL IDENTITY,
        [EmailTemplateId] int NOT NULL,
        [LanguageCode] nvarchar(10) NOT NULL,
        [Subject] nvarchar(500) NOT NULL,
        [HtmlBody] nvarchar(max) NOT NULL,
        [TextBody] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_EmailTemplateLocalizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailTemplateLocalizations_EmailTemplates_EmailTemplateId] FOREIGN KEY ([EmailTemplateId]) REFERENCES [EmailTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    CREATE TABLE [EmailTemplateVariables] (
        [Id] int NOT NULL IDENTITY,
        [EmailTemplateId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsRequired] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_EmailTemplateVariables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailTemplateVariables_EmailTemplates_EmailTemplateId] FOREIGN KEY ([EmailTemplateId]) REFERENCES [EmailTemplates] ([Id]) ON DELETE CASCADE
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
    CREATE UNIQUE INDEX [IX_EmailTemplateLocalizations_EmailTemplateId_LanguageCode] ON [EmailTemplateLocalizations] ([EmailTemplateId], [LanguageCode]);
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
    CREATE UNIQUE INDEX [IX_EmailTemplateVariables_EmailTemplateId_Name] ON [EmailTemplateVariables] ([EmailTemplateId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411014909_AddCountries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411014909_AddCountries', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE TABLE [CountryProducts] (
        [Id] int NOT NULL IDENTITY,
        [CountryId] int NOT NULL,
        [ProductId] nvarchar(450) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CountryProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CountryProducts_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CountryProducts_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_CountryId] ON [CountryProducts] ([CountryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CountryProducts_CountryId_ProductId] ON [CountryProducts] ([CountryId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_IsActive] ON [CountryProducts] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    CREATE INDEX [IX_CountryProducts_ProductId] ON [CountryProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411020839_AddCountryProductMapping'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411020839_AddCountryProductMapping', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411022418_AddRankEvaluationQueue'
)
BEGIN
    CREATE TABLE [RankEvaluationQueue] (
        [Id] bigint NOT NULL IDENTITY,
        [TriggerMemberId] nvarchar(50) NOT NULL,
        [EvaluateMemberId] nvarchar(50) NOT NULL,
        [TriggerEvent] int NOT NULL,
        [TriggerDate] datetime2 NOT NULL,
        [IsProcessed] bit NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [ProcessedBy] nvarchar(200) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [RetryCount] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RankEvaluationQueue] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411022418_AddRankEvaluationQueue'
)
BEGIN
    CREATE INDEX [IX_RankEvaluationQueue_EvaluateMemberId] ON [RankEvaluationQueue] ([EvaluateMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411022418_AddRankEvaluationQueue'
)
BEGIN
    CREATE INDEX [IX_RankEvaluationQueue_IsProcessed_TriggerDate] ON [RankEvaluationQueue] ([IsProcessed], [TriggerDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411022418_AddRankEvaluationQueue'
)
BEGIN
    CREATE INDEX [IX_RankEvaluationQueue_TriggerMemberId] ON [RankEvaluationQueue] ([TriggerMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411022418_AddRankEvaluationQueue'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411022418_AddRankEvaluationQueue', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411023856_AddCompanyInfo'
)
BEGIN
    CREATE TABLE [CompanyInfo] (
        [Id] int NOT NULL IDENTITY,
        [CompanyName] nvarchar(200) NOT NULL,
        [CompanyLegalId] nvarchar(100) NULL,
        [Address] nvarchar(500) NULL,
        [Phone] nvarchar(50) NULL,
        [SupportEmail] nvarchar(200) NOT NULL,
        [PresidentName] nvarchar(200) NULL,
        [WebsiteUrl] nvarchar(500) NULL,
        [LogoUrl] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CompanyInfo] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260411023856_AddCompanyInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260411023856_AddCompanyInfo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416145120_SetVipJoinPageMembership'
)
BEGIN
    EXEC(N'UPDATE [Products] SET [JoinPageMembership] = CAST(1 AS bit)
    WHERE [Id] = N''00000002-prod-0000-0000-000000000002'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416145120_SetVipJoinPageMembership'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416145120_SetVipJoinPageMembership', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    ALTER TABLE [Countries] ADD [RegionId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE TABLE [Regions] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsActive] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_Regions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE TABLE [RegionGateways] (
        [Id] int NOT NULL IDENTITY,
        [RegionId] int NOT NULL,
        [GatewayType] int NOT NULL,
        [Priority] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_RegionGateways] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RegionGateways_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE INDEX [IX_Countries_RegionId] ON [Countries] ([RegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE INDEX [IX_RegionGateways_IsActive] ON [RegionGateways] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE INDEX [IX_RegionGateways_RegionId] ON [RegionGateways] ([RegionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RegionGateways_RegionId_GatewayType] ON [RegionGateways] ([RegionId], [GatewayType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Regions_Code] ON [Regions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    CREATE INDEX [IX_Regions_IsActive] ON [Regions] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    ALTER TABLE [Countries] ADD CONSTRAINT [FK_Countries_Regions_RegionId] FOREIGN KEY ([RegionId]) REFERENCES [Regions] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416192601_AddRegionsAndRegionGateways'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416192601_AddRegionsAndRegionGateways', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    INSERT INTO Regions (Name, Code, Description, IsActive, SortOrder, CreatedBy, CreationDate)
                    VALUES
                    (''North America'',        ''NA'',   ''United States and Canada'',                                       1, 1, ''system'', GETUTCDATE()),
                    (''Latin America'',        ''LATAM'',''Mexico, Central America, South America and Caribbean'',           1, 2, ''system'', GETUTCDATE()),
                    (''Europe'',               ''EU'',   ''European countries including Russia and Turkey'',                 1, 3, ''system'', GETUTCDATE()),
                    (''Asia Pacific'',         ''APAC'', ''Asia and Pacific countries including Australia and New Zealand'', 1, 4, ''system'', GETUTCDATE()),
                    (''Middle East & Africa'', ''MEA'',  ''Middle Eastern and African countries'',                          1, 5, ''system'', GETUTCDATE());
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    UPDATE Countries
                    SET RegionId = (SELECT Id FROM Regions WHERE Code = ''NA'')
                    WHERE Iso2 IN (''US'',''CA'');
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    UPDATE Countries
                    SET RegionId = (SELECT Id FROM Regions WHERE Code = ''LATAM'')
                    WHERE Iso2 IN (
                        ''MX'',''GT'',''BZ'',''HN'',''SV'',''NI'',''CR'',''PA'',
                        ''CU'',''JM'',''HT'',''DO'',''TT'',''BB'',''LC'',''VC'',''GD'',''AG'',''DM'',''KN'',''BS'',
                        ''CO'',''VE'',''GY'',''SR'',''BR'',''EC'',''PE'',''BO'',''PY'',''CL'',''AR'',''UY''
                    );
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    UPDATE Countries
                    SET RegionId = (SELECT Id FROM Regions WHERE Code = ''EU'')
                    WHERE Iso2 IN (
                        ''AD'',''AL'',''AM'',''AT'',''AZ'',''BA'',''BE'',''BG'',''BY'',''CH'',
                        ''CY'',''CZ'',''DE'',''DK'',''EE'',''ES'',''FI'',''FR'',''GB'',''GE'',
                        ''GR'',''HR'',''HU'',''IE'',''IS'',''IT'',''LI'',''LT'',''LU'',''LV'',
                        ''MC'',''MD'',''ME'',''MT'',''NL'',''NO'',''PL'',''PT'',''RO'',''RS'',
                        ''RU'',''SE'',''SI'',''SK'',''SM'',''TR'',''UA''
                    );
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    UPDATE Countries
                    SET RegionId = (SELECT Id FROM Regions WHERE Code = ''APAC'')
                    WHERE Iso2 IN (
                        ''CN'',''JP'',''TW'',''MN'',
                        ''ID'',''MY'',''PH'',''SG'',''TH'',''VN'',''MM'',''LA'',''KH'',''BN'',''TL'',
                        ''IN'',''PK'',''BD'',''LK'',''NP'',''BT'',''MV'',
                        ''KZ'',''KG'',''TJ'',''TM'',''UZ'',
                        ''AU'',''NZ'',''FJ'',''PG'',''SB'',''VU'',''TO'',''WS'',''KI'',''MH'',''FM'',''PW'',''NR'',''TV'',
                        ''SC''
                    );
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    EXEC(N'
                    UPDATE Countries
                    SET RegionId = (SELECT Id FROM Regions WHERE Code = ''MEA'')
                    WHERE Iso2 IN (
                        ''AE'',''AF'',''BH'',''IQ'',''IR'',''IL'',''JO'',''KW'',''LB'',''OM'',''QA'',''SA'',''SY'',''YE'',
                        ''DZ'',''EG'',''LY'',''MA'',''TN'',
                        ''BF'',''BJ'',''CV'',''GA'',''GM'',''GH'',''GN'',''GW'',''LR'',''ML'',''MR'',''NE'',''NG'',''SL'',''SN'',''ST'',''TG'',
                        ''AO'',''CD'',''CF'',''CG'',''CM'',''GQ'',''RW'',''BI'',
                        ''DJ'',''ER'',''ET'',''KE'',''KM'',''MG'',''MU'',''MW'',''MZ'',''SO'',''SS'',''TZ'',''UG'',
                        ''BW'',''LS'',''NA'',''SZ'',''ZA'',''ZM'',''ZW'',''TD'',''SD''
                    );
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416215558_SeedRegionsAndAssignCountries'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416215558_SeedRegionsAndAssignCountries', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
                    ALTER TABLE Tickets
                    ADD
                        TicketNumber             nvarchar(20)    NOT NULL CONSTRAINT DF_Tickets_TicketNumber             DEFAULT (''''),
                        Channel                  int             NOT NULL CONSTRAINT DF_Tickets_Channel                  DEFAULT (0),
                        EscalationLevel          int             NOT NULL CONSTRAINT DF_Tickets_EscalationLevel          DEFAULT (0),
                        IsSlaFirstResponseBreached bit           NOT NULL CONSTRAINT DF_Tickets_IsSlaFRBreached           DEFAULT (0),
                        IsSlaResolutionBreached  bit             NOT NULL CONSTRAINT DF_Tickets_IsSlaResBreached          DEFAULT (0),
                        IsSlaPaused              bit             NOT NULL CONSTRAINT DF_Tickets_IsSlaPaused               DEFAULT (0),
                        SlaPausedMinutes         float           NOT NULL CONSTRAINT DF_Tickets_SlaPausedMinutes          DEFAULT (0),
                        FollowUpSent             bit             NOT NULL CONSTRAINT DF_Tickets_FollowUpSent              DEFAULT (0);
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
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
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE name = ''FK_Tickets_SlaPolicies_SlaPolicyId'')
                    BEGIN
                        ALTER TABLE Tickets
                        ADD CONSTRAINT FK_Tickets_SlaPolicies_SlaPolicyId
                            FOREIGN KEY (SlaPolicyId) REFERENCES SlaPolicies(Id)
                            ON DELETE SET NULL;
                    END
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE name = ''FK_Tickets_SupportTeams_AssignedTeamId'')
                    BEGIN
                        ALTER TABLE Tickets
                        ADD CONSTRAINT FK_Tickets_SupportTeams_AssignedTeamId
                            FOREIGN KEY (AssignedTeamId) REFERENCES SupportTeams(Id)
                            ON DELETE SET NULL;
                    END
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE name = ''FK_Tickets_TicketCategories_CategoryId'')
                    BEGIN
                        ALTER TABLE Tickets DROP CONSTRAINT FK_Tickets_TicketCategories_CategoryId;
                    END
                    ALTER TABLE Tickets
                    ADD CONSTRAINT FK_Tickets_TicketCategories_CategoryId
                        FOREIGN KEY (CategoryId) REFERENCES TicketCategories(Id);
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    EXEC(N'
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Tickets_TicketNumber'' AND object_id = OBJECT_ID(''Tickets''))
                        CREATE UNIQUE INDEX IX_Tickets_TicketNumber ON Tickets(TicketNumber);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Tickets_AssignedTeamId_Status'' AND object_id = OBJECT_ID(''Tickets''))
                        CREATE INDEX IX_Tickets_AssignedTeamId_Status ON Tickets(AssignedTeamId, Status);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Tickets_CategoryId_Status'' AND object_id = OBJECT_ID(''Tickets''))
                        CREATE INDEX IX_Tickets_CategoryId_Status ON Tickets(CategoryId, Status);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Tickets_SlaPolicyId'' AND object_id = OBJECT_ID(''Tickets''))
                        CREATE INDEX IX_Tickets_SlaPolicyId ON Tickets(SlaPolicyId);

                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Tickets_Status_CreationDate'' AND object_id = OBJECT_ID(''Tickets''))
                        CREATE INDEX IX_Tickets_Status_CreationDate ON Tickets(Status, CreationDate);
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260416232000_RepairTicketsTableSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260416232000_RepairTicketsTableSchema', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418172951_AddOrderNoToOrders'
)
BEGIN
    ALTER TABLE [Orders] ADD [OrderNo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418172951_AddOrderNoToOrders'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418172951_AddOrderNoToOrders', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419004534_AddFsbExtendedWindow'
)
BEGIN
    ALTER TABLE [CommissionCountDowns] ADD [FastStartBonus1ExtendedEnd] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419004534_AddFsbExtendedWindow'
)
BEGIN
    ALTER TABLE [CommissionCountDowns] ADD [FastStartBonus1ExtendedStart] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419004534_AddFsbExtendedWindow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419004534_AddFsbExtendedWindow', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419193908_AddBuilderBonusPerRankCommissionTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [FixedAmount], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (47, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Silver (1).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 1, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Silver'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (48, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Gold (2).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 2, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Gold'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (49, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Platinum (3).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 3, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Platinum'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (50, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Titanium (4).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 4, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Titanium'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (51, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Jade (5).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 5, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Jade'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (52, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Pearl (6).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 6, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Pearl'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (53, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Emerald (7).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 7, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Emerald'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (54, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Ruby (8).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 8, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Ruby'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (55, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Sapphire (9).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 9, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Sapphire'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (56, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Diamond (10).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 10, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (57, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Double Diamond (11).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 11, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Double Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (58, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Triple Diamond (12).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 12, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Triple Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (59, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Blue Diamond (13).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 13, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Blue Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (60, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Black Diamond (14).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 14, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Black Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (61, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Royal (15).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 15, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (62, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Double Royal (16).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 16, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Double Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (63, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Triple Royal (17).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 17, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Triple Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (64, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Blue Royal (18).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 18, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Blue Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (65, 6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Elite enrollment. Requires lifetime rank Black Royal (19).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 3, 19, 0.5E0, 0.5E0, 0, N''Builder Bonus Elite – Black Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (66, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Silver (1).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 1, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Silver'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (67, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Gold (2).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 2, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Gold'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (68, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Platinum (3).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 3, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Platinum'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (69, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Titanium (4).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 4, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Titanium'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (70, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Jade (5).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 5, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Jade'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (71, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Pearl (6).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 6, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Pearl'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (72, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Emerald (7).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 7, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Emerald'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (73, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Ruby (8).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 8, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Ruby'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (74, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Sapphire (9).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 9, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Sapphire'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (75, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Diamond (10).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 10, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (76, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Double Diamond (11).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 11, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Double Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (77, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Diamond (12).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 12, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Triple Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (78, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Diamond (13).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 13, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Blue Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (79, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Black Diamond (14).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 14, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Black Diamond'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (80, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Royal (15).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 15, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (81, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Double Royal (16).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 16, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Double Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (82, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Triple Royal (17).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 17, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Triple Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (83, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Blue Royal (18).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 18, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Blue Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (84, 7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Builder Bonus for Turbo enrollment. Requires lifetime rank Black Royal (19).'', 0, 0, 0.0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), NULL, NULL, 4, 19, 0.5E0, 0.5E0, 0, N''Builder Bonus Turbo – Black Royal'', 0, 4, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'FixedAmount', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419193908_AddBuilderBonusPerRankCommissionTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419193908_AddBuilderBonusPerRankCommissionTypes', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 10.0
    WHERE [Id] = 47;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 20.0
    WHERE [Id] = 48;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 30.0
    WHERE [Id] = 49;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 40.0
    WHERE [Id] = 50;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 50.0
    WHERE [Id] = 51;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 60.0
    WHERE [Id] = 52;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 65.0
    WHERE [Id] = 53;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 70.0
    WHERE [Id] = 54;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 75.0
    WHERE [Id] = 55;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 80.0
    WHERE [Id] = 56;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 85.0
    WHERE [Id] = 57;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 90.0
    WHERE [Id] = 58;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 95.0
    WHERE [Id] = 59;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 100.0
    WHERE [Id] = 60;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 105.0
    WHERE [Id] = 61;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 110.0
    WHERE [Id] = 62;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 115.0
    WHERE [Id] = 63;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 120.0
    WHERE [Id] = 64;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 125.0
    WHERE [Id] = 65;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 20.0
    WHERE [Id] = 66;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 40.0
    WHERE [Id] = 67;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 60.0
    WHERE [Id] = 68;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 80.0
    WHERE [Id] = 69;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 100.0
    WHERE [Id] = 70;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 120.0
    WHERE [Id] = 71;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 130.0
    WHERE [Id] = 72;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 140.0
    WHERE [Id] = 73;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 150.0
    WHERE [Id] = 74;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 160.0
    WHERE [Id] = 75;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 170.0
    WHERE [Id] = 76;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 180.0
    WHERE [Id] = 77;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 190.0
    WHERE [Id] = 78;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 200.0
    WHERE [Id] = 79;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 210.0
    WHERE [Id] = 80;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 220.0
    WHERE [Id] = 81;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 230.0
    WHERE [Id] = 82;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 240.0
    WHERE [Id] = 83;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 250.0
    WHERE [Id] = 84;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419202345_SeedBuilderBonusAmountsFromCompPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419202345_SeedBuilderBonusAmountsFromCompPlan', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 10.0
    WHERE [Id] = 66;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 20.0
    WHERE [Id] = 67;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 30.0
    WHERE [Id] = 68;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 40.0
    WHERE [Id] = 69;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 50.0
    WHERE [Id] = 70;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 60.0
    WHERE [Id] = 71;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 65.0
    WHERE [Id] = 72;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 70.0
    WHERE [Id] = 73;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 75.0
    WHERE [Id] = 74;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 80.0
    WHERE [Id] = 75;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 85.0
    WHERE [Id] = 76;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 90.0
    WHERE [Id] = 77;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 95.0
    WHERE [Id] = 78;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 100.0
    WHERE [Id] = 79;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 105.0
    WHERE [Id] = 80;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 110.0
    WHERE [Id] = 81;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 115.0
    WHERE [Id] = 82;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 120.0
    WHERE [Id] = 83;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [FixedAmount] = 125.0
    WHERE [Id] = 84;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419213830_FixTurboBonusAmounts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419213830_FixTurboBonusAmounts', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419215456_FixBuilderBonusCrossoverTypes'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Superseded by Cat7 Turbo types (IDs 66-84). Deactivated.'', [IsActive] = CAST(0 AS bit)
    WHERE [Id] = 35;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419215456_FixBuilderBonusCrossoverTypes'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Cat7 only fires for Turbo product (LevelNo=4). Deactivated for VIP.'', [IsActive] = CAST(0 AS bit)
    WHERE [Id] = 36;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419215456_FixBuilderBonusCrossoverTypes'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Cat7 only fires for Turbo product (LevelNo=4). Deactivated for Elite.'', [IsActive] = CAST(0 AS bit)
    WHERE [Id] = 37;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419215456_FixBuilderBonusCrossoverTypes'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''Builder Bonus Turbo base (rank 0 sponsors). Matches Cat6 base amount.'', [FixedAmount] = 60.0
    WHERE [Id] = 38;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419215456_FixBuilderBonusCrossoverTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419215456_FixBuilderBonusCrossoverTypes', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419220746_FixMemberBonusTurboAmount'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Description] = N''One-time $40 bonus paid to the direct enroller for the Turbo portion. Stacks with Member Bonus – Elite ($40) for a combined $80 on Turbo signups.'', [FixedAmount] = 40.0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419220746_FixMemberBonusTurboAmount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419220746_FixMemberBonusTurboAmount', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC sp_rename N'[CommissionTypes].[FixedAmount]', N'AmountPromo', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    ALTER TABLE [CommissionTypes] ADD [Amount] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 20.0, [AmountPromo] = NULL
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 40.0, [AmountPromo] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 40.0, [AmountPromo] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 150.0, [AmountPromo] = NULL
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 150.0, [AmountPromo] = NULL
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 150.0, [AmountPromo] = NULL
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 4.0, [AmountPromo] = NULL
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 10.0, [AmountPromo] = NULL
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 15.0, [AmountPromo] = NULL
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 25.0, [AmountPromo] = NULL
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 40.0, [AmountPromo] = NULL
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 80.0, [AmountPromo] = NULL
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 150.0, [AmountPromo] = NULL
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 300.0, [AmountPromo] = NULL
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 500.0, [AmountPromo] = NULL
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 750.0, [AmountPromo] = NULL
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 1000.0, [AmountPromo] = NULL
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 1500.0, [AmountPromo] = NULL
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 2000.0, [AmountPromo] = NULL
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 3000.0, [AmountPromo] = NULL
    WHERE [Id] = 20;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 4000.0, [AmountPromo] = NULL
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 5000.0, [AmountPromo] = NULL
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 7500.0, [AmountPromo] = NULL
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 10000.0, [AmountPromo] = NULL
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 15000.0, [AmountPromo] = NULL
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 600.0, [AmountPromo] = NULL
    WHERE [Id] = 26;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 1200.0, [AmountPromo] = NULL
    WHERE [Id] = 27;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 28;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 29;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 30;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 31;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 32;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 25.0, [AmountPromo] = NULL
    WHERE [Id] = 33;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 60.0, [AmountPromo] = NULL
    WHERE [Id] = 34;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 120.0, [AmountPromo] = NULL
    WHERE [Id] = 35;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 30.0, [AmountPromo] = NULL
    WHERE [Id] = 36;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 80.0, [AmountPromo] = NULL
    WHERE [Id] = 37;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 60.0, [AmountPromo] = NULL
    WHERE [Id] = 38;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 39;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 1.0, [AmountPromo] = NULL, [Description] = N''Deduction applied in real-time when a member consumes tokens. Unit cost can be overridden per TokenType; Amount here is the platform default.''
    WHERE [Id] = 40;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 41;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 42;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 43;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 44;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 45;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = NULL
    WHERE [Id] = 46;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 10.0, [AmountPromo] = NULL
    WHERE [Id] = 47;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 20.0, [AmountPromo] = NULL
    WHERE [Id] = 48;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 30.0, [AmountPromo] = NULL
    WHERE [Id] = 49;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 40.0, [AmountPromo] = NULL
    WHERE [Id] = 50;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 50.0, [AmountPromo] = NULL
    WHERE [Id] = 51;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 60.0, [AmountPromo] = NULL
    WHERE [Id] = 52;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 65.0, [AmountPromo] = NULL
    WHERE [Id] = 53;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 70.0, [AmountPromo] = NULL
    WHERE [Id] = 54;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 75.0, [AmountPromo] = NULL
    WHERE [Id] = 55;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 80.0, [AmountPromo] = NULL
    WHERE [Id] = 56;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 85.0, [AmountPromo] = NULL
    WHERE [Id] = 57;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 90.0, [AmountPromo] = NULL
    WHERE [Id] = 58;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 95.0, [AmountPromo] = NULL
    WHERE [Id] = 59;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 100.0, [AmountPromo] = NULL
    WHERE [Id] = 60;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 105.0, [AmountPromo] = NULL
    WHERE [Id] = 61;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 110.0, [AmountPromo] = NULL
    WHERE [Id] = 62;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 115.0, [AmountPromo] = NULL
    WHERE [Id] = 63;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 120.0, [AmountPromo] = NULL
    WHERE [Id] = 64;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 125.0, [AmountPromo] = NULL
    WHERE [Id] = 65;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 10.0, [AmountPromo] = NULL
    WHERE [Id] = 66;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 20.0, [AmountPromo] = NULL
    WHERE [Id] = 67;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 30.0, [AmountPromo] = NULL
    WHERE [Id] = 68;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 40.0, [AmountPromo] = NULL
    WHERE [Id] = 69;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 50.0, [AmountPromo] = NULL
    WHERE [Id] = 70;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 60.0, [AmountPromo] = NULL
    WHERE [Id] = 71;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 65.0, [AmountPromo] = NULL
    WHERE [Id] = 72;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 70.0, [AmountPromo] = NULL
    WHERE [Id] = 73;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 75.0, [AmountPromo] = NULL
    WHERE [Id] = 74;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 80.0, [AmountPromo] = NULL
    WHERE [Id] = 75;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 85.0, [AmountPromo] = NULL
    WHERE [Id] = 76;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 90.0, [AmountPromo] = NULL
    WHERE [Id] = 77;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 95.0, [AmountPromo] = NULL
    WHERE [Id] = 78;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 100.0, [AmountPromo] = NULL
    WHERE [Id] = 79;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 105.0, [AmountPromo] = NULL
    WHERE [Id] = 80;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 110.0, [AmountPromo] = NULL
    WHERE [Id] = 81;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 115.0, [AmountPromo] = NULL
    WHERE [Id] = 82;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 120.0, [AmountPromo] = NULL
    WHERE [Id] = 83;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    EXEC(N'UPDATE [CommissionTypes] SET [Amount] = 125.0, [AmountPromo] = NULL
    WHERE [Id] = 84;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260419221552_RenameFixedAmountToAmountAddAmountPromo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260419221552_RenameFixedAmountToAmountAddAmountPromo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420182940_AddSsnEncryptedAndCountryHasStates'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [SsnEncrypted] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420182940_AddSsnEncryptedAndCountryHasStates'
)
BEGIN
    ALTER TABLE [Countries] ADD [HasStates] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420182940_AddSsnEncryptedAndCountryHasStates'
)
BEGIN
    EXEC(N'UPDATE Countries SET HasStates = 1 WHERE Iso2 IN (''US'', ''CA'', ''AU'', ''BR'', ''MX'', ''IN'', ''AR'', ''DE'', ''ES'', ''IT'', ''GB'', ''MY'', ''NG'', ''ZA'', ''RU'', ''PT'', ''CL'', ''CO'', ''VE'')');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260420182940_AddSsnEncryptedAndCountryHasStates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260420182940_AddSsnEncryptedAndCountryHasStates', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421205728_AddCarBonusCommissionType'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'AmountPromo', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [Amount], [AmountPromo], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (85, 500.0, NULL, 4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Earn $500/month when maintaining personal activity (6+ pts) and 1,000+ Enrollment Team points in the calendar month.'', 0, 0, CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 0, 0.5E0, 0.5E0, 0, N''Car Bonus'', 0, 15, 0.0, 6, CAST(0 AS bit), 0, 0.0E0, 0, 0, 1000, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'AmountPromo', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260421205728_AddCarBonusCommissionType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260421205728_AddCarBonusCommissionType', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE TABLE [DocumentTypes] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_DocumentTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE TABLE [S3StorageConfig] (
        [Id] int NOT NULL IDENTITY,
        [BucketName] nvarchar(200) NOT NULL,
        [Region] nvarchar(50) NOT NULL,
        [FolderPrefix] nvarchar(200) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_S3StorageConfig] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE TABLE [MarketingDocuments] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(300) NOT NULL,
        [DocumentTypeId] int NOT NULL,
        [LanguageCode] nvarchar(10) NOT NULL,
        [LanguageName] nvarchar(100) NOT NULL,
        [S3Key] nvarchar(1000) NOT NULL,
        [OriginalFileName] nvarchar(500) NOT NULL,
        [FileSizeBytes] bigint NOT NULL,
        [ContentType] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_MarketingDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MarketingDocuments_DocumentTypes_DocumentTypeId] FOREIGN KEY ([DocumentTypeId]) REFERENCES [DocumentTypes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[DocumentTypes]'))
        SET IDENTITY_INSERT [DocumentTypes] ON;
    EXEC(N'INSERT INTO [DocumentTypes] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder])
    VALUES (1, N''system'', ''2026-01-01T00:00:00.0000000'', NULL, CAST(1 AS bit), NULL, NULL, N''Compensation Plan'', 1),
    (2, N''system'', ''2026-01-01T00:00:00.0000000'', NULL, CAST(1 AS bit), NULL, NULL, N''Marketing'', 2),
    (3, N''system'', ''2026-01-01T00:00:00.0000000'', NULL, CAST(1 AS bit), NULL, NULL, N''Independent Lifestyle Ambassador Agreement'', 3),
    (4, N''system'', ''2026-01-01T00:00:00.0000000'', NULL, CAST(1 AS bit), NULL, NULL, N''Policies & Procedures'', 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder') AND [object_id] = OBJECT_ID(N'[DocumentTypes]'))
        SET IDENTITY_INSERT [DocumentTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentTypes_Name] ON [DocumentTypes] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE INDEX [IX_MarketingDocuments_DocumentTypeId] ON [MarketingDocuments] ([DocumentTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE INDEX [IX_MarketingDocuments_DocumentTypeId_LanguageCode] ON [MarketingDocuments] ([DocumentTypeId], [LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    CREATE INDEX [IX_MarketingDocuments_LanguageCode] ON [MarketingDocuments] ([LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260422170645_AddMarketingDocuments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260422170645_AddMarketingDocuments', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 18, [MaxEnrollmentTeamPointsPerBranch] = 0.66000000000000003E0, [MaxTeamPointsPerBranch] = 0.0E0, [RankDescription] = N''Qualify with 18 Enrollment Team points (max 66% per leg).'', [TeamPoints] = 0
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 72, [MaxTeamPointsPerBranch] = 0.0E0, [RankDescription] = N''Qualify with 72 Enrollment Team points (max 50% per leg).'', [TeamPoints] = 0
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 175, [MaxTeamPointsPerBranch] = 0.0E0, [RankDescription] = N''Qualify with 175 Enrollment Team points (max 50% per leg). Boost Bonus unlocked.'', [TeamPoints] = 0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 175, [RankDescription] = N''Qualify with 350 Dual Team points and 175 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 350, [RankDescription] = N''Qualify with 700 Dual Team and 350 Enrollment Team points (max 50% per leg). Presidential Bonus unlocked.''
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 750, [RankDescription] = N''Qualify with 1,500 Dual Team and 750 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 1500, [RankDescription] = N''Qualify with 3,000 Dual Team and 1,500 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 3000, [RankDescription] = N''Qualify with 6,000 Dual Team and 3,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 5000, [RankDescription] = N''Qualify with 10,000 Dual Team and 5,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 7500, [RankDescription] = N''Qualify with 15,000 Dual Team and 7,500 Enrollment Team points (max 50% per leg). Car Bonus unlocked.''
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 10000, [RankDescription] = N''Qualify with 20,000 Dual Team and 10,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 15000, [RankDescription] = N''Qualify with 30,000 Dual Team and 15,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 30000, [RankDescription] = N''Qualify with 60,000 Dual Team and 30,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 60000, [RankDescription] = N''Qualify with 120,000 Dual Team and 60,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 100000, [RankDescription] = N''Qualify with 200,000 Dual Team and 100,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 150000, [RankDescription] = N''Qualify with 300,000 Dual Team and 150,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 200000, [RankDescription] = N''Qualify with 400,000 Dual Team and 200,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 250000, [RankDescription] = N''Qualify with 500,000 Dual Team and 250,000 Enrollment Team points (max 50% per leg).''
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [EnrollmentTeam] = 350000, [RankDescription] = N''Qualify with 700,000 Dual Team and 350,000 Enrollment Team points (max 50% per leg). The pinnacle of the Ambassador journey.''
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428150505_UpdateRankRequirementsToCanonical'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428150505_UpdateRankRequirementsToCanonical', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    ALTER TABLE [WalletHistories] ADD [Action] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    ALTER TABLE [WalletHistories] ADD [NewAccountIdentifier] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    ALTER TABLE [WalletHistories] ADD [NewIsPreferred] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    ALTER TABLE [WalletHistories] ADD [OldAccountIdentifier] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    ALTER TABLE [WalletHistories] ADD [OldIsPreferred] bit NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    CREATE TABLE [WalletApiLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(20) NOT NULL,
        [WalletType] int NOT NULL,
        [Operation] nvarchar(60) NOT NULL,
        [Endpoint] nvarchar(500) NULL,
        [HttpMethod] nvarchar(10) NULL,
        [RequestBody] nvarchar(max) NULL,
        [HttpStatusCode] int NOT NULL,
        [ResponseBody] nvarchar(max) NULL,
        [Success] bit NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [DurationMs] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_WalletApiLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    CREATE INDEX [IX_WalletApiLogs_CreationDate] ON [WalletApiLogs] ([CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    CREATE INDEX [IX_WalletApiLogs_MemberId_WalletType] ON [WalletApiLogs] ([MemberId], [WalletType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428173035_AddWalletApiLogAndExtendHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428173035_AddWalletApiLogAndExtendHistory', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    EXEC(N'IF OBJECT_ID(''PaymentGateways'', ''U'') IS NOT NULL DROP TABLE PaymentGateways;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    EXEC(N'DELETE FROM __EFMigrationsHistory WHERE MigrationId = ''20260428193800_AddPaymentGatewayInfo'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    CREATE TABLE [PaymentGateways] (
        [Id] int NOT NULL IDENTITY,
        [WalletType] int NOT NULL,
        [DisplayName] nvarchar(80) NOT NULL,
        [Description] nvarchar(2000) NOT NULL,
        [AdminFee] decimal(10,2) NOT NULL,
        [AdminFeeKind] int NOT NULL,
        [MinAdminFee] decimal(10,2) NULL,
        [Currency] nvarchar(3) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PaymentGateways] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AdminFee', N'AdminFeeKind', N'CreatedBy', N'CreationDate', N'Currency', N'Description', N'DisplayName', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'MinAdminFee', N'WalletType') AND [object_id] = OBJECT_ID(N'[PaymentGateways]'))
        SET IDENTITY_INSERT [PaymentGateways] ON;
    EXEC(N'INSERT INTO [PaymentGateways] ([Id], [AdminFee], [AdminFeeKind], [CreatedBy], [CreationDate], [Currency], [Description], [DisplayName], [IsActive], [LastUpdateBy], [LastUpdateDate], [MinAdminFee], [WalletType])
    VALUES (1, 1.95, 1, N''seed'', ''2026-04-28T00:00:00.0000000Z'', N''USD'', N''I-Payout maintains your in-account balance. Once you register, I-Payout sends a confirmation email; you must verify before payouts can be sent. Funds typically arrive within 24 hours of approval. International withdrawals from your I-Payout account to a bank may incur additional fees from I-Payout itself. Admin fee: $1.95 USD per transaction.'', N''eWallet (I-Payout)'', CAST(1 AS bit), NULL, NULL, NULL, 4),
    (2, 1.95, 1, N''seed'', ''2026-04-28T00:00:00.0000000Z'', N''USD'', N''Dwolla pushes commissions directly into your linked US bank account. You must complete Dwolla''''s identity verification before your account is approved. Standard ACH transfers settle in 3–5 business days. Dwolla is US-only. Admin fee: $1.95 USD per transaction.'', N''Dwolla'', CAST(1 AS bit), NULL, NULL, NULL, 1),
    (3, 2.0, 2, N''seed'', ''2026-04-28T00:00:00.0000000Z'', N''USD'', N''Provide the receiving wallet address for Bitcoin (BTC) or USDT (TRC-20). Double-check the address — crypto transactions are irreversible. The company is not liable for funds sent to a wrong address you provided. Network fees are deducted from the payout in addition to the admin fee. Admin fee: minimum 2% of payout, with a minimum of $6.95 USD per transaction.'', N''Crypto (Bitcoin / USDT)'', CAST(1 AS bit), NULL, NULL, 6.95, 9),
    (4, 1.95, 1, N''seed'', ''2026-04-28T00:00:00.0000000Z'', N''USD'', N''AdvCash holds funds in your AdvCash account in the currency of your choice. From there you can withdraw to bank, card, or other gateways. Verification through AdvCash is required to lift withdrawal limits. Available in most regions worldwide. Admin fee: $1.95 USD per transaction.'', N''AdvCash'', CAST(1 AS bit), NULL, NULL, NULL, 10)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AdminFee', N'AdminFeeKind', N'CreatedBy', N'CreationDate', N'Currency', N'Description', N'DisplayName', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'MinAdminFee', N'WalletType') AND [object_id] = OBJECT_ID(N'[PaymentGateways]'))
        SET IDENTITY_INSERT [PaymentGateways] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentGateways_WalletType] ON [PaymentGateways] ([WalletType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428220829_AddPaymentGatewayInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428220829_AddPaymentGatewayInfo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428223426_AddCreditCardEncryptedPanAndPriority'
)
BEGIN
    ALTER TABLE [CreditCards] ADD [EncryptedPan] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428223426_AddCreditCardEncryptedPanAndPriority'
)
BEGIN
    ALTER TABLE [CreditCards] ADD [Priority] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428223426_AddCreditCardEncryptedPanAndPriority'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428223426_AddCreditCardEncryptedPanAndPriority', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    EXEC(N'DELETE FROM CreditCards;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CreditCards]') AND [c].[name] = N'ExpiryMonth');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [CreditCards] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [CreditCards] DROP COLUMN [ExpiryMonth];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CreditCards]') AND [c].[name] = N'ExpiryYear');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [CreditCards] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [CreditCards] DROP COLUMN [ExpiryYear];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    ALTER TABLE [CreditCards] ADD [EncryptedCvv] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    ALTER TABLE [CreditCards] ADD [EncryptedExpiry] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428225715_EncryptCreditCardExpiryAndCvv'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428225715_EncryptCreditCardExpiryAndCvv', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428234320_AddProfilePreferencesAndHistories'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [DefaultLanguage] nvarchar(8) NOT NULL DEFAULT N'en';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428234320_AddProfilePreferencesAndHistories'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [PayoutFrequency] int NOT NULL DEFAULT 2;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428234320_AddProfilePreferencesAndHistories'
)
BEGIN
    CREATE TABLE [MemberAddressHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [PreviousAddress] nvarchar(max) NULL,
        [PreviousCity] nvarchar(max) NULL,
        [PreviousState] nvarchar(max) NULL,
        [PreviousZipCode] nvarchar(max) NULL,
        [PreviousCountry] nvarchar(max) NULL,
        [NewAddress] nvarchar(max) NULL,
        [NewCity] nvarchar(max) NULL,
        [NewState] nvarchar(max) NULL,
        [NewZipCode] nvarchar(max) NULL,
        [NewCountry] nvarchar(max) NULL,
        [Reason] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberAddressHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428234320_AddProfilePreferencesAndHistories'
)
BEGIN
    CREATE TABLE [MemberCredentialChangeLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(max) NOT NULL,
        [Kind] int NOT NULL,
        [PreviousValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MemberCredentialChangeLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260428234320_AddProfilePreferencesAndHistories'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260428234320_AddProfilePreferencesAndHistories', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429003855_AddEinToMemberProfile'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [EinEncrypted] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260429003855_AddEinToMemberProfile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260429003855_AddEinToMemberProfile', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    DROP INDEX [IX_TokenTypeProducts_TokenTypeId_ProductId] ON [TokenTypeProducts];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    ALTER TABLE [TokenTypes] ADD [Category] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTypeProducts]') AND [c].[name] = N'QuantityGranted');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [TokenTypeProducts] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [TokenTypeProducts] ADD DEFAULT 1 FOR [QuantityGranted];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    ALTER TABLE [TokenTypeProducts] ADD [Role] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [TokenTypeId])
    VALUES (1, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000001-prod-0000-0000-000000000001'', 1, 2),
    (2, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 8),
    (3, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 88),
    (4, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 64),
    (5, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 64),
    (6, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 65),
    (7, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 65),
    (8, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 86),
    (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 86),
    (10, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 5),
    (11, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 5),
    (12, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 11),
    (13, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 11),
    (14, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 71),
    (15, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 71),
    (16, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 72),
    (17, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 72),
    (18, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 81),
    (19, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 81),
    (20, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 82),
    (21, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 82),
    (22, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 98),
    (23, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 98),
    (24, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 69),
    (25, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 69),
    (26, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 69),
    (27, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 70),
    (28, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 70),
    (29, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 70),
    (30, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 73),
    (31, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 73),
    (32, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 73),
    (33, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 74),
    (34, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 74),
    (35, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 74),
    (36, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 83),
    (37, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 83),
    (38, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 83),
    (39, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 84),
    (40, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 84),
    (41, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 84),
    (42, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 99);
    INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [TokenTypeId])
    VALUES (43, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 99),
    (44, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 99),
    (45, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 13),
    (46, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 16),
    (47, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 19),
    (48, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 80),
    (49, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 80),
    (50, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 89),
    (51, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 90),
    (52, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 90),
    (53, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 92),
    (54, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 6),
    (55, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 94),
    (56, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 94),
    (57, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 96),
    (58, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 96),
    (59, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 96),
    (60, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 3),
    (61, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 4),
    (62, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 23),
    (63, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000005-prod-0000-0000-000000000005'', 1, 10)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [Role], [TokenTypeId])
    VALUES (64, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000001-prod-0000-0000-000000000001'', 1, 56)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [Role], [TokenTypeId])
    VALUES (65, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 2, 56)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [Role], [TokenTypeId])
    VALUES (66, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000001-prod-0000-0000-000000000001'', 1, 59)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [Role], [TokenTypeId])
    VALUES (67, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 2, 59)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [Role], [TokenTypeId])
    VALUES (68, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000002-prod-0000-0000-000000000002'', 1, 62)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [Role], [TokenTypeId])
    VALUES (69, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 2, 62)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [Role], [TokenTypeId])
    VALUES (70, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000003-prod-0000-0000-000000000003'', 1, 66)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] ON;
    EXEC(N'INSERT INTO [TokenTypeProducts] ([Id], [CreatedBy], [CreationDate], [LastUpdateBy], [LastUpdateDate], [ProductId], [QuantityGranted], [Role], [TokenTypeId])
    VALUES (71, N''seed'', ''2026-03-16T00:00:00.0000000Z'', NULL, NULL, N''00000004-prod-0000-0000-000000000004'', 1, 2, 66)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'LastUpdateBy', N'LastUpdateDate', N'ProductId', N'QuantityGranted', N'Role', N'TokenTypeId') AND [object_id] = OBJECT_ID(N'[TokenTypeProducts]'))
        SET IDENTITY_INSERT [TokenTypeProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 5
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 4
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 5
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 4
    WHERE [Id] = 21;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 22;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 4
    WHERE [Id] = 23;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 5
    WHERE [Id] = 24;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 25;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 26;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 27;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 28;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 29;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 30;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 31;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 32;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 33;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 34;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 35;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 36;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 37;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 38;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 39;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 40;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 41;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 42;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 43;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 44;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 45;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 46;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 47;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 48;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 49;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 50;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 51;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 52;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 53;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 54;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 3
    WHERE [Id] = 55;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 56;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 57;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 58;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 59;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 60;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 61;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 62;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 63;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 64;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 65;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 66;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 67;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 68;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 69;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 70;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 71;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 72;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 73;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 74;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 75;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 76;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 2
    WHERE [Id] = 77;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 4
    WHERE [Id] = 78;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 79;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 80;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 81;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 82;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 83;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 84;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 85;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 86;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 87;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 88;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 89;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 90;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 91;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 92;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 93;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 94;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 95;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 96;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 97;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 98;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    EXEC(N'UPDATE [TokenTypes] SET [Category] = 1
    WHERE [Id] = 99;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenTypeProducts_TokenTypeId_ProductId_Role] ON [TokenTypeProducts] ([TokenTypeId], [ProductId], [Role]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    ALTER TABLE [TokenTypeProducts] ADD CONSTRAINT [FK_TokenTypeProducts_TokenTypes_TokenTypeId] FOREIGN KEY ([TokenTypeId]) REFERENCES [TokenTypes] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505203928_AddTokenCategoryAndProductRole'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505203928_AddTokenCategoryAndProductRole', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTransactions]') AND [c].[name] = N'UsedByMemberId');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [TokenTransactions] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [TokenTransactions] ALTER COLUMN [UsedByMemberId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTransactions]') AND [c].[name] = N'ReferenceId');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [TokenTransactions] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [TokenTransactions] ALTER COLUMN [ReferenceId] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    DROP INDEX [IX_TokenTransactions_MemberId_CreationDate] ON [TokenTransactions];
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTransactions]') AND [c].[name] = N'MemberId');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [TokenTransactions] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [TokenTransactions] ALTER COLUMN [MemberId] nvarchar(36) NOT NULL;
    CREATE INDEX [IX_TokenTransactions_MemberId_CreationDate] ON [TokenTransactions] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenTransactions]') AND [c].[name] = N'DistributedToMemberId');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [TokenTransactions] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [TokenTransactions] ALTER COLUMN [DistributedToMemberId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [ExpiresAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [OriginalOwnerMemberId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [PreviousOwnerMemberId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [Status] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [UsedOnOrderId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    EXEC(N'
                    UPDATE [TokenTransactions]
                    SET [OriginalOwnerMemberId] = [MemberId],
                        [Status]                = CASE WHEN [UsedAt] IS NOT NULL THEN 2 ELSE 0 END
                    WHERE [ReferenceId] IS NOT NULL;
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    CREATE INDEX [IX_TokenTransactions_MemberId_Status_ReferenceId] ON [TokenTransactions] ([MemberId], [Status], [ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TokenTransactions_ReferenceId] ON [TokenTransactions] ([ReferenceId]) WHERE [ReferenceId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505221341_AddTokenInstanceLifecycle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505221341_AddTokenInstanceLifecycle', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506001920_AddSignupRiskFingerprints'
)
BEGIN
    CREATE TABLE [SignupRiskFingerprints] (
        [Id] bigint NOT NULL IDENTITY,
        [VisitorId] nvarchar(100) NOT NULL,
        [RequestId] nvarchar(100) NULL,
        [Flow] int NOT NULL,
        [OrderId] nvarchar(36) NULL,
        [MemberId] nvarchar(36) NULL,
        [SponsorReplicateSite] nvarchar(100) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(500) NULL,
        [CountryIso2] nvarchar(2) NULL,
        [IsFlagged] bit NOT NULL,
        [FlagReason] nvarchar(200) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_SignupRiskFingerprints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506001920_AddSignupRiskFingerprints'
)
BEGIN
    CREATE INDEX [IX_SignupRiskFingerprints_IpAddress_CreationDate] ON [SignupRiskFingerprints] ([IpAddress], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506001920_AddSignupRiskFingerprints'
)
BEGIN
    CREATE INDEX [IX_SignupRiskFingerprints_OrderId] ON [SignupRiskFingerprints] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506001920_AddSignupRiskFingerprints'
)
BEGIN
    CREATE INDEX [IX_SignupRiskFingerprints_VisitorId_CreationDate] ON [SignupRiskFingerprints] ([VisitorId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260506001920_AddSignupRiskFingerprints'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260506001920_AddSignupRiskFingerprints', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507104500_AddTicketCommentAuthorTypeColumn'
)
BEGIN
    ALTER TABLE [TicketComments] ADD [AuthorType] nvarchar(max) NOT NULL DEFAULT N'customer';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507104500_AddTicketCommentAuthorTypeColumn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507104500_AddTicketCommentAuthorTypeColumn', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140342_AddLifestyleConsultantRank'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] ON;
    EXEC(N'INSERT INTO [RankDefinitions] ([Id], [CertificateTemplateUrl], [CreatedBy], [CreationDate], [Description], [LastUpdateBy], [LastUpdateDate], [Name], [SortOrder], [Status])
    VALUES (20, NULL, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Default starting rank — no requirements. Earned automatically on signup.'', NULL, NULL, N''Lifestyle Consultant'', 0, 1)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CertificateTemplateUrl', N'CreatedBy', N'CreationDate', N'Description', N'LastUpdateBy', N'LastUpdateDate', N'Name', N'SortOrder', N'Status') AND [object_id] = OBJECT_ID(N'[RankDefinitions]'))
        SET IDENTITY_INSERT [RankDefinitions] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260507140342_AddLifestyleConsultantRank'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260507140342_AddLifestyleConsultantRank', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508003007_AddMemberStatisticHistory'
)
BEGIN
    CREATE TABLE [MemberStatisticHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(20) NOT NULL,
        [SnapshotYear] int NOT NULL,
        [SnapshotMonth] int NOT NULL,
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
        [LeftLegPoints] decimal(18,2) NOT NULL,
        [RightLegPoints] decimal(18,2) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MemberStatisticHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508003007_AddMemberStatisticHistory'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberStatisticHistories_MemberId_SnapshotYear_SnapshotMonth] ON [MemberStatisticHistories] ([MemberId], [SnapshotYear], [SnapshotMonth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508003007_AddMemberStatisticHistory'
)
BEGIN
    CREATE INDEX [IX_MemberStatisticHistories_SnapshotYear_SnapshotMonth] ON [MemberStatisticHistories] ([SnapshotYear], [SnapshotMonth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508003007_AddMemberStatisticHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508003007_AddMemberStatisticHistory', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508010339_AddCountryPayoutDefaultsAndCompanyDefaultFreq'
)
BEGIN
    ALTER TABLE [CompanyInfo] ADD [DefaultPayoutFrequency] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508010339_AddCountryPayoutDefaultsAndCompanyDefaultFreq'
)
BEGIN
    CREATE TABLE [CountryPayoutDefaults] (
        [Id] int NOT NULL IDENTITY,
        [CountryIso2] nvarchar(2) NOT NULL,
        [WalletType] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CountryPayoutDefaults] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508010339_AddCountryPayoutDefaultsAndCompanyDefaultFreq'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CountryPayoutDefaults_CountryIso2] ON [CountryPayoutDefaults] ([CountryIso2]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508010339_AddCountryPayoutDefaultsAndCompanyDefaultFreq'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508010339_AddCountryPayoutDefaultsAndCompanyDefaultFreq', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [DoubleBuilderBonus] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [DoubleSponsorBonus] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [ResetFsbCountdown] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [ResetFsbCountdownExecutedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD [FastStartBonus1ExtendedEnd] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD [FastStartBonus1ExtendedStart] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    ALTER TABLE [CommissionCountDownHistories] ADD [Reason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508111434_AddPromoBonusFlagsAndFsbHistoryFields', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE TABLE [CorporateContests] (
        [Id] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [BannerUrl] nvarchar(500) NULL,
        [RulesUrl] nvarchar(500) NULL,
        [TopX] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_CorporateContests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE TABLE [CorporateContestEarnings] (
        [Id] bigint NOT NULL IDENTITY,
        [ContestId] nvarchar(50) NOT NULL,
        [BeneficiaryMemberId] nvarchar(20) NOT NULL,
        [SourceMemberId] nvarchar(20) NOT NULL,
        [SourceOrderId] nvarchar(50) NOT NULL,
        [Level] int NOT NULL,
        [Points] int NOT NULL,
        [MembershipLevelId] int NOT NULL,
        [EarnedDate] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CorporateContestEarnings] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CorporateContestEarnings_CorporateContests_ContestId] FOREIGN KEY ([ContestId]) REFERENCES [CorporateContests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE TABLE [CorporateContestTranslations] (
        [Id] int NOT NULL IDENTITY,
        [ContestId] nvarchar(50) NOT NULL,
        [LanguageCode] nvarchar(10) NOT NULL,
        [Name] nvarchar(200) NULL,
        [Description] nvarchar(2000) NULL,
        [BannerUrl] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CorporateContestTranslations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CorporateContestTranslations_CorporateContests_ContestId] FOREIGN KEY ([ContestId]) REFERENCES [CorporateContests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE INDEX [IX_CorporateContestEarnings_ContestId_BeneficiaryMemberId] ON [CorporateContestEarnings] ([ContestId], [BeneficiaryMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateContestEarnings_ContestId_SourceOrderId_BeneficiaryMemberId] ON [CorporateContestEarnings] ([ContestId], [SourceOrderId], [BeneficiaryMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE INDEX [IX_CorporateContests_IsActive] ON [CorporateContests] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE INDEX [IX_CorporateContests_StartDate_EndDate] ON [CorporateContests] ([StartDate], [EndDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateContestTranslations_ContestId_LanguageCode] ON [CorporateContestTranslations] ([ContestId], [LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508134800_AddCorporateContests'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508134800_AddCorporateContests', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508194253_AddContestPointsBoxPosition'
)
BEGIN
    ALTER TABLE [CorporateContests] ADD [PointsBoxXPercent] int NOT NULL DEFAULT 75;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508194253_AddContestPointsBoxPosition'
)
BEGIN
    ALTER TABLE [CorporateContests] ADD [PointsBoxYPercent] int NOT NULL DEFAULT 45;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508194253_AddContestPointsBoxPosition'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508194253_AddContestPointsBoxPosition', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508200922_BackfillContestPointsBoxDefaults'
)
BEGIN
    EXEC(N'
                    UPDATE CorporateContests
                       SET PointsBoxXPercent = 50,
                           PointsBoxYPercent = 50
                     WHERE PointsBoxXPercent = 0
                       AND PointsBoxYPercent = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260508200922_BackfillContestPointsBoxDefaults'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260508200922_BackfillContestPointsBoxDefaults', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [ApiCredentials] (
        [Id] nvarchar(450) NOT NULL,
        [ServiceKey] nvarchar(100) NOT NULL,
        [Environment] nvarchar(50) NOT NULL,
        [BaseUrl] nvarchar(500) NULL,
        [ApiKeyEncrypted] nvarchar(2000) NULL,
        [SecretKeyEncrypted] nvarchar(2000) NULL,
        [MerchantIdEncrypted] nvarchar(2000) NULL,
        [AdditionalSecretEncrypted] nvarchar(2000) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_ApiCredentials] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [CountryGroups] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CountryGroups] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [CurrencyPolicies] (
        [Id] int NOT NULL IDENTITY,
        [PresentmentCurrency] nvarchar(3) NOT NULL,
        [MarkupPercent] decimal(5,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CurrencyPolicies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [ExchangeRateSnapshots] (
        [Id] bigint NOT NULL IDENTITY,
        [BaseCurrency] nvarchar(3) NOT NULL,
        [QuoteCurrency] nvarchar(3) NOT NULL,
        [Rate] decimal(18,8) NOT NULL,
        [FetchedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ExchangeRateSnapshots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayCatalog] (
        [Id] int NOT NULL IDENTITY,
        [Processor] int NOT NULL,
        [DisplayName] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [SupportsRefund] bit NOT NULL,
        [SupportsRecurring] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_GatewayCatalog] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayChargeAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [RouteBucketKey] nvarchar(200) NOT NULL,
        [CardProcessor] int NOT NULL,
        [FallbackStepIndex] int NOT NULL,
        [PresentmentCurrency] nvarchar(3) NOT NULL,
        [OriginalAmountUsd] decimal(18,4) NOT NULL,
        [ConvertedAmount] decimal(18,4) NOT NULL,
        [ExchangeRateUsed] decimal(18,8) NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [GatewayTransactionId] nvarchar(500) NULL,
        [PaymentHistoryId] nvarchar(100) NULL,
        [FailureReason] nvarchar(1000) NULL,
        [AttemptedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [MemberId] nvarchar(50) NOT NULL,
        [OperationType] int NOT NULL,
        [CardBrand] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_GatewayChargeAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayFallbackRules] (
        [Id] int NOT NULL IDENTITY,
        [OperationType] int NOT NULL,
        [PrimaryProcessor] int NOT NULL,
        [StepOrder] int NOT NULL,
        [NextProcessor] int NOT NULL,
        [DelayMinutes] int NOT NULL,
        [ForceUsdOnFallback] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_GatewayFallbackRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayRoutingCounters] (
        [Id] bigint NOT NULL IDENTITY,
        [RouteBucketKey] nvarchar(200) NOT NULL,
        [CardProcessor] int NOT NULL,
        [AttemptCount] bigint NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_GatewayRoutingCounters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [CountryGroupCountries] (
        [Id] int NOT NULL IDENTITY,
        [CountryGroupId] int NOT NULL,
        [IsoCountryCode] nvarchar(3) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_CountryGroupCountries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CountryGroupCountries_CountryGroups_CountryGroupId] FOREIGN KEY ([CountryGroupId]) REFERENCES [CountryGroups] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayRoutingRules] (
        [Id] int NOT NULL IDENTITY,
        [OperationType] int NOT NULL,
        [CardBrand] int NULL,
        [IsoCountryCode] nvarchar(3) NULL,
        [CountryGroupId] int NULL,
        [IsCatchAll] bit NOT NULL,
        [CurrencyPolicyId] int NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_GatewayRoutingRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GatewayRoutingRules_CountryGroups_CountryGroupId] FOREIGN KEY ([CountryGroupId]) REFERENCES [CountryGroups] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_GatewayRoutingRules_CurrencyPolicies_CurrencyPolicyId] FOREIGN KEY ([CurrencyPolicyId]) REFERENCES [CurrencyPolicies] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE TABLE [GatewayRoutingRuleSplits] (
        [Id] int NOT NULL IDENTITY,
        [GatewayRoutingRuleId] int NOT NULL,
        [CardProcessor] int NOT NULL,
        [WeightPercent] decimal(5,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_GatewayRoutingRuleSplits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GatewayRoutingRuleSplits_GatewayRoutingRules_GatewayRoutingRuleId] FOREIGN KEY ([GatewayRoutingRuleId]) REFERENCES [GatewayRoutingRules] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ApiCredentials_ServiceKey_Environment] ON [ApiCredentials] ([ServiceKey], [Environment]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CountryGroupCountries_CountryGroupId_IsoCountryCode] ON [CountryGroupCountries] ([CountryGroupId], [IsoCountryCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CountryGroups_Code] ON [CountryGroups] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CurrencyPolicies_PresentmentCurrency] ON [CurrencyPolicies] ([PresentmentCurrency]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_ExchangeRateSnapshots_QuoteCurrency_ExpiresAtUtc] ON [ExchangeRateSnapshots] ([QuoteCurrency], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayCatalog_IsActive] ON [GatewayCatalog] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GatewayCatalog_Processor] ON [GatewayCatalog] ([Processor]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayChargeAttempts_AttemptedAtUtc] ON [GatewayChargeAttempts] ([AttemptedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayChargeAttempts_MemberId] ON [GatewayChargeAttempts] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayChargeAttempts_PaymentHistoryId] ON [GatewayChargeAttempts] ([PaymentHistoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GatewayFallbackRules_OperationType_PrimaryProcessor_StepOrder] ON [GatewayFallbackRules] ([OperationType], [PrimaryProcessor], [StepOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingCounters_RouteBucketKey] ON [GatewayRoutingCounters] ([RouteBucketKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GatewayRoutingCounters_RouteBucketKey_CardProcessor] ON [GatewayRoutingCounters] ([RouteBucketKey], [CardProcessor]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingRules_CountryGroupId] ON [GatewayRoutingRules] ([CountryGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingRules_CurrencyPolicyId] ON [GatewayRoutingRules] ([CurrencyPolicyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingRules_IsActive] ON [GatewayRoutingRules] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingRules_OperationType_CardBrand] ON [GatewayRoutingRules] ([OperationType], [CardBrand]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    CREATE INDEX [IX_GatewayRoutingRuleSplits_GatewayRoutingRuleId] ON [GatewayRoutingRuleSplits] ([GatewayRoutingRuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512111629_AddBillingGatewayRoutingEngine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512111629_AddBillingGatewayRoutingEngine', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [DailyResidualEarnings] (
        [Id] bigint NOT NULL IDENTITY,
        [BeneficiaryMemberId] nvarchar(100) NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [EarnedDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [SourceOrderId] nvarchar(100) NULL,
        [Notes] nvarchar(500) NULL,
        [ConsolidatedIntoCommissionEarningId] nvarchar(100) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_DailyResidualEarnings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [GlobalParameters] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(200) NOT NULL,
        [Value] nvarchar(2000) NOT NULL,
        [Description] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_GlobalParameters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [RecurringBillingAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [SubscriptionBillingStateId] nvarchar(100) NOT NULL,
        [MemberId] nvarchar(100) NOT NULL,
        [ProductId] nvarchar(100) NOT NULL,
        [AttemptIndex] int NOT NULL,
        [AttemptedAt] datetime2 NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [FundingSource] int NOT NULL,
        [Outcome] int NOT NULL,
        [PaymentHistoryId] nvarchar(100) NULL,
        [OrderId] nvarchar(100) NULL,
        [TokenTransactionId] bigint NULL,
        [CommissionDeductionEarningId] nvarchar(100) NULL,
        [GatewayChargeAttemptId] bigint NULL,
        [FailureReason] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_RecurringBillingAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [RecurringBillingPlans] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [CycleType] int NOT NULL,
        [RetryCadenceDays] nvarchar(100) NOT NULL,
        [OnAllRetriesFail] int NOT NULL,
        [StopAfterUnbilledDays] int NULL,
        [PayFromCommissionBalanceFirst] bit NOT NULL,
        [TokenTypeId] int NULL,
        [FixedAmountOverride] decimal(18,4) NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_RecurringBillingPlans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [RecurringBillingPlanProducts] (
        [Id] int NOT NULL IDENTITY,
        [RecurringBillingPlanId] int NOT NULL,
        [ProductId] nvarchar(100) NOT NULL,
        [TokenTypeIdOverride] int NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_RecurringBillingPlanProducts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringBillingPlanProducts_RecurringBillingPlans_RecurringBillingPlanId] FOREIGN KEY ([RecurringBillingPlanId]) REFERENCES [RecurringBillingPlans] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE TABLE [SubscriptionBillingStates] (
        [Id] nvarchar(450) NOT NULL,
        [MembershipSubscriptionId] nvarchar(100) NOT NULL,
        [MemberId] nvarchar(100) NOT NULL,
        [RecurringBillingPlanId] int NOT NULL,
        [BillingAnchorDate] datetime2 NOT NULL,
        [LastSuccessfulBillingDate] datetime2 NULL,
        [NextBillingDate] datetime2 NOT NULL,
        [CurrentAttemptIndex] int NOT NULL,
        [NextAttemptDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [LastAttemptAt] datetime2 NULL,
        [LastAttemptOutcome] nvarchar(50) NULL,
        [LastFailureReason] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_SubscriptionBillingStates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SubscriptionBillingStates_RecurringBillingPlans_RecurringBillingPlanId] FOREIGN KEY ([RecurringBillingPlanId]) REFERENCES [RecurringBillingPlans] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_DailyResidualEarnings_BeneficiaryMemberId_Status] ON [DailyResidualEarnings] ([BeneficiaryMemberId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_DailyResidualEarnings_EarnedDate] ON [DailyResidualEarnings] ([EarnedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GlobalParameters_Key] ON [GlobalParameters] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingAttempts_MemberId_AttemptedAt] ON [RecurringBillingAttempts] ([MemberId], [AttemptedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingAttempts_Outcome_AttemptedAt] ON [RecurringBillingAttempts] ([Outcome], [AttemptedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingAttempts_SubscriptionBillingStateId] ON [RecurringBillingAttempts] ([SubscriptionBillingStateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingPlanProducts_ProductId] ON [RecurringBillingPlanProducts] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RecurringBillingPlanProducts_RecurringBillingPlanId_ProductId] ON [RecurringBillingPlanProducts] ([RecurringBillingPlanId], [ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingPlans_CycleType] ON [RecurringBillingPlans] ([CycleType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingPlans_IsActive] ON [RecurringBillingPlans] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_SubscriptionBillingStates_MemberId] ON [SubscriptionBillingStates] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionBillingStates_MembershipSubscriptionId] ON [SubscriptionBillingStates] ([MembershipSubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_SubscriptionBillingStates_RecurringBillingPlanId] ON [SubscriptionBillingStates] ([RecurringBillingPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    CREATE INDEX [IX_SubscriptionBillingStates_Status_NextAttemptDate] ON [SubscriptionBillingStates] ([Status], [NextAttemptDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512214230_AddRecurringBillingEngine'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512214230_AddRecurringBillingEngine', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235015_AddDailyResidualEarningSnapshotFields'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [CurrentRankId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235015_AddDailyResidualEarningSnapshotFields'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [EligibleDualTeamPoints] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235015_AddDailyResidualEarningSnapshotFields'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [EligibleEnrollmentTeamPoints] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235015_AddDailyResidualEarningSnapshotFields'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [PersonalPoints] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235015_AddDailyResidualEarningSnapshotFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512235015_AddDailyResidualEarningSnapshotFields', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235143_BackfillDailyResidualEarningsFromCommissionEarning'
)
BEGIN
    EXEC(N'
    INSERT INTO DailyResidualEarnings
        (BeneficiaryMemberId, Amount, EarnedDate, Status, SourceOrderId, Notes,
         ConsolidatedIntoCommissionEarningId,
         CurrentRankId, EligibleDualTeamPoints, EligibleEnrollmentTeamPoints, PersonalPoints,
         CreationDate, CreatedBy)
    SELECT
        CE.BeneficiaryMemberId,
        CE.Amount,
        CE.EarnedDate,
        CE.Status,
        CE.SourceOrderId,
        CASE
            WHEN CE.Notes IS NULL OR CE.Notes = ''''
            THEN N''Backfilled from CommissionEarning #'' + CE.Id
            ELSE CE.Notes + N'' [backfilled from CommissionEarning #'' + CE.Id + N'']''
        END,
        NULL,  -- ConsolidatedIntoCommissionEarningId: not applicable for backfilled rows
        NULL,  -- CurrentRankId: snapshot not recoverable
        NULL,  -- EligibleDualTeamPoints: snapshot not recoverable
        NULL,  -- EligibleEnrollmentTeamPoints: snapshot not recoverable
        NULL,  -- PersonalPoints: snapshot not recoverable
        GETUTCDATE(),
        N''backfill-migration''
    FROM CommissionEarnings CE
    INNER JOIN CommissionTypes CT ON CE.CommissionTypeId = CT.Id
    WHERE CT.ResidualBased = 1
      AND CT.IsPaidOnSignup = 0
      AND CE.IsDeleted = 0;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235143_BackfillDailyResidualEarningsFromCommissionEarning'
)
BEGIN
    EXEC(N'
    UPDATE CE
    SET
        CE.IsDeleted  = 1,
        CE.DeletedAt  = GETUTCDATE(),
        CE.DeletedBy  = N''backfill-migration'',
        CE.Notes      = ISNULL(CE.Notes + N'' '', N'''') + N''[migrated to DailyResidualEarning — see backfill-migration]'',
        CE.LastUpdateDate = GETUTCDATE(),
        CE.LastUpdateBy   = N''backfill-migration''
    FROM CommissionEarnings CE
    INNER JOIN CommissionTypes CT ON CE.CommissionTypeId = CT.Id
    WHERE CT.ResidualBased = 1
      AND CT.IsPaidOnSignup = 0
      AND CE.IsDeleted = 0;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260512235143_BackfillDailyResidualEarningsFromCommissionEarning'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260512235143_BackfillDailyResidualEarningsFromCommissionEarning', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513012953_AddDailyResidualEarningPaymentTracking'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [CommentedBy] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513012953_AddDailyResidualEarningPaymentTracking'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [PaymentComment] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513012953_AddDailyResidualEarningPaymentTracking'
)
BEGIN
    ALTER TABLE [DailyResidualEarnings] ADD [PaymentDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513012953_AddDailyResidualEarningPaymentTracking'
)
BEGIN
    EXEC(N'
                    UPDATE DailyResidualEarnings
                    SET    PaymentDate  = EarnedDate,
                           CommentedBy  = N''backfill-migration''
                    WHERE  CreatedBy    = N''backfill-migration''
                      AND  Status       = 2;  -- CommissionEarningStatus.Paid
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513012953_AddDailyResidualEarningPaymentTracking'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513012953_AddDailyResidualEarningPaymentTracking', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [SubscriptionBillingStates] ADD [ShardKey] bigint NOT NULL IDENTITY;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD [DualTeamContribution] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD [EnrollmentContribution] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD [PersonalContribution] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [CreditCards] ADD [SpreedlyPaymentMethodToken] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    ALTER TABLE [ApiCredentials] ADD [SpreedlyGatewayTokenEncrypted] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE TABLE [CommissionTriggerQueues] (
        [Id] bigint NOT NULL IDENTITY,
        [BatchId] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(100) NOT NULL,
        [OrderId] nvarchar(100) NOT NULL,
        [TriggerType] nvarchar(50) NOT NULL,
        [IsProcessed] bit NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CommissionTriggerQueues] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE TABLE [PointDeltaEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [BatchId] nvarchar(450) NOT NULL,
        [OrderId] nvarchar(100) NOT NULL,
        [MemberId] nvarchar(100) NOT NULL,
        [ProductId] nvarchar(100) NULL,
        [EventType] int NOT NULL,
        [DualTeamDelta] int NOT NULL,
        [EnrollmentDelta] int NOT NULL,
        [PersonalDelta] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [AppliedAt] datetime2 NULL,
        [FailureReason] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PointDeltaEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE TABLE [RecurringBillingBatches] (
        [Id] nvarchar(450) NOT NULL,
        [RunDate] datetime2 NOT NULL,
        [Gateway] int NOT NULL,
        [WorkerCount] int NOT NULL,
        [ScheduledStartTime] datetime2 NOT NULL,
        [CaseCount] int NOT NULL,
        [Status] int NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_RecurringBillingBatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE TABLE [RecurringBillingBatchShards] (
        [Id] nvarchar(450) NOT NULL,
        [BatchId] nvarchar(450) NOT NULL,
        [ShardIndex] int NOT NULL,
        [IdRangeStart] bigint NOT NULL,
        [IdRangeEnd] bigint NOT NULL,
        [AssignedWorkerKey] nvarchar(200) NULL,
        [Status] int NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [CasesProcessed] int NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_RecurringBillingBatchShards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecurringBillingBatchShards_RecurringBillingBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [RecurringBillingBatches] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SubscriptionBillingStates_ShardKey] ON [SubscriptionBillingStates] ([ShardKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_CommissionTriggerQueues_BatchId_IsProcessed] ON [CommissionTriggerQueues] ([BatchId], [IsProcessed]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_CommissionTriggerQueues_MemberId] ON [CommissionTriggerQueues] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_PointDeltaEvents_BatchId_Status] ON [PointDeltaEvents] ([BatchId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_PointDeltaEvents_MemberId] ON [PointDeltaEvents] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingBatches_RunDate_Gateway] ON [RecurringBillingBatches] ([RunDate], [Gateway]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingBatches_Status] ON [RecurringBillingBatches] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RecurringBillingBatchShards_BatchId_ShardIndex] ON [RecurringBillingBatchShards] ([BatchId], [ShardIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    CREATE INDEX [IX_RecurringBillingBatchShards_BatchId_Status] ON [RecurringBillingBatchShards] ([BatchId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513175646_AddSpreedlyVaultAndHighVolumeBilling', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513182346_AddGatewayChargeAttemptLatencyMs'
)
BEGIN
    ALTER TABLE [GatewayChargeAttempts] ADD [LatencyMs] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260513182346_AddGatewayChargeAttemptLatencyMs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260513182346_AddGatewayChargeAttemptLatencyMs', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 5;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 6;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 7;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 8;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 9;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 10;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 11;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 12;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 13;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 14;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 15;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 16;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 17;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 18;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    EXEC(N'UPDATE [RankRequirements] SET [ExternalMembers] = 0
    WHERE [Id] = 19;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524112532_RemoveExternalMembersRequirement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524112532_RemoveExternalMembersRequirement', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524134551_AddMemberStatisticDeltaQueue'
)
BEGIN
    CREATE TABLE [MemberStatisticDeltas] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(100) NOT NULL,
        [EnrollmentPointsDelta] int NOT NULL,
        [EnrollmentTeamSizeDelta] int NOT NULL,
        [QualifiedSponsoredMembersDelta] int NOT NULL,
        [IsApplied] bit NOT NULL,
        [AppliedAt] datetime2 NULL,
        [SourceMemberId] nvarchar(100) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MemberStatisticDeltas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524134551_AddMemberStatisticDeltaQueue'
)
BEGIN
    CREATE INDEX [IX_MemberStatisticDeltas_IsApplied_CreationDate] ON [MemberStatisticDeltas] ([IsApplied], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524134551_AddMemberStatisticDeltaQueue'
)
BEGIN
    CREATE INDEX [IX_MemberStatisticDeltas_MemberId_IsApplied] ON [MemberStatisticDeltas] ([MemberId], [IsApplied]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260524134551_AddMemberStatisticDeltaQueue'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260524134551_AddMemberStatisticDeltaQueue', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    DROP INDEX [IX_SignupRiskFingerprints_VisitorId_CreationDate] ON [SignupRiskFingerprints];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    ALTER TABLE [SignupRiskFingerprints] ADD [ClearReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    ALTER TABLE [SignupRiskFingerprints] ADD [Cleared] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    ALTER TABLE [SignupRiskFingerprints] ADD [ClearedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    ALTER TABLE [SignupRiskFingerprints] ADD [ClearedBy] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    CREATE INDEX [IX_SignupRiskFingerprints_VisitorId_CreationDate_Cleared] ON [SignupRiskFingerprints] ([VisitorId], [CreationDate], [Cleared]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526154312_AddSignupRiskFingerprintClearedFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526154312_AddSignupRiskFingerprintClearedFields', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [SponsorBonusMultiplier] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    ALTER TABLE [CorporatePromos] ADD [BuilderBonusMultiplier] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    EXEC(N'
                    UPDATE CorporatePromos
                    SET SponsorBonusMultiplier = CASE WHEN DoubleSponsorBonus = 1 THEN 2 ELSE 1 END,
                        BuilderBonusMultiplier = CASE WHEN DoubleBuilderBonus = 1 THEN 2 ELSE 1 END;
                ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CorporatePromos]') AND [c].[name] = N'DoubleSponsorBonus');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [CorporatePromos] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [CorporatePromos] DROP COLUMN [DoubleSponsorBonus];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CorporatePromos]') AND [c].[name] = N'DoubleBuilderBonus');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [CorporatePromos] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [CorporatePromos] DROP COLUMN [DoubleBuilderBonus];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526163527_ReplaceCorporatePromoDoubleFlagsWithMultipliers', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DROP INDEX [IX_GenealogyTree_HierarchyPath] ON [GenealogyTree];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DROP INDEX [IX_DualTeamTree_HierarchyPath] ON [DualTeamTree];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GenealogyTree]') AND [c].[name] = N'ParentMemberId');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [GenealogyTree] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [GenealogyTree] ALTER COLUMN [ParentMemberId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[GenealogyTree]') AND [c].[name] = N'HierarchyPath');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [GenealogyTree] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [GenealogyTree] ALTER COLUMN [HierarchyPath] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DualTeamTree]') AND [c].[name] = N'ParentMemberId');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [DualTeamTree] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [DualTeamTree] ALTER COLUMN [ParentMemberId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DualTeamTree]') AND [c].[name] = N'HierarchyPath');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [DualTeamTree] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [DualTeamTree] ALTER COLUMN [HierarchyPath] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree] ([ParentMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_ParentMemberId] ON [DualTeamTree] ([ParentMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260603162424_MakeHierarchyPathUnboundedDropPathIndex', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606133753_AddDualTeamMemberIdUniqueIndex'
)
BEGIN
    DROP INDEX [IX_DualTeamTree_MemberId] ON [DualTeamTree];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606133753_AddDualTeamMemberIdUniqueIndex'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DualTeamTree_MemberId] ON [DualTeamTree] ([MemberId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606133753_AddDualTeamMemberIdUniqueIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606133753_AddDualTeamMemberIdUniqueIndex', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606164857_AddDualTeamLegFrontier'
)
BEGIN
    CREATE TABLE [DualTeamLegFrontiers] (
        [Id] nvarchar(450) NOT NULL,
        [SponsorMemberId] nvarchar(450) NOT NULL,
        [PreferredSide] int NOT NULL,
        [DeepestMemberId] nvarchar(max) NULL,
        [DeepestDepth] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_DualTeamLegFrontiers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606164857_AddDualTeamLegFrontier'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DualTeamLegFrontiers_SponsorMemberId] ON [DualTeamLegFrontiers] ([SponsorMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606164857_AddDualTeamLegFrontier'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606164857_AddDualTeamLegFrontier', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606214221_AddDualTeamMemberIdLookupIndex'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_MemberId_Lookup] ON [DualTeamTree] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260606214221_AddDualTeamMemberIdLookupIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260606214221_AddDualTeamMemberIdLookupIndex', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE TABLE [PayoutAttemptEarnings] (
        [Id] bigint NOT NULL IDENTITY,
        [PayoutAttemptId] bigint NOT NULL,
        [CommissionEarningId] nvarchar(50) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PayoutAttemptEarnings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE TABLE [PayoutAttempts] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(50) NOT NULL,
        [WalletTypeSnapshot] int NOT NULL,
        [PayoutAccountSnapshot] nvarchar(500) NOT NULL,
        [PayoutAccountMetaSnapshot] nvarchar(2000) NULL,
        [AmountUsd] decimal(18,2) NOT NULL,
        [ProcessDateUtc] datetime2 NOT NULL,
        [Outcome] nvarchar(20) NOT NULL,
        [GatewayTransactionId] nvarchar(500) NULL,
        [GatewayErrorCode] nvarchar(100) NULL,
        [GatewayErrorMessage] nvarchar(2000) NULL,
        [AttemptedAtUtc] datetime2 NOT NULL,
        [CompletedAtUtc] datetime2 NULL,
        [LatencyMs] bigint NULL,
        [EarningsCount] int NOT NULL,
        [DisbursementMode] int NOT NULL,
        [PayoutBatchId] nvarchar(50) NULL,
        [ReceiptUrl] nvarchar(1000) NULL,
        [ReceiptSha256] nvarchar(64) NULL,
        [ReceiptLedgerSeq] bigint NULL,
        [ReceiptPrevHash] nvarchar(64) NULL,
        [ReceiptAnchorRef] nvarchar(500) NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_PayoutAttempts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE TABLE [PayoutBatches] (
        [Id] nvarchar(50) NOT NULL,
        [WalletType] int NOT NULL,
        [ProcessDateUtc] datetime2 NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ExportCsvUrl] nvarchar(1000) NULL,
        [ResultCsvUrl] nvarchar(1000) NULL,
        [MemberCount] int NOT NULL,
        [TotalAmountUsd] decimal(18,2) NOT NULL,
        [ReconciledBy] nvarchar(100) NULL,
        [ReconciledAt] datetime2 NULL,
        [Notes] nvarchar(2000) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] varbinary(max) NULL,
        CONSTRAINT [PK_PayoutBatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE TABLE [PayoutGatewaySettings] (
        [Id] int NOT NULL IDENTITY,
        [WalletType] int NOT NULL,
        [MinimumPayoutAmount] decimal(18,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PayoutGatewaySettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutAttemptEarnings_CommissionEarningId] ON [PayoutAttemptEarnings] ([CommissionEarningId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutAttemptEarnings_PayoutAttemptId] ON [PayoutAttemptEarnings] ([PayoutAttemptId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutAttempts_MemberId_ProcessDateUtc] ON [PayoutAttempts] ([MemberId], [ProcessDateUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutAttempts_Outcome_ProcessDateUtc] ON [PayoutAttempts] ([Outcome], [ProcessDateUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutAttempts_PayoutBatchId] ON [PayoutAttempts] ([PayoutBatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE INDEX [IX_PayoutBatches_WalletType_ProcessDateUtc] ON [PayoutBatches] ([WalletType], [ProcessDateUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PayoutGatewaySettings_WalletType] ON [PayoutGatewaySettings] ([WalletType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610163527_AddPayoutGatewayAndAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610163527_AddPayoutGatewayAndAudit', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610221657_SeedRankSeniorityBonusCommissionTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] ON;
    EXEC(N'INSERT INTO [CommissionCategories] ([Id], [CreatedBy], [CreationDate], [Description], [IsActive], [LastUpdateBy], [LastUpdateDate], [Name])
    VALUES (9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', N''Once-per-rank bonus for holding a rank ≥14 consecutive days.'', CAST(1 AS bit), NULL, NULL, N''Rank Seniority Bonus'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedBy', N'CreationDate', N'Description', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'Name') AND [object_id] = OBJECT_ID(N'[CommissionCategories]'))
        SET IDENTITY_INSERT [CommissionCategories] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610221657_SeedRankSeniorityBonusCommissionTypes'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'AmountPromo', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] ON;
    EXEC(N'INSERT INTO [CommissionTypes] ([Id], [Amount], [AmountPromo], [CommissionCategoryId], [CreatedBy], [CreationDate], [Cummulative], [CurrentRank], [DaysAfterJoining], [Description], [EnrollmentTeam], [ExternalMembers], [IsActive], [IsEnrollmentBased], [IsPaidOnRenewal], [IsPaidOnSignup], [IsRealTime], [IsSponsorBonus], [LastUpdateBy], [LastUpdateDate], [LevelNo], [LifeTimeRank], [MaxEnrollmentTeamPointsPerBranch], [MaxTeamPointsPerBranch], [MembersRebill], [Name], [NewMembers], [PaymentDelayDays], [Percentage], [PersonalPoints], [ResidualBased], [ResidualOverCommissionType], [ResidualPercentage], [ReverseId], [SponsoredMembers], [TeamPoints], [TriggerOrder])
    VALUES (86, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Silver (rank 1). Grant when ambassador holds Silver ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 1, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Silver'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (87, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Gold (rank 2). Grant when ambassador holds Gold ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 2, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Gold'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (88, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Platinum (rank 3). Grant when ambassador holds Platinum ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 3, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Platinum'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (89, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Titanium (rank 4). Grant when ambassador holds Titanium ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 4, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Titanium'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (90, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Jade (rank 5). Grant when ambassador holds Jade ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 5, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Jade'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (91, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Pearl (rank 6). Grant when ambassador holds Pearl ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 6, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Pearl'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (92, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Emerald (rank 7). Grant when ambassador holds Emerald ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 7, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Emerald'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (93, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Ruby (rank 8). Grant when ambassador holds Ruby ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 8, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Ruby'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (94, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Sapphire (rank 9). Grant when ambassador holds Sapphire ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 9, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Sapphire'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (95, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Diamond (rank 10). Grant when ambassador holds Diamond ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 10, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Diamond'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (96, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Double Diamond (rank 11). Grant when ambassador holds Double Diamond ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 11, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Double Diamond'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (97, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Triple Diamond (rank 12). Grant when ambassador holds Triple Diamond ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 12, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Triple Diamond'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (98, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Blue Diamond (rank 13). Grant when ambassador holds Blue Diamond ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 13, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Blue Diamond'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (99, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Black Diamond (rank 14). Grant when ambassador holds Black Diamond ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 14, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Black Diamond'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (100, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Royal (rank 15). Grant when ambassador holds Royal ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 15, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Royal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (101, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Double Royal (rank 16). Grant when ambassador holds Double Royal ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 16, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Double Royal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (102, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Triple Royal (rank 17). Grant when ambassador holds Triple Royal ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 17, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Triple Royal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (103, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Blue Royal (rank 18). Grant when ambassador holds Blue Royal ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 18, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Blue Royal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0),
    (104, 100.0, NULL, 9, N''seed'', ''2026-03-16T00:00:00.0000000Z'', CAST(0 AS bit), 0, 0, N''Once-per-rank seniority bonus for Black Royal (rank 19). Grant when ambassador holds Black Royal ≥14 consecutive days.'', 0, 0, CAST(1 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, 0, 19, 0.5E0, 0.5E0, 0, N''Rank Seniority Bonus – Black Royal'', 0, 0, 0.0, 0, CAST(0 AS bit), 0, 0.0E0, 0, 0, 0, 0)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Amount', N'AmountPromo', N'CommissionCategoryId', N'CreatedBy', N'CreationDate', N'Cummulative', N'CurrentRank', N'DaysAfterJoining', N'Description', N'EnrollmentTeam', N'ExternalMembers', N'IsActive', N'IsEnrollmentBased', N'IsPaidOnRenewal', N'IsPaidOnSignup', N'IsRealTime', N'IsSponsorBonus', N'LastUpdateBy', N'LastUpdateDate', N'LevelNo', N'LifeTimeRank', N'MaxEnrollmentTeamPointsPerBranch', N'MaxTeamPointsPerBranch', N'MembersRebill', N'Name', N'NewMembers', N'PaymentDelayDays', N'Percentage', N'PersonalPoints', N'ResidualBased', N'ResidualOverCommissionType', N'ResidualPercentage', N'ReverseId', N'SponsoredMembers', N'TeamPoints', N'TriggerOrder') AND [object_id] = OBJECT_ID(N'[CommissionTypes]'))
        SET IDENTITY_INSERT [CommissionTypes] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260610221657_SeedRankSeniorityBonusCommissionTypes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260610221657_SeedRankSeniorityBonusCommissionTypes', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    DROP TABLE [PayoutGatewaySettings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    ALTER TABLE [PaymentGateways] ADD [MinimumPayoutAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [MinimumPayoutAmount] = 25.0
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [MinimumPayoutAmount] = 25.0
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [MinimumPayoutAmount] = 25.0
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [MinimumPayoutAmount] = 25.0
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260611223108_UnifyPayoutGatewayOnPaymentGatewayInfo', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [Wallets]               SET [WalletType] = 6 WHERE [WalletType] = 10;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [WalletHistories]       SET [WalletType] = 6 WHERE [WalletType] = 10;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [WalletApiLogs]         SET [WalletType] = 6 WHERE [WalletType] = 10;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [CountryPayoutDefaults] SET [WalletType] = 6 WHERE [WalletType] = 10;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [PayoutBatches]         SET [WalletType] = 6 WHERE [WalletType] = 10;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'
    INSERT INTO [WalletHistories]
        ([WalletId], [MemberId], [WalletType], [Action], [OldStatus], [NewStatus],
         [OldAccountIdentifier], [NewAccountIdentifier], [OldIsPreferred], [NewIsPreferred],
         [ChangeReason], [CreationDate], [CreatedBy])
    SELECT w.[Id], w.[MemberId], 6, 7, w.[Status], w.[Status],
           w.[AccountIdentifier], w.[AccountIdentifier], w.[IsPreferred], w.[IsPreferred],
           N''Dwolla → Volet: Dwolla retired. Both gateways pay out to the same email address, so the account identifier is unchanged.'',
           SYSUTCDATETIME(), N''migration:MergeAdvcashIntoVoletAndRetireDwolla''
    FROM [Wallets] w
    WHERE w.[WalletType] = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [Wallets]               SET [WalletType] = 6 WHERE [WalletType] = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [CountryPayoutDefaults] SET [WalletType] = 6 WHERE [WalletType] = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [Description] = N''Dwolla is no longer offered. Existing accounts were migrated to Volet, which also pays out to your email address. This entry is kept only so historical payout records keep resolving.'', [DisplayName] = N''Dwolla (retired)'', [IsActive] = CAST(0 AS bit)
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [Description] = N''Volet (formerly AdvCash) holds funds in your Volet account in the currency of your choice. From there you can withdraw to bank, card, or other gateways. Verification through Volet is required to lift withdrawal limits. Available in most regions worldwide. Admin fee: $1.95 USD per transaction.'', [DisplayName] = N''Volet'', [WalletType] = 6
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815013705_MergeAdvcashIntoVoletAndRetireDwolla', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [PaymentGateways] ADD [AdminPortalUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [PaymentGateways] ADD [ApiVersion] nvarchar(10) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [PaymentGateways] ADD [Environment] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [ApiCredentials] ADD [PortalPasswordEncrypted] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [ApiCredentials] ADD [PortalUrl] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    ALTER TABLE [ApiCredentials] ADD [PortalUsernameEncrypted] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [AdminPortalUrl] = N''https://www.i-payout.com/'', [ApiVersion] = NULL, [Environment] = N''Sandbox''
    WHERE [Id] = 1;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [AdminPortalUrl] = NULL, [ApiVersion] = NULL, [Environment] = NULL
    WHERE [Id] = 2;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [AdminPortalUrl] = NULL, [ApiVersion] = NULL, [Environment] = NULL
    WHERE [Id] = 3;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    EXEC(N'UPDATE [PaymentGateways] SET [AdminPortalUrl] = N''https://account.volet.com/'', [ApiVersion] = NULL, [Environment] = NULL
    WHERE [Id] = 4;
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AdminFee', N'AdminFeeKind', N'AdminPortalUrl', N'ApiVersion', N'CreatedBy', N'CreationDate', N'Currency', N'Description', N'DisplayName', N'Environment', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'MinAdminFee', N'MinimumPayoutAmount', N'WalletType') AND [object_id] = OBJECT_ID(N'[PaymentGateways]'))
        SET IDENTITY_INSERT [PaymentGateways] ON;
    EXEC(N'INSERT INTO [PaymentGateways] ([Id], [AdminFee], [AdminFeeKind], [AdminPortalUrl], [ApiVersion], [CreatedBy], [CreationDate], [Currency], [Description], [DisplayName], [Environment], [IsActive], [LastUpdateBy], [LastUpdateDate], [MinAdminFee], [MinimumPayoutAmount], [WalletType])
    VALUES (5, 1.95, 1, N''https://sandbox.payquicker.io/'', N''V2'', N''seed'', ''2026-04-28T00:00:00.0000000Z'', N''USD'', N''PayQuicker delivers your commissions to an insured account linked to a debit card. You receive an invitation email to complete registration; once verified, funds are available instantly and can be spent online, at retail, or moved to your bank. Admin fee: $1.95 USD per transaction.'', N''PayQuicker'', N''Sandbox'', CAST(0 AS bit), NULL, NULL, NULL, 25.0, 11)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AdminFee', N'AdminFeeKind', N'AdminPortalUrl', N'ApiVersion', N'CreatedBy', N'CreationDate', N'Currency', N'Description', N'DisplayName', N'Environment', N'IsActive', N'LastUpdateBy', N'LastUpdateDate', N'MinAdminFee', N'MinimumPayoutAmount', N'WalletType') AND [object_id] = OBJECT_ID(N'[PaymentGateways]'))
        SET IDENTITY_INSERT [PaymentGateways] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    EXEC(N'
    INSERT INTO [ApiCredentials] ([Id],[ServiceKey],[Environment],[BaseUrl],[PortalUrl],[IsActive],[IsDeleted],[CreationDate],[LastUpdateDate],[CreatedBy])
    SELECT CONVERT(nvarchar(50), NEWID()), v.ServiceKey, v.Environment, v.BaseUrl, v.PortalUrl, 0, 0,
           SYSUTCDATETIME(), SYSUTCDATETIME(), N''migration:AddPayQuickerGatewayAndPortalCredentials''
    FROM (VALUES
        (N''PayQuickerV2'', N''Sandbox'',    N''https://api.sandbox.payquicker.io/api/v2'', N''https://sandbox.payquicker.io/''),
        (N''PayQuickerV2'', N''Production'', N''https://api.payquicker.io/api/v2'',         N''https://portal.payquicker.io/''),
        (N''PayQuickerV1'', N''Sandbox'',    N''https://platform.mypayquicker.build'',      N''https://mypayquicker.build/''),
        (N''PayQuickerV1'', N''Production'', N''https://platform.mypayquicker.com'',        N''https://mypayquicker.com/''),
        (N''EWallet'',      N''Sandbox'',    N''https://sandbox.i-payout.com/eWalletAPI'',  N''https://sandbox.i-payout.com/''),
        (N''EWallet'',      N''Production'', N''https://api.i-payout.com/eWalletAPI'',      N''https://www.i-payout.com/'')
    ) AS v(ServiceKey, Environment, BaseUrl, PortalUrl)
    WHERE NOT EXISTS (
        SELECT 1 FROM [ApiCredentials] c
        WHERE c.[ServiceKey] = v.ServiceKey AND c.[Environment] = v.Environment);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815020805_AddPayQuickerGatewayAndPortalCredentials'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815020805_AddPayQuickerGatewayAndPortalCredentials', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815032055_AddDataProtectionKeyRing'
)
BEGIN
    CREATE TABLE [DataProtectionKeys] (
        [Id] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [Xml] nvarchar(max) NULL,
        CONSTRAINT [PK_DataProtectionKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815032055_AddDataProtectionKeyRing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815032055_AddDataProtectionKeyRing', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815033123_SeedItInfrastructureKbArticle'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM [TicketCategories] WHERE [Id] = 8)
    BEGIN
        SET IDENTITY_INSERT [TicketCategories] ON;
        INSERT INTO [TicketCategories]
            ([Id],[Name],[Description],[DefaultPriority],[SortOrder],[IsActive],[CreationDate],[CreatedBy])
        VALUES
            (8, N''IT / Infrastructure'',
             N''Internal runbooks for the platform itself: encryption, key management, deployment and environment configuration. Not customer facing.'',
             N''Normal'', 100, 1, SYSUTCDATETIME(), N''migration:SeedItInfrastructureKbArticle'');
        SET IDENTITY_INSERT [TicketCategories] OFF;
    END;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815033123_SeedItInfrastructureKbArticle'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM [KbArticles] WHERE [Slug] = N''gateway-credential-encryption'')
    BEGIN
        INSERT INTO [KbArticles]
            ([Id],[Title],[Slug],[Body],[CategoryId],[TagsJson],[Visibility],[AuthorAgentId],
             [ViewCount],[HelpfulCount],[NotHelpfulCount],[PublishedAt],[Version],
             [CreationDate],[CreatedBy],[LastUpdateDate],[IsDeleted])
        VALUES
            (N''kb-gateway-credential-encryption'',
             N''Gateway credential encryption - how it works'',
             N''gateway-credential-encryption'',
             N''
    <h3>What this protects</h3>
    <p>Payout gateway credentials - PayQuicker client id and secret, the i-Payout merchant GUID and password, the company funding account token, and the admin portal sign-in details. They live in the <strong>ApiCredentials</strong> table and are stored encrypted, never in plain text.</p>
    <p>Two services touch them: <strong>AdminAPI</strong> writes them (someone fills the form at Admin &rarr; Billing &rarr; API Credentials) and <strong>Billing</strong> reads them when a payout runs. That split is why the setup below matters - both processes must be able to derive the same keys.</p>

    <h3>How it works: two layers</h3>
    <pre>
      secret                key ring key              certificate
      (ApiCredentials)      (DataProtectionKeys)      (outside the database)
           |                        |                        |
           +---- encrypted with ----+                        |
                                    +------ wrapped with ----+
    </pre>
    <ol>
      <li><strong>The secret</strong> is encrypted with AES-256-CBC + HMAC-SHA256 by ASP.NET Core Data Protection and stored with an <code>ENC:</code> prefix.</li>
      <li><strong>The key</strong> that did the encrypting lives in the <code>DataProtectionKeys</code> table of the same database both services already share.</li>
      <li><strong>That key is itself wrapped</strong> with an X.509 certificate. The database only ever holds the wrapped form.</li>
    </ol>
    <p>The point of layer 3: a database backup on its own is not enough to decrypt anything. Whoever restores it still needs the certificate private key, which is kept outside the database. Two factors, deliberately separated.</p>

    <h3>Where everything lives</h3>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th>Item</th><th>Location</th><th>Notes</th></tr>
      <tr><td>Encrypted secrets</td><td><code>ApiCredentials</code> table</td><td>Values start with <code>ENC:CfDJ8</code></td></tr>
      <tr><td>Key ring</td><td><code>DataProtectionKeys</code> table</td><td>Same database as the app. Rotates itself every 90 days</td></tr>
      <tr><td>Certificate</td><td>Windows cert store, or a PFX file</td><td>Configured per host, must be identical on AdminAPI and Billing</td></tr>
      <tr><td>Application name</td><td><code>MLMConqueror.GatewayCredentials.v1</code></td><td>Hardcoded. Changing it makes existing ciphertext unreadable</td></tr>
    </table>

    <h3>Configuration</h3>
    <p>Both AdminAPI and Billing read the same keys. Prefer <code>Thumbprint</code> in production - the private key never touches the application disk:</p>
    <pre>
    "DataProtection": {
      "Certificate": {
        "Thumbprint": "&lt;cert thumbprint in LocalMachine\My&gt;",
        "Path": "",
        "Password": ""
      }
    }
    </pre>
    <p>Environment variable form, for container or systemd deployments:</p>
    <pre>
    DataProtection__Certificate__Thumbprint=...
    </pre>
    <p>In <strong>Development only</strong>, if the PFX path does not exist a self-signed certificate is generated automatically so a fresh clone runs without setup. This never happens in any other environment - see Troubleshooting.</p>

    <h3>Setting up a new environment</h3>
    <ol>
      <li>Obtain or issue a certificate for key wrapping. It needs a private key and the Key Encipherment usage. A 10 year validity is reasonable - when it expires the key ring can no longer be unwrapped.</li>
      <li>Install it on both the AdminAPI and Billing hosts (same certificate, not one each).</li>
      <li>Set <code>DataProtection:Certificate:Thumbprint</code> on both.</li>
      <li>Start both services. The <code>DataProtectionKeys</code> table fills itself on first use.</li>
      <li>Enter the gateway credentials at Admin &rarr; Billing &rarr; API Credentials.</li>
      <li>Verify: run a payout in sandbox. If Billing can read the credential, the wiring is correct.</li>
    </ol>

    <h3>Rotating the certificate</h3>
    <p>Existing key ring entries stay wrapped with the old certificate, so it cannot simply be swapped:</p>
    <ol>
      <li>Install the new certificate alongside the old one on both hosts.</li>
      <li>Point <code>DataProtection:Certificate:Thumbprint</code> at the new one.</li>
      <li>Add the old one under <code>DataProtection:Certificate:Retired:0:Thumbprint</code> so existing keys can still be unwrapped.</li>
      <li>Restart both services. New keys are wrapped with the new certificate; old keys keep working.</li>
      <li>Only after every key has rotated (Data Protection rotates every 90 days, so allow a full cycle) may the retired entry be removed.</li>
    </ol>

    <h3>Backup and restore</h3>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th>If you lose...</th><th>Consequence</th></tr>
      <tr><td>The database</td><td>Restore it. The key ring comes back with it.</td></tr>
      <tr><td>The certificate</td><td><strong>Every stored credential becomes unrecoverable.</strong> The key ring cannot be unwrapped. Every gateway credential has to be re-entered by hand.</td></tr>
      <tr><td>Both</td><td>Same as above, plus the credentials themselves.</td></tr>
    </table>
    <p>So: back the certificate up somewhere that is not the database, and restrict who can read it. Anyone with the certificate and a database backup can read every gateway secret.</p>

    <h3>Troubleshooting</h3>
    <table border="1" cellpadding="6" cellspacing="0">
      <tr><th>Symptom</th><th>Cause</th><th>Fix</th></tr>
      <tr><td>Service will not start: "needs a certificate"</td><td>Neither Thumbprint nor Path configured</td><td>Set one. The service refuses to start rather than fail on the first payout.</td></tr>
      <tr><td>Service will not start: "certificate was not found at..."</td><td>PFX path configured but missing, outside Development</td><td>Restore it from the secret store. It is never regenerated - a fresh certificate could not unwrap the existing key ring.</td></tr>
      <tr><td><code>PAYQUICKER_CREDENTIAL_UNDECRYPTABLE</code></td><td>AdminAPI and Billing are not using the same certificate, or the stored value predates real encryption</td><td>Confirm both thumbprints match, then re-enter the credential.</td></tr>
      <tr><td><code>EWALLET_CREDENTIAL_UNDECRYPTABLE</code></td><td>Same, for i-Payout</td><td>Same.</td></tr>
      <tr><td><code>SECRET_ALREADY_PREFIXED</code> on save</td><td>A caller sent a value starting with <code>ENC:</code></td><td>Send the secret in plain text over TLS. The server encrypts it; the caller must not.</td></tr>
    </table>

    <h3>Things that will break it</h3>
    <ul>
      <li>Changing the application name or the protector purpose in code. Both are constants on <code>GatewayCredentialProtector</code> and are load bearing.</li>
      <li>Giving AdminAPI and Billing different certificates. They will both start, and every payout will fail to read its credential.</li>
      <li>Deleting rows from <code>DataProtectionKeys</code>. Anything encrypted with a deleted key is gone.</li>
      <li>Letting the certificate expire without rotating first.</li>
    </ul>

    <h3>How to tell a secret is really encrypted</h3>
    <p>Real ciphertext begins with <code>ENC:CfDJ8</code>. <code>CfDJ8</code> is the base64url form of the Data Protection magic header. A value that starts with <code>ENC:</code> but not <code>CfDJ8</code> is plain text with a prefix stuck on it - that was the behaviour of an earlier implementation and it is not encryption. Query to check:</p>
    <pre>
    SELECT ServiceKey, Environment,
           CASE WHEN ApiKeyEncrypted LIKE ''''ENC:CfDJ8%'''' THEN ''''encrypted''''
                WHEN ApiKeyEncrypted IS NULL          THEN ''''not set''''
                ELSE ''''NOT ENCRYPTED - re-enter it'''' END AS ApiKeyState
    FROM ApiCredentials;
    </pre>
    '',
             8,
             N''["encryption","data-protection","certificates","payquicker","i-payout","runbook"]'',
             2,                      -- Internal: staff only, never public
             N''migration:SeedItInfrastructureKbArticle'',
             0, 0, 0,
             SYSUTCDATETIME(), 1,
             SYSUTCDATETIME(), N''migration:SeedItInfrastructureKbArticle'', SYSUTCDATETIME(), 0);
    END;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815033123_SeedItInfrastructureKbArticle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815033123_SeedItInfrastructureKbArticle', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815213548_AddWalletProviderAccountCreatedAt'
)
BEGIN
    ALTER TABLE [Wallets] ADD [ProviderAccountCreatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815213548_AddWalletProviderAccountCreatedAt'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815213548_AddWalletProviderAccountCreatedAt', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815233551_SeedVoletCredential'
)
BEGIN
    EXEC(N'
    INSERT INTO [ApiCredentials] ([Id],[ServiceKey],[Environment],[BaseUrl],[PortalUrl],[IsActive],[IsDeleted],[CreationDate],[LastUpdateDate],[CreatedBy])
    SELECT CONVERT(nvarchar(50), NEWID()), v.ServiceKey, v.Environment, v.BaseUrl, v.PortalUrl, 0, 0,
           SYSUTCDATETIME(), SYSUTCDATETIME(), N''migration:SeedVoletCredential''
    FROM (VALUES
        (N''Volet'', N''Production'', N''https://wallet.advcash.com/wsm/merchantWebService'', N''https://account.volet.com/''),
        (N''Volet'', N''Sandbox'',    N''https://wallet.advcash.com/wsm/merchantWebService'', N''https://account.volet.com/'')
    ) AS v(ServiceKey, Environment, BaseUrl, PortalUrl)
    WHERE NOT EXISTS (
        SELECT 1 FROM [ApiCredentials] c
        WHERE c.[ServiceKey] = v.ServiceKey AND c.[Environment] = v.Environment);');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260815233551_SeedVoletCredential'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260815233551_SeedVoletCredential', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818201803_NormalizeEWalletIdentifiersToMemberId'
)
BEGIN
    EXEC(N'
    UPDATE [Wallets]
    SET [AccountIdentifier] = [MemberId]
    WHERE [WalletType] = 4              -- eWallet
      AND [AccountIdentifier] LIKE ''%@%'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818201803_NormalizeEWalletIdentifiersToMemberId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260818201803_NormalizeEWalletIdentifiersToMemberId', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [PreferredTwoFactorChannel] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    EXEC(N'UPDATE AspNetUsers SET PreferredTwoFactorChannel = 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TwoFactorEnrolledAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TwoFactorPhoneConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TwoFactorPhoneEncrypted] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [TwoFactorPhoneLast4] nvarchar(4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE TABLE [AuthSecurityEvents] (
        [Id] bigint NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [UserEmail] nvarchar(256) NOT NULL,
        [EventType] int NOT NULL,
        [Outcome] int NOT NULL,
        [OperationKey] nvarchar(64) NULL,
        [Channel] int NULL,
        [FailureReason] nvarchar(128) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [RequestPath] nvarchar(256) NULL,
        [ChallengeJti] nvarchar(64) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AuthSecurityEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuthSecurityEvents_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE TABLE [SmsTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [EventType] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SmsTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE TABLE [StepUpPolicies] (
        [OperationKey] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(128) NOT NULL,
        [Category] int NOT NULL,
        [IsEnabled] bit NOT NULL,
        [RequiredChannel] int NULL,
        [FreshnessWindowMinutes] int NOT NULL,
        [IsObsolete] bit NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_StepUpPolicies] PRIMARY KEY ([OperationKey])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE TABLE [SmsTemplateLocalizations] (
        [Id] int NOT NULL IDENTITY,
        [SmsTemplateId] int NOT NULL,
        [LanguageCode] nvarchar(10) NOT NULL,
        [Body] nvarchar(480) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        CONSTRAINT [PK_SmsTemplateLocalizations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SmsTemplateLocalizations_SmsTemplates_SmsTemplateId] FOREIGN KEY ([SmsTemplateId]) REFERENCES [SmsTemplates] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE INDEX [IX_AuthSecurityEvents_EventType_CreationDate] ON [AuthSecurityEvents] ([EventType], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE INDEX [IX_AuthSecurityEvents_OperationKey_CreationDate] ON [AuthSecurityEvents] ([OperationKey], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE INDEX [IX_AuthSecurityEvents_UserId_CreationDate] ON [AuthSecurityEvents] ([UserId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SmsTemplateLocalizations_SmsTemplateId_LanguageCode] ON [SmsTemplateLocalizations] ([SmsTemplateId], [LanguageCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SmsTemplates_EventType] ON [SmsTemplates] ([EventType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828120121_Add2faStepUpAndSmsTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828120121_Add2faStepUpAndSmsTemplates', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828172625_SeedTwoFactorTemplates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9001, N''Two-Factor Verification Code'', N''TWO_FACTOR_CODE'', N''Security'', N''Sent by SesEmailService when a member requests a 2FA code by email during login.'', CAST(1 AS bit), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828172625_SeedTwoFactorTemplates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9001, 9001, N''en'', N''Your MLM Conqueror verification code'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            A verification code was requested for your MLM Conqueror account.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 8px 0 24px 0;">'', nchar(10), N''            <span style="font-family: ''''Courier New'''', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;">{{Code}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            This code expires in {{ExpiresInMinutes}} minutes.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If you did not try to sign in, someone may know your password. Change it as soon as you can.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''A verification code was requested for your MLM Conqueror account.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{Code}}'', nchar(10), nchar(10), N''This code expires in {{ExpiresInMinutes}} minutes.'', nchar(10), nchar(10), N''If you did not try to sign in, someone may know your password. Change it as soon as you can.''), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL),
    (9002, 9001, N''es'', N''Tu código de verificación de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Se solicitó un código de verificación para tu cuenta de MLM Conqueror.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 8px 0 24px 0;">'', nchar(10), N''            <span style="font-family: ''''Courier New'''', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;">{{Code}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Este código caduca en {{ExpiresInMinutes}} minutos.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no intentaste iniciar sesión, es posible que alguien conozca tu contraseña. Cámbiala cuanto antes.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Se solicitó un código de verificación para tu cuenta de MLM Conqueror.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{Code}}'', nchar(10), nchar(10), N''Este código caduca en {{ExpiresInMinutes}} minutos.'', nchar(10), nchar(10), N''Si no intentaste iniciar sesión, es posible que alguien conozca tu contraseña. Cámbiala cuanto antes.''), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828172625_SeedTwoFactorTemplates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplates]'))
        SET IDENTITY_INSERT [SmsTemplates] ON;
    EXEC(N'INSERT INTO [SmsTemplates] ([Id], [Name], [EventType], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9001, N''Two-Factor Verification Code'', N''TWO_FACTOR_CODE'', CAST(1 AS bit), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplates]'))
        SET IDENTITY_INSERT [SmsTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828172625_SeedTwoFactorTemplates'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'SmsTemplateId', N'LanguageCode', N'Body', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplateLocalizations]'))
        SET IDENTITY_INSERT [SmsTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [SmsTemplateLocalizations] ([Id], [SmsTemplateId], [LanguageCode], [Body], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9001, 9001, N''en'', N''MLM Conqueror: your code is {{Code}}. Expires in {{ExpiresInMinutes}} min.'', ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL),
    (9002, 9001, N''es'', N''MLM Conqueror: tu código es {{Code}}. Caduca en {{ExpiresInMinutes}} min.'', ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'SmsTemplateId', N'LanguageCode', N'Body', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplateLocalizations]'))
        SET IDENTITY_INSERT [SmsTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828172625_SeedTwoFactorTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828172625_SeedTwoFactorTemplates', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210345_SeedEmailConfirmationTemplate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9002, N''Email Address Confirmation'', N''EMAIL_CONFIRMATION'', N''Security'', N''Sent by SesEmailService so a new user can confirm the email address they signed up with.'', CAST(1 AS bit), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210345_SeedEmailConfirmationTemplate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9003, 9002, N''en'', N''Confirm your MLM Conqueror email address'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;">'', nchar(10), N''            Confirm this address to finish setting up your account.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 0 0 24px 0;">'', nchar(10), N''            <a href="{{ConfirmationUrl}}" style="display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;">Confirm email address</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;">'', nchar(10), N''            If the button does not work, copy this link into your browser:<br />'', nchar(10), N''            <a href="{{ConfirmationUrl}}" style="color: #1a73e8;">{{ConfirmationUrl}}</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            This link expires in {{ExpiresInHours}} hours.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If you did not create this account, you can ignore this message.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Confirm this address to finish setting up your account.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{ConfirmationUrl}}'', nchar(10), nchar(10), N''This link expires in {{ExpiresInHours}} hours.'', nchar(10), nchar(10), N''If you did not create this account, you can ignore this message.''), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL),
    (9004, 9002, N''es'', N''Confirma tu dirección de correo de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;">'', nchar(10), N''            Confirma esta dirección para terminar de configurar tu cuenta.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 0 0 24px 0;">'', nchar(10), N''            <a href="{{ConfirmationUrl}}" style="display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;">Confirmar dirección de correo</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;">'', nchar(10), N''            Si el botón no funciona, copia este enlace en tu navegador:<br />'', nchar(10), N''            <a href="{{ConfirmationUrl}}" style="color: #1a73e8;">{{ConfirmationUrl}}</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Este enlace caduca en {{ExpiresInHours}} horas.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no creaste esta cuenta, puedes ignorar este mensaje.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Confirma esta dirección para terminar de configurar tu cuenta.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{ConfirmationUrl}}'', nchar(10), nchar(10), N''Este enlace caduca en {{ExpiresInHours}} horas.'', nchar(10), nchar(10), N''Si no creaste esta cuenta, puedes ignorar este mensaje.''), ''2026-08-28T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828210345_SeedEmailConfirmationTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828210345_SeedEmailConfirmationTemplate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829091129_SeedPasswordResetTemplate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9005, N''Password Reset'', N''PASSWORD_RESET'', N''Security'', N''Sent by SesEmailService so a user who forgot their password can set a new one.'', CAST(1 AS bit), ''2026-08-29T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829091129_SeedPasswordResetTemplate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9006, 9005, N''en'', N''Reset your MLM Conqueror password'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;">'', nchar(10), N''            We received a request to reset the password for your account.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 0 0 24px 0;">'', nchar(10), N''            <a href="{{ResetUrl}}" style="display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;">Reset password</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;">'', nchar(10), N''            If the button does not work, copy this link into your browser:<br />'', nchar(10), N''            <a href="{{ResetUrl}}" style="color: #1a73e8;">{{ResetUrl}}</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            This link expires in {{ExpiresInHours}} hours.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If you did not request this, you can ignore this message; your password will not change.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''We received a request to reset the password for your account.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{ResetUrl}}'', nchar(10), nchar(10), N''This link expires in {{ExpiresInHours}} hours.'', nchar(10), nchar(10), N''If you did not request this, you can ignore this message; your password will not change.''), ''2026-08-29T00:00:00.0000000Z'', N''system:seed'', NULL, NULL),
    (9007, 9005, N''es'', N''Restablece tu contraseña de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 24px; line-height: 1.5;">'', nchar(10), N''            Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 0 0 24px 0;">'', nchar(10), N''            <a href="{{ResetUrl}}" style="display: inline-block; background-color: #1a73e8; color: #ffffff; font-size: 16px; font-weight: bold; text-decoration: none; padding: 14px 28px; border-radius: 6px;">Restablecer contraseña</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #555555; padding-bottom: 16px; line-height: 1.5; word-break: break-all;">'', nchar(10), N''            Si el botón no funciona, copia este enlace en tu navegador:<br />'', nchar(10), N''            <a href="{{ResetUrl}}" style="color: #1a73e8;">{{ResetUrl}}</a>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Este enlace caduca en {{ExpiresInHours}} horas.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 13px; color: #999999; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no lo solicitaste, puedes ignorar este mensaje; tu contraseña no cambiará.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Hemos recibido una solicitud para restablecer la contraseña de tu cuenta.'' AS nvarchar(max)), nchar(10), nchar(10), N''{{ResetUrl}}'', nchar(10), nchar(10), N''Este enlace caduca en {{ExpiresInHours}} horas.'', nchar(10), nchar(10), N''Si no lo solicitaste, puedes ignorar este mensaje; tu contraseña no cambiará.''), ''2026-08-29T00:00:00.0000000Z'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829091129_SeedPasswordResetTemplate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829091129_SeedPasswordResetTemplate', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830161900_AddCryptoPaymentConfirmations'
)
BEGIN
    CREATE TABLE [CryptoPaymentConfirmations] (
        [Id] nvarchar(450) NOT NULL,
        [OrderId] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(50) NOT NULL,
        [MemberEmail] nvarchar(256) NOT NULL,
        [CryptoCurrency] nvarchar(20) NOT NULL,
        [AmountDue] decimal(18,4) NOT NULL,
        [Status] int NOT NULL,
        [CryptoTransactionId] nvarchar(128) NULL,
        [ConfirmedByUserId] nvarchar(450) NULL,
        [ConfirmedByEmail] nvarchar(256) NULL,
        [ConfirmedAt] datetime2 NULL,
        [Notes] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_CryptoPaymentConfirmations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830161900_AddCryptoPaymentConfirmations'
)
BEGIN
    CREATE INDEX [IX_CryptoPaymentConfirmations_MemberId] ON [CryptoPaymentConfirmations] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830161900_AddCryptoPaymentConfirmations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CryptoPaymentConfirmations_OrderId] ON [CryptoPaymentConfirmations] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830161900_AddCryptoPaymentConfirmations'
)
BEGIN
    CREATE INDEX [IX_CryptoPaymentConfirmations_Status_CreationDate] ON [CryptoPaymentConfirmations] ([Status], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830161900_AddCryptoPaymentConfirmations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830161900_AddCryptoPaymentConfirmations', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831015959_ActualizaCaducidadEnPlantillasDeEnlace'
)
BEGIN
    EXEC(N'
    UPDATE  l
    SET     l.HtmlBody       = REPLACE(REPLACE(l.HtmlBody, N''expires in {{ExpiresInHours}} hours'', N''expires in {{ExpiresInMinutes}} minutes''), N''caduca en {{ExpiresInHours}} horas'', N''caduca en {{ExpiresInMinutes}} minutos''),
            l.TextBody       = REPLACE(REPLACE(l.TextBody, N''expires in {{ExpiresInHours}} hours'', N''expires in {{ExpiresInMinutes}} minutes''), N''caduca en {{ExpiresInHours}} horas'', N''caduca en {{ExpiresInMinutes}} minutos''),
            l.LastUpdateDate = ''2026-08-30T00:00:00'',
            l.LastUpdateBy   = N''system:seed''
    FROM    EmailTemplateLocalizations l
    JOIN    EmailTemplates             t ON t.Id = l.EmailTemplateId
    WHERE   t.EventType IN (N''PASSWORD_RESET'', N''EMAIL_CONFIRMATION'')
      AND  (l.HtmlBody LIKE N''%{{ExpiresInHours}}%'' OR l.TextBody LIKE N''%{{ExpiresInHours}}%'');');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831015959_ActualizaCaducidadEnPlantillasDeEnlace'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831015959_ActualizaCaducidadEnPlantillasDeEnlace', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015004933_AgregaMarcaDeCambioDeFactor'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [SecurityFactorsChangedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015004933_AgregaMarcaDeCambioDeFactor'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015004933_AgregaMarcaDeCambioDeFactor', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010018_SiembraPlantillaCambioDeFactor'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9008, N''Security Factor Changed'', N''SECURITY_FACTOR_CHANGED'', N''Security'', N''Sent to the account owner when the email, confirmed phone, authenticator or preferred 2FA channel changes. Email changes go to the previous address.'', CAST(1 AS bit), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010018_SiembraPlantillaCambioDeFactor'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9008, 9008, N''en'', N''Security change on your MLM Conqueror account'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            On {{ChangedAt}} the following was changed on your MLM Conqueror account: <strong>{{Change}}</strong>.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            If you made this change, you don''''t need to do anything. For your protection, sensitive changes such as where your commissions are paid are blocked for a while.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If you did not make this change, someone may have access to your account. Sign in the usual way, change your password right away and contact support.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''On {{ChangedAt}} the following was changed on your MLM Conqueror account: {{Change}}.'' AS nvarchar(max)), nchar(10), nchar(10), N''If you made this change, you don''''t need to do anything. For your protection, sensitive changes such as where your commissions are paid are blocked for a while.'', nchar(10), nchar(10), N''If you did not make this change, someone may have access to your account. Sign in the usual way, change your password right away and contact support.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9009, 9008, N''es'', N''Cambio de seguridad en tu cuenta de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            El {{ChangedAt}} se cambió en tu cuenta de MLM Conqueror: <strong>{{Change}}</strong>.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Si fuiste tú, no tienes que hacer nada. Para protegerte, los cambios sensibles, como a dónde se pagan tus comisiones, quedan bloqueados durante un tiempo.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no fuiste tú, es posible que alguien tenga acceso a tu cuenta. Entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''El {{ChangedAt}} se cambió en tu cuenta de MLM Conqueror: {{Change}}.'' AS nvarchar(max)), nchar(10), nchar(10), N''Si fuiste tú, no tienes que hacer nada. Para protegerte, los cambios sensibles, como a dónde se pagan tus comisiones, quedan bloqueados durante un tiempo.'', nchar(10), nchar(10), N''Si no fuiste tú, es posible que alguien tenga acceso a tu cuenta. Entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010018_SiembraPlantillaCambioDeFactor'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015010018_SiembraPlantillaCambioDeFactor', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010941_SiembraPlantillaCodigoStepUp'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9010, N''Step-Up Verification Code'', N''STEP_UP_CODE'', N''Security'', N''Sent by TwoFactorService when a member requests a code to confirm a sensitive change (e.g. where commissions are paid).'', CAST(1 AS bit), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010941_SiembraPlantillaCodigoStepUp'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9010, 9010, N''en'', N''Confirm a change to your MLM Conqueror account'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            A sensitive change was requested on your MLM Conqueror account, such as changing where your commissions are paid. Use this code to confirm it:'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 8px 0 24px 0;">'', nchar(10), N''            <span style="font-family: ''''Courier New'''', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;">{{Code}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            This code expires in {{ExpiresInMinutes}} minutes. Never share it with anyone, not even with someone who says they work for MLM Conqueror.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If you did not request this change, do not use the code. Someone may have access to your account: change your password right away and contact support.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''A sensitive change was requested on your MLM Conqueror account, such as changing where your commissions are paid. Use this code to confirm it:'' AS nvarchar(max)), nchar(10), nchar(10), N''{{Code}}'', nchar(10), nchar(10), N''This code expires in {{ExpiresInMinutes}} minutes. Never share it with anyone, not even with someone who says they work for MLM Conqueror.'', nchar(10), nchar(10), N''If you did not request this change, do not use the code. Someone may have access to your account: change your password right away and contact support.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9011, 9010, N''es'', N''Confirma un cambio en tu cuenta de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Se solicitó un cambio sensible en tu cuenta de MLM Conqueror, como cambiar a dónde se pagan tus comisiones. Usa este código para confirmarlo:'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td align="center" style="padding: 8px 0 24px 0;">'', nchar(10), N''            <span style="font-family: ''''Courier New'''', Courier, monospace; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #111111;">{{Code}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Este código caduca en {{ExpiresInMinutes}} minutos. No lo compartas con nadie, ni siquiera con quien diga que trabaja para MLM Conqueror.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no solicitaste este cambio, no uses el código. Es posible que alguien tenga acceso a tu cuenta: cambia tu contraseña cuanto antes y contacta con soporte.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Se solicitó un cambio sensible en tu cuenta de MLM Conqueror, como cambiar a dónde se pagan tus comisiones. Usa este código para confirmarlo:'' AS nvarchar(max)), nchar(10), nchar(10), N''{{Code}}'', nchar(10), nchar(10), N''Este código caduca en {{ExpiresInMinutes}} minutos. No lo compartas con nadie, ni siquiera con quien diga que trabaja para MLM Conqueror.'', nchar(10), nchar(10), N''Si no solicitaste este cambio, no uses el código. Es posible que alguien tenga acceso a tu cuenta: cambia tu contraseña cuanto antes y contacta con soporte.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010941_SiembraPlantillaCodigoStepUp'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplates]'))
        SET IDENTITY_INSERT [SmsTemplates] ON;
    EXEC(N'INSERT INTO [SmsTemplates] ([Id], [Name], [EventType], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9002, N''Step-Up Verification Code'', N''STEP_UP_CODE'', CAST(1 AS bit), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplates]'))
        SET IDENTITY_INSERT [SmsTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010941_SiembraPlantillaCodigoStepUp'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'SmsTemplateId', N'LanguageCode', N'Body', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplateLocalizations]'))
        SET IDENTITY_INSERT [SmsTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [SmsTemplateLocalizations] ([Id], [SmsTemplateId], [LanguageCode], [Body], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9003, 9002, N''en'', N''MLM Conqueror: code {{Code}} confirms a sensitive change to your account, like where you get paid. Expires in {{ExpiresInMinutes}} min. Not you? Change your password.'', ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9004, 9002, N''es'', N''MLM Conqueror: código {{Code}} para confirmar un cambio sensible, como dónde cobras. Caduca en {{ExpiresInMinutes}} min. ¿No fuiste tú? Cambia tu clave.'', ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'SmsTemplateId', N'LanguageCode', N'Body', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[SmsTemplateLocalizations]'))
        SET IDENTITY_INSERT [SmsTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015010941_SiembraPlantillaCodigoStepUp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015010941_SiembraPlantillaCodigoStepUp', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015022139_SiembraPlantillaBloqueoDelSegundoFactor'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9012, N''Two-Factor Locked'', N''TWO_FACTOR_LOCKED'', N''Security'', N''Sent once to the account owner when the daily cap of wrong verification codes is reached and sign-in or step-up verification is blocked for up to 24 hours.'', CAST(1 AS bit), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015022139_SiembraPlantillaBloqueoDelSegundoFactor'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9012, 9012, N''en'', N''Security alert on your MLM Conqueror account'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            On {{LockedAt}} too many incorrect verification codes were entered to <strong>{{Action}}</strong> on your MLM Conqueror account.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            For your protection, that verification is blocked for up to 24 hours. Codes cannot be requested or used until then, not even the correct one.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If it wasn''''t you, someone knows your password or has access to an open session. Sign in the usual way once the block ends, change your password right away and contact support.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''On {{LockedAt}} too many incorrect verification codes were entered to {{Action}} on your MLM Conqueror account.'' AS nvarchar(max)), nchar(10), nchar(10), N''For your protection, that verification is blocked for up to 24 hours. Codes cannot be requested or used until then, not even the correct one.'', nchar(10), nchar(10), N''If it wasn''''t you, someone knows your password or has access to an open session. Sign in the usual way once the block ends, change your password right away and contact support.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9013, 9012, N''es'', N''Alerta de seguridad en tu cuenta de MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            El {{LockedAt}} se introdujeron demasiados códigos de verificación incorrectos para <strong>{{Action}}</strong> en tu cuenta de MLM Conqueror.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Para protegerte, esa verificación queda bloqueada hasta 24 horas. Mientras tanto no se pueden pedir ni usar códigos, ni siquiera el correcto.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si no fuiste tú, alguien conoce tu contraseña o tiene acceso a una sesión abierta. Cuando termine el bloqueo, entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''El {{LockedAt}} se introdujeron demasiados códigos de verificación incorrectos para {{Action}} en tu cuenta de MLM Conqueror.'' AS nvarchar(max)), nchar(10), nchar(10), N''Para protegerte, esa verificación queda bloqueada hasta 24 horas. Mientras tanto no se pueden pedir ni usar códigos, ni siquiera el correcto.'', nchar(10), nchar(10), N''Si no fuiste tú, alguien conoce tu contraseña o tiene acceso a una sesión abierta. Cuando termine el bloqueo, entra por el camino de siempre, cambia tu contraseña cuanto antes y contacta con soporte.''), ''2026-10-14T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015022139_SiembraPlantillaBloqueoDelSegundoFactor'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015022139_SiembraPlantillaBloqueoDelSegundoFactor', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    ALTER TABLE [TokenTypes] ADD [IsPurchasable] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    ALTER TABLE [TokenTransactions] ADD [TokenPurchaseId] nvarchar(36) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    ALTER TABLE [CryptoPaymentConfirmations] ADD [Purpose] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE TABLE [TokenPurchases] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(36) NOT NULL,
        [TokenTypeId] int NOT NULL,
        [GrantedProductIds] nvarchar(400) NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,4) NOT NULL,
        [TotalAmount] decimal(18,4) NOT NULL,
        [PaymentMethod] int NOT NULL,
        [Status] int NOT NULL,
        [OrderId] nvarchar(450) NOT NULL,
        [IdempotencyKey] nvarchar(64) NOT NULL,
        [PeriodKey] int NOT NULL,
        [CreditCardId] nvarchar(450) NULL,
        [PaymentHistoryId] nvarchar(450) NULL,
        [FailureCode] nvarchar(64) NULL,
        [Notes] nvarchar(500) NULL,
        [CompletedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_TokenPurchases] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE INDEX [IX_TokenTransactions_OriginalOwnerMemberId_TransactionType_CreationDate] ON [TokenTransactions] ([OriginalOwnerMemberId], [TransactionType], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TokenTransactions_TokenPurchaseId] ON [TokenTransactions] ([TokenPurchaseId]) WHERE [TokenPurchaseId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE INDEX [IX_CryptoPaymentConfirmations_Purpose_Status_CreationDate] ON [CryptoPaymentConfirmations] ([Purpose], [Status], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenPurchases_MemberId_IdempotencyKey] ON [TokenPurchases] ([MemberId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE INDEX [IX_TokenPurchases_MemberId_PeriodKey_Status] ON [TokenPurchases] ([MemberId], [PeriodKey], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TokenPurchases_OrderId] ON [TokenPurchases] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    CREATE INDEX [IX_TokenPurchases_Status] ON [TokenPurchases] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    EXEC(N'
    UPDATE TokenTypes
       SET IsPurchasable = 1
     WHERE IsActive = 1
       AND Category <> 2
       AND Name NOT LIKE ''%FREE%''
       AND Name NOT LIKE ''%Coupon%''
       AND Name NOT LIKE ''%SpecialPromo%''
       AND Name NOT LIKE ''%Help a Friend%'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015105744_CompraDeTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015105744_CompraDeTokens', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015111329_SiembraPlantillaCambioDireccionDeposito'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9016, N''Crypto Deposit Address Changed'', N''CRYPTO_DEPOSIT_ADDRESS_CHANGED'', N''Security'', N''Sent to every active SuperAdmin when a crypto deposit address (Crypto.DepositAddress.*) is created or changed in Global Parameters. Members send their crypto payments to that address.'', CAST(1 AS bit), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015111329_SiembraPlantillaCambioDireccionDeposito'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9016, 9016, N''en'', N''{{Currency}} deposit address changed in MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="520" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 520px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            On {{ChangedAt}} <strong>{{ChangedBy}}</strong> changed the <strong>{{Currency}}</strong> deposit address in Global Parameters. Members paying in {{Currency}} are now shown the new address.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 8px; line-height: 1.5;">'', nchar(10), N''            Previous address:<br /><span style="font-family: Consolas, ''''Courier New'''', monospace; color: #222222; word-break: break-all;">{{OldAddress}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            New address:<br /><span style="font-family: Consolas, ''''Courier New'''', monospace; color: #222222; word-break: break-all;">{{NewAddress}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            An empty previous address means it was just created; an empty new address means {{Currency}} is no longer offered.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            If nobody on the team expected this change, payments may be going to someone else''''s wallet. Sign in to the admin panel the usual way, restore the correct address right away and review that account.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''On {{ChangedAt}} {{ChangedBy}} changed the {{Currency}} deposit address in Global Parameters. Members paying in {{Currency}} are now shown the new address.'' AS nvarchar(max)), nchar(10), nchar(10), N''Previous address: {{OldAddress}}'', nchar(10), N''New address: {{NewAddress}}'', nchar(10), nchar(10), N''An empty previous address means it was just created; an empty new address means {{Currency}} is no longer offered.'', nchar(10), nchar(10), N''If nobody on the team expected this change, payments may be going to someone else''''s wallet. Sign in to the admin panel the usual way, restore the correct address right away and review that account.''), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9017, 9016, N''es'', N''Cambió la dirección de depósito {{Currency}} en MLM Conqueror'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="520" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 520px;">'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            El {{ChangedAt}} <strong>{{ChangedBy}}</strong> cambió la dirección de depósito <strong>{{Currency}}</strong> en Parámetros globales. A los miembros que pagan en {{Currency}} ya se les muestra la nueva.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 8px; line-height: 1.5;">'', nchar(10), N''            Dirección anterior:<br /><span style="font-family: Consolas, ''''Courier New'''', monospace; color: #222222; word-break: break-all;">{{OldAddress}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Dirección nueva:<br /><span style="font-family: Consolas, ''''Courier New'''', monospace; color: #222222; word-break: break-all;">{{NewAddress}}</span>'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">'', nchar(10), N''            Si la dirección anterior está vacía, se acaba de crear; si la nueva está vacía, {{Currency}} deja de ofrecerse.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">'', nchar(10), N''            Si nadie del equipo esperaba este cambio, los pagos podrían estar llegando a la cartera de otra persona. Entra al panel de administración por el camino de siempre, restablece la dirección correcta cuanto antes y revisa esa cuenta.'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''El {{ChangedAt}} {{ChangedBy}} cambió la dirección de depósito {{Currency}} en Parámetros globales. A los miembros que pagan en {{Currency}} ya se les muestra la nueva.'' AS nvarchar(max)), nchar(10), nchar(10), N''Dirección anterior: {{OldAddress}}'', nchar(10), N''Dirección nueva: {{NewAddress}}'', nchar(10), nchar(10), N''Si la dirección anterior está vacía, se acaba de crear; si la nueva está vacía, {{Currency}} deja de ofrecerse.'', nchar(10), nchar(10), N''Si nadie del equipo esperaba este cambio, los pagos podrían estar llegando a la cartera de otra persona. Entra al panel de administración por el camino de siempre, restablece la dirección correcta cuanto antes y revisa esa cuenta.''), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015111329_SiembraPlantillaCambioDireccionDeposito'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015111329_SiembraPlantillaCambioDireccionDeposito', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015114733_SiembraPlantillaReciboCompraTokens'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] ON;
    EXEC(N'INSERT INTO [EmailTemplates] ([Id], [Name], [EventType], [Category], [Description], [IsActive], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9014, N''Token Purchase Receipt'', N''TOKEN_PURCHASE_RECEIPT'', N''Billing'', N''Sent to the member when a token purchase from the BizCenter is completed, or when a crypto token purchase is confirmed. Never includes token codes.'', CAST(1 AS bit), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'EventType', N'Category', N'Description', N'IsActive', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplates]'))
        SET IDENTITY_INSERT [EmailTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015114733_SiembraPlantillaReciboCompraTokens'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] ON;
    EXEC(N'INSERT INTO [EmailTemplateLocalizations] ([Id], [EmailTemplateId], [LanguageCode], [Subject], [HtmlBody], [TextBody], [CreationDate], [CreatedBy], [LastUpdateDate], [LastUpdateBy])
    VALUES (9014, 9014, N''en'', N''Your token purchase receipt — order {{OrderNo}}'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr><td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">Hi {{FullName}}, thanks for your purchase.</td></tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;">'', nchar(10), N''            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />'', nchar(10), N''            <strong>Total:</strong> {{Amount}}<br />'', nchar(10), N''            <strong>Paid with:</strong> {{PaymentMethod}}<br />'', nchar(10), N''            <strong>Date:</strong> {{PurchaseDate}}<br />'', nchar(10), N''            <strong>Order:</strong> {{OrderNo}}<br />'', nchar(10), N''            <strong>Purchase id:</strong> {{PurchaseId}}'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr><td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.</td></tr>'', nchar(10), N''        <tr><td style="font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">If you did not make this purchase, sign in the usual way, change your password and contact support.</td></tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Hi {{FullName}}, thanks for your purchase.'' AS nvarchar(max)), nchar(10), nchar(10), N''Tokens: {{Quantity}} x {{TokenType}}'', nchar(10), N''Total: {{Amount}}'', nchar(10), N''Paid with: {{PaymentMethod}}'', nchar(10), N''Date: {{PurchaseDate}}'', nchar(10), N''Order: {{OrderNo}}'', nchar(10), N''Purchase id: {{PurchaseId}}'', nchar(10), nchar(10), N''Your tokens are in the Tokens section of your Biz Center. Token purchases are non-refundable.'', nchar(10), nchar(10), N''If you did not make this purchase, sign in the usual way, change your password and contact support.''), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL),
    (9015, 9014, N''es'', N''Recibo de tu compra de tokens — pedido {{OrderNo}}'', CONCAT(CAST(N''<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-family: Arial, Helvetica, sans-serif; background-color: #f4f4f4; padding: 24px 0;">'' AS nvarchar(max)), nchar(10), N''  <tr>'', nchar(10), N''    <td align="center">'', nchar(10), N''      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 8px; padding: 32px; max-width: 480px;">'', nchar(10), N''        <tr><td style="font-size: 16px; color: #222222; padding-bottom: 16px; line-height: 1.5;">Hola {{FullName}}, gracias por tu compra.</td></tr>'', nchar(10), N''        <tr>'', nchar(10), N''          <td style="font-size: 14px; color: #222222; padding-bottom: 16px; line-height: 1.7;">'', nchar(10), N''            <strong>Tokens:</strong> {{Quantity}} × {{TokenType}}<br />'', nchar(10), N''            <strong>Total:</strong> {{Amount}}<br />'', nchar(10), N''            <strong>Pagado con:</strong> {{PaymentMethod}}<br />'', nchar(10), N''            <strong>Fecha:</strong> {{PurchaseDate}}<br />'', nchar(10), N''            <strong>Pedido:</strong> {{OrderNo}}<br />'', nchar(10), N''            <strong>Id de compra:</strong> {{PurchaseId}}'', nchar(10), N''          </td>'', nchar(10), N''        </tr>'', nchar(10), N''        <tr><td style="font-size: 14px; color: #555555; padding-bottom: 16px; line-height: 1.5;">Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.</td></tr>'', nchar(10), N''        <tr><td style="font-size: 13px; color: #b00020; border-top: 1px solid #eeeeee; padding-top: 16px; line-height: 1.5;">Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.</td></tr>'', nchar(10), N''      </table>'', nchar(10), N''    </td>'', nchar(10), N''  </tr>'', nchar(10), N''</table>''), CONCAT(CAST(N''Hola {{FullName}}, gracias por tu compra.'' AS nvarchar(max)), nchar(10), nchar(10), N''Tokens: {{Quantity}} x {{TokenType}}'', nchar(10), N''Total: {{Amount}}'', nchar(10), N''Pagado con: {{PaymentMethod}}'', nchar(10), N''Fecha: {{PurchaseDate}}'', nchar(10), N''Pedido: {{OrderNo}}'', nchar(10), N''Id de compra: {{PurchaseId}}'', nchar(10), nchar(10), N''Tus tokens están en la sección Tokens de tu Biz Center. Las compras de tokens no son reembolsables.'', nchar(10), nchar(10), N''Si no hiciste esta compra, entra por el camino de siempre, cambia tu contraseña y contacta con soporte.''), ''2026-10-15T00:00:00.0000000'', N''system:seed'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'EmailTemplateId', N'LanguageCode', N'Subject', N'HtmlBody', N'TextBody', N'CreationDate', N'CreatedBy', N'LastUpdateDate', N'LastUpdateBy') AND [object_id] = OBJECT_ID(N'[EmailTemplateLocalizations]'))
        SET IDENTITY_INSERT [EmailTemplateLocalizations] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015114733_SiembraPlantillaReciboCompraTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015114733_SiembraPlantillaReciboCompraTokens', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015121849_AmpliaCreadoPorDeCompraDeTokens'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TokenPurchases]') AND [c].[name] = N'CreatedBy');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [TokenPurchases] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [TokenPurchases] ALTER COLUMN [CreatedBy] nvarchar(256) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015121849_AmpliaCreadoPorDeCompraDeTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015121849_AmpliaCreadoPorDeCompraDeTokens', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015194238_TokenDeConcurrenciaEnCodigos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015194238_TokenDeConcurrenciaEnCodigos', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015212959_GuardaDireccionDeDepositoDeCompra'
)
BEGIN
    ALTER TABLE [CryptoPaymentConfirmations] ADD [DepositAddress] nvarchar(512) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015212959_GuardaDireccionDeDepositoDeCompra'
)
BEGIN
    ALTER TABLE [CryptoPaymentConfirmations] ADD [DepositNetwork] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261015212959_GuardaDireccionDeDepositoDeCompra'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261015212959_GuardaDireccionDeDepositoDeCompra', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE TABLE [EntraxApiCalls] (
        [Id] bigint NOT NULL IDENTITY,
        [Operation] nvarchar(30) NOT NULL,
        [ExternalOrderId] nvarchar(100) NULL,
        [ExternalPaymentId] nvarchar(64) NULL,
        [HttpStatusCode] int NOT NULL,
        [Success] bit NOT NULL,
        [ErrorCode] nvarchar(60) NULL,
        [ProviderStatus] nvarchar(30) NULL,
        [DurationMs] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_EntraxApiCalls] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE TABLE [EntraxPayments] (
        [Id] nvarchar(450) NOT NULL,
        [OrderId] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(50) NOT NULL,
        [ExternalOrderId] nvarchar(100) NOT NULL,
        [ExternalPaymentId] nvarchar(64) NULL,
        [Status] int NOT NULL,
        [AmountUsd] decimal(18,4) NOT NULL,
        [AmountRub] decimal(18,4) NULL,
        [ExchangeRate] decimal(18,6) NULL,
        [PaidAmountUsd] decimal(18,4) NULL,
        [QrLink] nvarchar(1000) NULL,
        [ExpiresAt] datetime2 NULL,
        [PaidAt] datetime2 NULL,
        [Consumed] bit NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [LastCheckedAt] datetime2 NULL,
        [FailureCode] nvarchar(60) NULL,
        [Notes] nvarchar(500) NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(100) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_EntraxPayments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE INDEX [IX_EntraxApiCalls_CreationDate] ON [EntraxApiCalls] ([CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE INDEX [IX_EntraxApiCalls_ExternalPaymentId] ON [EntraxApiCalls] ([ExternalPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EntraxPayments_ExternalOrderId] ON [EntraxPayments] ([ExternalOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_EntraxPayments_ExternalPaymentId] ON [EntraxPayments] ([ExternalPaymentId]) WHERE [ExternalPaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE INDEX [IX_EntraxPayments_OrderId] ON [EntraxPayments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    CREATE INDEX [IX_EntraxPayments_Status_CreationDate] ON [EntraxPayments] ([Status], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_EntraxPayments_OrderId_Consumed] ON [EntraxPayments] ([OrderId]) WHERE [Consumed] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016045914_EntraxEnElAlta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016045914_EntraxEnElAlta', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016194619_IndiceResidualesPorBeneficiarioYFecha'
)
BEGIN
    CREATE INDEX [IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate] ON [DailyResidualEarnings] ([BeneficiaryMemberId], [EarnedDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016194619_IndiceResidualesPorBeneficiarioYFecha'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016194619_IndiceResidualesPorBeneficiarioYFecha', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016220023_IndiceResidualesConImporteYEstado'
)
BEGIN
    DROP INDEX [IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate] ON [DailyResidualEarnings];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016220023_IndiceResidualesConImporteYEstado'
)
BEGIN
    CREATE INDEX [IX_DailyResidualEarnings_BeneficiaryMemberId_EarnedDate] ON [DailyResidualEarnings] ([BeneficiaryMemberId], [EarnedDate]) INCLUDE ([Amount], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261016220023_IndiceResidualesConImporteYEstado'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261016220023_IndiceResidualesConImporteYEstado', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001044_IndicePatrocinadorYFechaDeAlta'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM [MemberProfiles] WHERE DATALENGTH([SponsorMemberId]) / 2 > 450)
        THROW 51001, N''MemberProfiles.SponsorMemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan J, Tarea 18).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001044_IndicePatrocinadorYFechaDeAlta'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MemberProfiles]') AND [c].[name] = N'SponsorMemberId');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [MemberProfiles] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [MemberProfiles] ALTER COLUMN [SponsorMemberId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001044_IndicePatrocinadorYFechaDeAlta'
)
BEGIN
    CREATE INDEX [IX_MemberProfiles_SponsorMemberId_EnrollDate] ON [MemberProfiles] ([SponsorMemberId], [EnrollDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001044_IndicePatrocinadorYFechaDeAlta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017001044_IndicePatrocinadorYFechaDeAlta', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001236_IndiceFechaDeAlta'
)
BEGIN
    CREATE INDEX [IX_MemberProfiles_EnrollDate] ON [MemberProfiles] ([EnrollDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017001236_IndiceFechaDeAlta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017001236_IndiceFechaDeAlta', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM [MemberStatusHistories] WHERE DATALENGTH([MemberId]) / 2 > 450)
        THROW 51003, N''MemberStatusHistories.MemberId tiene valores de mas de 450 caracteres: revisar antes de migrar (Plan J, Tarea 27).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MemberStatusHistories]') AND [c].[name] = N'MemberId');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [MemberStatusHistories] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [MemberStatusHistories] ALTER COLUMN [MemberId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    CREATE TABLE [MembershipStatusHistories] (
        [Id] bigint NOT NULL IDENTITY,
        [MemberId] nvarchar(450) NOT NULL,
        [SubscriptionId] nvarchar(450) NOT NULL,
        [MembershipLevelId] int NOT NULL,
        [OldStatus] int NULL,
        [NewStatus] int NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_MembershipStatusHistories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    CREATE INDEX [IX_MemberStatusHistories_ChangedAt] ON [MemberStatusHistories] ([ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    CREATE INDEX [IX_MemberStatusHistories_MemberId_ChangedAt] ON [MemberStatusHistories] ([MemberId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    CREATE INDEX [IX_MembershipStatusHistories_ChangedAt] ON [MembershipStatusHistories] ([ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    CREATE INDEX [IX_MembershipStatusHistories_MemberId_ChangedAt] ON [MembershipStatusHistories] ([MemberId], [ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017020312_HistorialDeEstados'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017020312_HistorialDeEstados', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017021319_MarcaMantenerCalificado'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [KeepQualified] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017021319_MarcaMantenerCalificado'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017021319_MarcaMantenerCalificado', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017043716_InscripcionesAEventos'
)
BEGIN
    CREATE TABLE [CorporateEventRegistrations] (
        [Id] bigint NOT NULL IDENTITY,
        [EventId] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(20) NOT NULL,
        [RegisteredAt] datetime2 NOT NULL,
        [Tickets] int NOT NULL,
        [Price] decimal(18,2) NULL,
        [Source] nvarchar(50) NOT NULL,
        [ExternalRef] nvarchar(100) NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CorporateEventRegistrations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CorporateEventRegistrations_CorporateEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [CorporateEvents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CorporateEventRegistrations_MemberProfiles_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [MemberProfiles] ([MemberId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017043716_InscripcionesAEventos'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CorporateEventRegistrations_EventId_ExternalRef] ON [CorporateEventRegistrations] ([EventId], [ExternalRef]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017043716_InscripcionesAEventos'
)
BEGIN
    CREATE INDEX [IX_CorporateEventRegistrations_EventId_MemberId] ON [CorporateEventRegistrations] ([EventId], [MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017043716_InscripcionesAEventos'
)
BEGIN
    CREATE INDEX [IX_CorporateEventRegistrations_MemberId] ON [CorporateEventRegistrations] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017043716_InscripcionesAEventos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017043716_InscripcionesAEventos', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'
    IF (SELECT COUNT(*) FROM [RecurringBillingPlans] WHERE [Name] = N''Lifestyle Ambassador (Annual)'') > 1
        THROW 51010, N''Hay mas de un plan Lifestyle Ambassador (Annual): dejar uno antes de migrar (Plan K, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [SubscriptionBillingStates] s
               JOIN [RecurringBillingPlans] p ON p.[Id] = s.[RecurringBillingPlanId]
               WHERE p.[Name] = N''Lifestyle Ambassador (Annual)'')
        THROW 51011, N''Hay estados de facturacion por suscripcion en el plan anual: revisarlos antes de migrar (Plan K, Tarea 5).'', 1;
    -- Anti-truncado: la columna se vuelve anulable manteniendo nvarchar(100). Si en esta base fuera mas
    -- ancha (alguien la amplio a mano), el ALTER la estrecharia: mejor parar.
    IF COL_LENGTH(N''SubscriptionBillingStates'', N''MembershipSubscriptionId'') = -1
       OR COL_LENGTH(N''SubscriptionBillingStates'', N''MembershipSubscriptionId'') > 200
        THROW 51012, N''SubscriptionBillingStates.MembershipSubscriptionId es mas ancha que nvarchar(100): revisar antes de migrar (Plan K, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [SubscriptionBillingStates] WHERE LEN([MembershipSubscriptionId]) > 100)
        THROW 51013, N''Hay MembershipSubscriptionId de mas de 100 caracteres: se truncarian (Plan K, Tarea 5).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    DROP INDEX [IX_SubscriptionBillingStates_MembershipSubscriptionId] ON [SubscriptionBillingStates];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SubscriptionBillingStates]') AND [c].[name] = N'MembershipSubscriptionId');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [SubscriptionBillingStates] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [SubscriptionBillingStates] ALTER COLUMN [MembershipSubscriptionId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    ALTER TABLE [SubscriptionBillingStates] ADD [Purpose] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    ALTER TABLE [RecurringBillingPlans] ADD [IsAnnualFeePlan] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'UPDATE [RecurringBillingPlans] SET [IsAnnualFeePlan] = 1 WHERE [Name] = N''Lifestyle Ambassador (Annual)'';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    ALTER TABLE [MemberProfiles] ADD [FeesFromBalance] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    CREATE TABLE [AnnualFeePayments] (
        [Id] nvarchar(450) NOT NULL,
        [MemberId] nvarchar(100) NOT NULL,
        [SubscriptionBillingStateId] nvarchar(450) NOT NULL,
        [CycleDueDate] datetime2 NOT NULL,
        [Amount] decimal(18,4) NOT NULL,
        [Method] int NOT NULL,
        [Source] int NOT NULL,
        [Status] int NOT NULL,
        [OrderId] nvarchar(450) NOT NULL,
        [PaymentHistoryId] nvarchar(450) NULL,
        [TokenTransactionId] bigint NULL,
        [TokenCode] nvarchar(64) NULL,
        [IdempotencyKey] nvarchar(64) NULL,
        [FailureCode] nvarchar(64) NULL,
        [Notes] nvarchar(500) NULL,
        [CompletedAt] datetime2 NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(256) NOT NULL,
        [LastUpdateDate] datetime2 NOT NULL,
        [LastUpdateBy] nvarchar(256) NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [DeletedBy] nvarchar(max) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_AnnualFeePayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AnnualFeePayments_SubscriptionBillingStates_SubscriptionBillingStateId] FOREIGN KEY ([SubscriptionBillingStateId]) REFERENCES [SubscriptionBillingStates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    CREATE INDEX [IX_AnnualFeePayments_MemberId_CreationDate] ON [AnnualFeePayments] ([MemberId], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AnnualFeePayments_OrderId] ON [AnnualFeePayments] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    CREATE INDEX [IX_AnnualFeePayments_Status_CreationDate] ON [AnnualFeePayments] ([Status], [CreationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AnnualFeePayments_LivePerCycle] ON [AnnualFeePayments] ([SubscriptionBillingStateId], [CycleDueDate]) WHERE [Status] IN (1, 2, 4)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_AnnualFeePayments_Member_IdempotencyKey] ON [AnnualFeePayments] ([MemberId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_SubscriptionBillingStates_MembershipSubscriptionId] ON [SubscriptionBillingStates] ([MembershipSubscriptionId]) WHERE [MembershipSubscriptionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_SubscriptionBillingStates_AnnualFeeMember] ON [SubscriptionBillingStates] ([MemberId]) WHERE [Purpose] = 2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_RecurringBillingPlans_AnnualFee] ON [RecurringBillingPlans] ([IsAnnualFeePlan]) WHERE [IsAnnualFeePlan] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    EXEC(N'ALTER TABLE [SubscriptionBillingStates] ADD CONSTRAINT [CK_SubscriptionBillingStates_Purpose] CHECK (([Purpose] = 1 AND [MembershipSubscriptionId] IS NOT NULL) OR ([Purpose] = 2 AND [MembershipSubscriptionId] IS NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017104209_CuotaAnual'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017104209_CuotaAnual', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([OrderNo]) / 2 > 40)
        THROW 51020, N''Orders.OrderNo tiene valores de mas de 40 caracteres (Plan M, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [Orders] WHERE [OrderNo] IS NOT NULL GROUP BY [OrderNo] HAVING COUNT(*) > 1)
        THROW 51021, N''Orders.OrderNo tiene duplicados: ver Scripts/sql/plan-m/01-orderno-duplicados.sql y R15 del Plan M.'', 1;
    IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([MemberId]) / 2 > 20 OR DATALENGTH([MembershipSubscriptionId]) / 2 > 450)
        THROW 51022, N''Orders.MemberId o MembershipSubscriptionId no caben (Plan M, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [OrderDetails] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([ProductId]) / 2 > 450)
        THROW 51023, N''OrderDetails.OrderId o ProductId no caben (Plan M, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [OrderDetails] d WHERE NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = d.[OrderId]))
        THROW 51024, N''OrderDetails tiene lineas sin pedido: no se puede crear FK_OrderDetails_Orders_OrderId.'', 1;
    IF EXISTS (SELECT 1 FROM [PaymentHistories] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([MemberId]) / 2 > 100 OR DATALENGTH([GatewayTransactionId]) / 2 > 200)
        THROW 51025, N''PaymentHistories.OrderId, MemberId o GatewayTransactionId no caben (Plan M, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] WHERE DATALENGTH([LastOrderId]) / 2 > 450)
        THROW 51026, N''MembershipSubscriptions.LastOrderId no cabe (Plan M, Tarea 5).'', 1;
    IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] s WHERE s.[LastOrderId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = s.[LastOrderId]))
        THROW 51027, N''MembershipSubscriptions.LastOrderId apunta a pedidos que no existen: no se puede crear la FK.'', 1;
    IF EXISTS (SELECT 1 FROM [MemberStatistics] WHERE DATALENGTH([MemberId]) / 2 > 100)
        THROW 51028, N''MemberStatistics.MemberId no cabe (Plan M, Tarea 5).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT [FK_MembershipSubscriptions_Orders_LastOrderId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    ALTER TABLE [OrderDetails] DROP CONSTRAINT [FK_OrderDetails_Orders_OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    ALTER TABLE [PaymentHistories] DROP CONSTRAINT [FK_PaymentHistories_Orders_OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DROP INDEX [IX_PaymentHistories_OrdersId] ON [PaymentHistories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DROP INDEX [IX_OrderDetails_OrdersId] ON [OrderDetails];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DROP INDEX [IX_MembershipSubscriptions_LastOrderId1] ON [MembershipSubscriptions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    EXEC(N'DROP INDEX IF EXISTS [IX_DualTeamTree_ParentMemberId] ON [DualTeamTree];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'OrdersId');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [PaymentHistories] DROP COLUMN [OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'OrdersId');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [OrderDetails] DROP COLUMN [OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MembershipSubscriptions]') AND [c].[name] = N'LastOrderId1');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [MembershipSubscriptions] DROP COLUMN [LastOrderId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'OrderId');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [OrderId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var32 nvarchar(max);
    SELECT @var32 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'MemberId');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var32 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [MemberId] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var33 nvarchar(max);
    SELECT @var33 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'GatewayTransactionId');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var33 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [GatewayTransactionId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var34 nvarchar(max);
    SELECT @var34 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'OrderNo');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var34 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [OrderNo] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var35 nvarchar(max);
    SELECT @var35 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'MembershipSubscriptionId');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var35 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [MembershipSubscriptionId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var36 nvarchar(max);
    SELECT @var36 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'MemberId');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var36 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [MemberId] nvarchar(20) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var37 nvarchar(max);
    SELECT @var37 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'ProductId');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var37 + ';');
    ALTER TABLE [OrderDetails] ALTER COLUMN [ProductId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var38 nvarchar(max);
    SELECT @var38 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'OrderId');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var38 + ';');
    ALTER TABLE [OrderDetails] ALTER COLUMN [OrderId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var39 nvarchar(max);
    SELECT @var39 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MemberStatistics]') AND [c].[name] = N'MemberId');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [MemberStatistics] DROP CONSTRAINT ' + @var39 + ';');
    ALTER TABLE [MemberStatistics] ALTER COLUMN [MemberId] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var40 nvarchar(max);
    SELECT @var40 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MembershipSubscriptions]') AND [c].[name] = N'LastOrderId');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT ' + @var40 + ';');
    ALTER TABLE [MembershipSubscriptions] ALTER COLUMN [LastOrderId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_PaymentHistories_GatewayTransactionId] ON [PaymentHistories] ([GatewayTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_PaymentHistories_MemberId_TransactionStatus] ON [PaymentHistories] ([MemberId], [TransactionStatus]) INCLUDE ([ProcessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_PaymentHistories_OrderId] ON [PaymentHistories] ([OrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_Orders_MemberId_OrderDate] ON [Orders] ([MemberId], [OrderDate]) INCLUDE ([Status], [CreationDate], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_Orders_MembershipSubscriptionId] ON [Orders] ([MembershipSubscriptionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_Orders_OrderDate] ON [Orders] ([OrderDate]) INCLUDE ([Status], [TotalAmount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Orders_OrderNo] ON [Orders] ([OrderNo]) WHERE [OrderNo] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_OrderId] ON [OrderDetails] ([OrderId]) INCLUDE ([ProductId], [Quantity], [UnitPrice]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_MemberStatistics_MemberId] ON [MemberStatistics] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_MembershipSubscriptions_LastOrderId] ON [MembershipSubscriptions] ([LastOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    CREATE INDEX [IX_DualTeamTree_ParentMemberId] ON [DualTeamTree] ([ParentMemberId]) INCLUDE ([MemberId], [Side], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD CONSTRAINT [FK_MembershipSubscriptions_Orders_LastOrderId] FOREIGN KEY ([LastOrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    ALTER TABLE [OrderDetails] ADD CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017171000_PedidosAcotadosEIndices', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM [GenealogyTree] WHERE DATALENGTH([Id]) / 2 > 100)
        THROW 51033, N''GenealogyTree.Id tiene valores de mas de 100 caracteres: no caben en NodeNullKey (Plan M, Tarea 9).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    EXEC(N'DROP INDEX IF EXISTS [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    ALTER TABLE [GenealogyTree] ADD [Node] hierarchyid NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD [NodeLevel] AS [Node].GetLevel() PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD [NodeNullKey] AS CASE WHEN [Node] IS NULL THEN CAST([Id] AS nvarchar(100)) END PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GenealogyTree_Node] ON [GenealogyTree] ([Node], [NodeNullKey]) INCLUDE ([MemberId], [Level], [CreationDate], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_NodeLevel_Node] ON [GenealogyTree] ([NodeLevel], [Node]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree] ([ParentMemberId]) INCLUDE ([Node]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD CONSTRAINT [CK_GenealogyTree_Node_OrdinalesEnteros] CHECK ([Node] IS NULL OR CHARINDEX(''.'', [Node].ToString()) = 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017233430_NodoDeGenealogia'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017233430_NodoDeGenealogia', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018123738_HuecoUnicoDelBinario'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM [DualTeamTree] WHERE [IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL
               GROUP BY [ParentMemberId], [Side] HAVING COUNT(*) > 1)
        THROW 51048, N''DualTeamTree tiene huecos repetidos: ejecutar Scripts/sql/plan-p/01-reparar-huecos-binario.sql (Plan P, Tarea 3).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018123738_HuecoUnicoDelBinario'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DualTeamTree_ParentMemberId_Side] ON [DualTeamTree] ([ParentMemberId], [Side]) WHERE [IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018123738_HuecoUnicoDelBinario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018123738_HuecoUnicoDelBinario', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    CREATE SEQUENCE [DualTeamRootOrdinal] AS int START WITH 1 INCREMENT BY 1 NO CYCLE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [BinaryPath] varbinary(1700) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [Depth] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [LeftLegCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [RightLegCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_DualTeamTree_BinaryPath] ON [DualTeamTree] ([BinaryPath]) INCLUDE ([MemberId], [Depth], [ParentMemberId], [Side]) WHERE [IsDeleted] = 0 AND [BinaryPath] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.DualChildKey(@p varbinary(1700), @depth int, @side int)
    RETURNS TABLE AS RETURN
    WITH a AS (
        SELECT 4 + @depth / 8 + 1        AS bi,   -- byte (base 1) del terminador del padre = bit del lado
               7 - @depth % 8            AS si,
               4 + (@depth + 1) / 8 + 1  AS bj,   -- byte del terminador del hijo
               7 - (@depth + 1) % 8      AS sj)
    SELECT CAST(
        SUBSTRING(@p, 1, a.bi - 1)
        + CASE WHEN a.bi = a.bj
               THEN CAST(CAST(((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                               | POWER(2, a.sj)) AS binary(1)) AS varbinary(1))
               ELSE CAST(CAST((CAST(SUBSTRING(@p, a.bi, 1) AS int) & ~(CASE WHEN @side = 0 THEN POWER(2, a.si) ELSE 0 END))
                              AS binary(1)) AS varbinary(1)) + 0x80
          END AS varbinary(1700)) AS ChildKey
    FROM a;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261018142343_ClaveCompactaDelBinario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018142343_ClaveCompactaDelBinario', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019180006_SuscripcionesActivasPorMiembro'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_MembershipSubscriptions_Activas] ON [MembershipSubscriptions] ([MemberId]) WHERE [SubscriptionStatus] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019180006_SuscripcionesActivasPorMiembro'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019180006_SuscripcionesActivasPorMiembro', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019192507_ColaDeDeltasDePierna'
)
BEGIN
    CREATE TABLE [DualLegDeltas] (
        [Id] bigint NOT NULL IDENTITY,
        [StartMemberId] nvarchar(450) NOT NULL,
        [Side] int NOT NULL,
        [Points] decimal(18,4) NOT NULL,
        [NodeCount] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_DualLegDeltas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019192507_ColaDeDeltasDePierna'
)
BEGIN
    CREATE INDEX [IX_DualLegDeltas_StartMemberId_Id] ON [DualLegDeltas] ([StartMemberId], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019192507_ColaDeDeltasDePierna'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.Numeros(@hasta int)
    RETURNS TABLE AS RETURN
    WITH e1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
         e5(n) AS (SELECT 1 FROM e1 a CROSS JOIN e1 b CROSS JOIN e1 c CROSS JOIN e1 d CROSS JOIN e1 e)
    SELECT TOP (CASE WHEN @hasta IS NULL OR @hasta < 0 THEN 0 ELSE @hasta + 1 END)
           CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS int) AS N
    FROM e5;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019192507_ColaDeDeltasDePierna'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.DualAncestorKeys(@k varbinary(1700), @ladoPropio int)
    RETURNS TABLE AS RETURN
    WITH t AS (
        SELECT DATALENGTH(@k) AS L, CAST(SUBSTRING(@k, DATALENGTH(@k), 1) AS int) AS U),
    d AS (
        SELECT (t.L - 5) * 8 + 7 - CASE WHEN t.U & 1 > 0 THEN 0 WHEN t.U & 2 > 0 THEN 1 WHEN t.U & 4 > 0 THEN 2
                                        WHEN t.U & 8 > 0 THEN 3 WHEN t.U & 16 > 0 THEN 4 WHEN t.U & 32 > 0 THEN 5
                                        WHEN t.U & 64 > 0 THEN 6 ELSE 7 END AS D
        FROM t WHERE t.L >= 5),
    b AS (
        SELECT n.N AS J, d.D, CAST(SUBSTRING(@k, 5 + n.N / 8, 1) AS int) AS B, n.N % 8 AS M
        FROM d CROSS APPLY dbo.Numeros(d.D) n)
    SELECT b.J AS Depth,
           CAST(SUBSTRING(@k, 1, 4 + b.J / 8)
                + CAST(CAST((b.B & (256 - POWER(2, 8 - b.M))) | POWER(2, 7 - b.M) AS binary(1)) AS varbinary(1))
                AS varbinary(1700)) AS AncestorKey,
           CASE WHEN b.J = b.D THEN @ladoPropio
                WHEN b.B & POWER(2, 7 - b.M) <> 0 THEN 1 ELSE 0 END AS Side
    FROM b;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019192507_ColaDeDeltasDePierna'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019192507_ColaDeDeltasDePierna', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [LeftLegQualified] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [RightLegQualified] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    ALTER TABLE [DualTeamTree] ADD [SelfQualified] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    ALTER TABLE [DualLegDeltas] ADD [OwnChange] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    ALTER TABLE [DualLegDeltas] ADD [QualifiedCount] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019201124_CalificadosDelBinario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019201124_CalificadosDelBinario', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    ALTER TABLE [DualLegDeltas] ADD [Attempts] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    CREATE TABLE [DualLegDeltaDeadLetters] (
        [Id] bigint NOT NULL IDENTITY,
        [DeltaId] bigint NOT NULL,
        [StartMemberId] nvarchar(450) NOT NULL,
        [Side] int NOT NULL,
        [Points] decimal(18,4) NOT NULL,
        [NodeCount] int NOT NULL,
        [QualifiedCount] int NOT NULL,
        [OwnChange] bit NOT NULL,
        [Attempts] int NOT NULL,
        [CreationDate] datetime2 NOT NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [DiscardedDate] datetime2 NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_DualLegDeltaDeadLetters] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    CREATE INDEX [IX_DualLegDeltaDeadLetters_StartMemberId] ON [DualLegDeltaDeadLetters] ([StartMemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.NumerosEntre(@desde int, @hasta int)
    RETURNS TABLE AS RETURN
    WITH e1(n) AS (SELECT 1 FROM (VALUES (1),(1),(1),(1),(1),(1),(1),(1),(1),(1)) v(n)),
         e5(n) AS (SELECT 1 FROM e1 a CROSS JOIN e1 b CROSS JOIN e1 c CROSS JOIN e1 d CROSS JOIN e1 e)
    SELECT TOP (CASE WHEN @desde IS NULL OR @hasta IS NULL OR @desde < 0 OR @hasta < @desde THEN 0 ELSE @hasta - @desde + 1 END)
           CAST(@desde + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS int) AS N
    FROM e5;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.DualKeyDepth(@k varbinary(1700))
    RETURNS TABLE AS RETURN
    SELECT (t.L - 5) * 8 + 7 - CASE WHEN t.U & 1 > 0 THEN 0 WHEN t.U & 2 > 0 THEN 1 WHEN t.U & 4 > 0 THEN 2
                                    WHEN t.U & 8 > 0 THEN 3 WHEN t.U & 16 > 0 THEN 4 WHEN t.U & 32 > 0 THEN 5
                                    WHEN t.U & 64 > 0 THEN 6 ELSE 7 END AS D
    FROM (SELECT DATALENGTH(@k) AS L, CAST(SUBSTRING(@k, DATALENGTH(@k), 1) AS int) AS U) t
    WHERE t.L >= 5;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.DualPathKeys(@k varbinary(1700), @ladoPropio int, @desde int)
    RETURNS TABLE AS RETURN
    WITH b AS (
        SELECT n.N AS J, d.D, CAST(SUBSTRING(@k, 5 + n.N / 8, 1) AS int) AS B, n.N % 8 AS M
        FROM dbo.DualKeyDepth(@k) d
        CROSS APPLY dbo.NumerosEntre(CASE WHEN @desde IS NULL OR @desde < 0 THEN 0 ELSE @desde END, d.D) n)
    SELECT b.J AS Depth,
           CAST(SUBSTRING(@k, 1, 4 + b.J / 8)
                + CAST(CAST((b.B & (256 - POWER(2, 8 - b.M))) | POWER(2, 7 - b.M) AS binary(1)) AS varbinary(1))
                AS varbinary(1700)) AS AncestorKey,
           CASE WHEN b.J = b.D THEN @ladoPropio
                WHEN b.B & POWER(2, 7 - b.M) <> 0 THEN 1 ELSE 0 END AS Side,
           CAST(CASE WHEN b.J = b.D THEN 1 ELSE 0 END AS bit) AS IsSelf
    FROM b;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.DualAncestorKeys(@k varbinary(1700), @ladoPropio int)
    RETURNS TABLE AS RETURN
    SELECT p.Depth, p.AncestorKey, p.Side, p.IsSelf FROM dbo.DualPathKeys(@k, @ladoPropio, 0) p;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019222602_VaciadoPorUnionDeCaminos'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019222602_VaciadoPorUnionDeCaminos', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230616_SuscripcionesSinCascada'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230616_SuscripcionesSinCascada'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230616_SuscripcionesSinCascada'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [MemberProfiles] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230616_SuscripcionesSinCascada'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230616_SuscripcionesSinCascada'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019230616_SuscripcionesSinCascada', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    EXEC(N'
    UPDATE MembershipSubscriptions SET DualTeamContribution = 0, EnrollmentContribution = 0
    WHERE DualTeamContribution <> 0 OR EnrollmentContribution <> 0;
    WITH a AS (
        SELECT s.Id, ROW_NUMBER() OVER (PARTITION BY s.MemberId ORDER BY s.MembershipLevelId DESC, s.Id) AS Rn
        FROM MembershipSubscriptions s WHERE s.SubscriptionStatus = 1)
    UPDATE s SET DualTeamContribution = p.PP, EnrollmentContribution = p.PP
    FROM MembershipSubscriptions s
    JOIN a ON a.Id = s.Id AND a.Rn = 1
    CROSS APPLY (SELECT TOP 1 ms.PersonalPoints AS PP FROM MemberStatistics ms WHERE ms.MemberId = s.MemberId ORDER BY ms.Id) p
    WHERE p.PP <> 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD CONSTRAINT [FK_MembershipSubscriptions_MemberProfiles_MemberId] FOREIGN KEY ([MemberId]) REFERENCES [MemberProfiles] ([MemberId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD CONSTRAINT [FK_MembershipSubscriptions_MembershipLevels_MembershipLevelId] FOREIGN KEY ([MembershipLevelId]) REFERENCES [MembershipLevels] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019230647_PuntosPorMembresiaActiva'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019230647_PuntosPorMembresiaActiva', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020071823_RetiraRutaDelBinario'
)
BEGIN
    EXEC(N'
    DECLARE @modulo nvarchar(400);
    DECLARE candidatos CURSOR LOCAL FAST_FORWARD FOR
        SELECT QUOTENAME(OBJECT_SCHEMA_NAME(m.object_id)) + N''.'' + QUOTENAME(OBJECT_NAME(m.object_id))
        FROM sys.sql_modules m
        JOIN sys.objects o ON o.object_id = m.object_id
        WHERE o.is_ms_shipped = 0
          AND m.definition LIKE N''%DualTeamTree%'' AND m.definition LIKE N''%HierarchyPath%'';
    OPEN candidatos;
    FETCH NEXT FROM candidatos INTO @modulo;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.dm_sql_referenced_entities(@modulo, N''OBJECT'') r
                   WHERE r.referenced_entity_name = N''DualTeamTree'' AND r.referenced_minor_name = N''HierarchyPath'')
        BEGIN
            DECLARE @msg nvarchar(600) = CONCAT(N''El objeto '', @modulo, N'' aun lee DualTeamTree.HierarchyPath (Plan Q, Tarea 13).'');
            THROW 51100, @msg, 1;
        END
        FETCH NEXT FROM candidatos INTO @modulo;
    END
    CLOSE candidatos;
    DEALLOCATE candidatos;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020071823_RetiraRutaDelBinario'
)
BEGIN
    DECLARE @var41 nvarchar(max);
    SELECT @var41 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DualTeamTree]') AND [c].[name] = N'HierarchyPath');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [DualTeamTree] DROP CONSTRAINT ' + @var41 + ';');
    ALTER TABLE [DualTeamTree] DROP COLUMN [HierarchyPath];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020071823_RetiraRutaDelBinario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020071823_RetiraRutaDelBinario', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094634_DeltaPorLineaDePatrocinio'
)
BEGIN
    ALTER TABLE [MemberStatisticDeltas] ADD [Scope] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094634_DeltaPorLineaDePatrocinio'
)
BEGIN
    EXEC(N'
    CREATE OR ALTER FUNCTION dbo.GenealogyAncestors(@node hierarchyid)
    RETURNS TABLE AS RETURN
    SELECT n.N AS Distancia, @node.GetAncestor(n.N) AS Node
    FROM dbo.Numeros(@node.GetLevel() - 1) n;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020094634_DeltaPorLineaDePatrocinio'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020094634_DeltaPorLineaDePatrocinio', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020104121_CalificadosDeInscripcion'
)
BEGIN
    ALTER TABLE [MemberStatistics] ADD [EnrollmentQualifiedTeamMembers] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020104121_CalificadosDeInscripcion'
)
BEGIN
    ALTER TABLE [MemberStatisticDeltas] ADD [EnrollmentQualifiedTeamMembersDelta] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020104121_CalificadosDeInscripcion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020104121_CalificadosDeInscripcion', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020130943_SuscripcionRetenidaPorLaCuenta'
)
BEGIN
    ALTER TABLE [MembershipSubscriptions] ADD [HeldByAccountStatus] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020130943_SuscripcionRetenidaPorLaCuenta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020130943_SuscripcionRetenidaPorLaCuenta', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020161053_UnNodoPorMiembro'
)
BEGIN
    EXEC(N'
    IF EXISTS (SELECT 1 FROM GenealogyTree WHERE IsDeleted = 0 GROUP BY MemberId HAVING COUNT(*) > 1)
        THROW 51108, N''GenealogyTree tiene miembros con mas de un nodo vivo: ejecutar antes Scripts/sql/plan-q/01-nodos-de-inscripcion-repetidos.sql (Plan Q, Tarea 21).'', 1;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020161053_UnNodoPorMiembro'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_GenealogyTree_MemberId_Vivo] ON [GenealogyTree] ([MemberId]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020161053_UnNodoPorMiembro'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020161053_UnNodoPorMiembro', N'10.0.11');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    EXEC(N'
    IF COL_LENGTH(N''dbo.MemberProfiles'', N''SignupMembershipLevelId'') IS NULL
        ALTER TABLE [MemberProfiles] ADD [SignupMembershipLevelId] int NULL;
    IF COL_LENGTH(N''dbo.MemberProfiles'', N''SearchName'') IS NULL
        ALTER TABLE [MemberProfiles] ADD [SearchName] AS CAST(CONCAT([FirstName], N'' '', [LastName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI;
    IF COL_LENGTH(N''dbo.MemberProfiles'', N''SearchNameReversed'') IS NULL
        ALTER TABLE [MemberProfiles] ADD [SearchNameReversed] AS CAST(CONCAT([LastName], N'' '', [FirstName]) AS nvarchar(201)) COLLATE Latin1_General_100_CI_AI;');
END;

COMMIT;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.MemberProfiles'') AND name = N''IX_MemberProfiles_Clientes'')
    BEGIN
        DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY(''EngineEdition'') AS int) IN (3, 5, 8) THEN N''ON'' ELSE N''OFF'' END;
        DECLARE @sql nvarchar(max) = N''CREATE INDEX [IX_MemberProfiles_Clientes] ON [MemberProfiles] ([MemberId] ASC) INCLUDE ([EnrollDate], [SponsorMemberId]) WHERE [MemberType] = 1 AND [IsDeleted] = 0 WITH (ONLINE = '' + @online + N'')'';
        EXEC (@sql);
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.MemberProfiles'') AND name = N''IX_MemberProfiles_SearchName'')
    BEGIN
        DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY(''EngineEdition'') AS int) IN (3, 5, 8) THEN N''ON'' ELSE N''OFF'' END;
        DECLARE @sql nvarchar(max) = N''CREATE INDEX [IX_MemberProfiles_SearchName] ON [MemberProfiles] ([SearchName] ASC) INCLUDE ([MemberId]) WHERE [IsDeleted] = 0 WITH (ONLINE = '' + @online + N'')'';
        EXEC (@sql);
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.MemberProfiles'') AND name = N''IX_MemberProfiles_SearchNameReversed'')
    BEGIN
        DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY(''EngineEdition'') AS int) IN (3, 5, 8) THEN N''ON'' ELSE N''OFF'' END;
        DECLARE @sql nvarchar(max) = N''CREATE INDEX [IX_MemberProfiles_SearchNameReversed] ON [MemberProfiles] ([SearchNameReversed] ASC) INCLUDE ([MemberId]) WHERE [IsDeleted] = 0 WITH (ONLINE = '' + @online + N'')'';
        EXEC (@sql);
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic
                   JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                   WHERE i.object_id = OBJECT_ID(N''dbo.MemberProfiles'') AND i.name = N''IX_MemberProfiles_EnrollDate''
                     AND COL_NAME(ic.object_id, ic.column_id) = N''SignupMembershipLevelId'')
    BEGIN
        DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY(''EngineEdition'') AS int) IN (3, 5, 8) THEN N''ON'' ELSE N''OFF'' END;
        DECLARE @sql nvarchar(max) = N''CREATE INDEX [IX_MemberProfiles_EnrollDate] ON [MemberProfiles] ([EnrollDate] ASC) ''
            + N''INCLUDE ([SponsorMemberId], [MemberType], [SignupMembershipLevelId]) ''
            + CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.MemberProfiles'') AND name = N''IX_MemberProfiles_EnrollDate'')
                   THEN N''WITH (DROP_EXISTING = ON, ONLINE = '' + @online + N'')''
                   ELSE N''WITH (ONLINE = '' + @online + N'')'' END;
        EXEC (@sql);
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020173755_BusquedaYNivelDeAlta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020173755_BusquedaYNivelDeAlta', N'10.0.11');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020235015_ConteoDelEquipoDeInscripcion'
)
BEGIN
    EXEC(N'
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.GenealogyTree'') AND name = N''IX_GenealogyTree_Node_Vivo'')
    BEGIN
        DECLARE @online nvarchar(3) = CASE WHEN CAST(SERVERPROPERTY(''EngineEdition'') AS int) IN (3, 5, 8) THEN N''ON'' ELSE N''OFF'' END;
        DECLARE @sql nvarchar(max) = N''CREATE INDEX [IX_GenealogyTree_Node_Vivo] ON [GenealogyTree] ([Node] ASC) WHERE [IsDeleted] = 0 WITH (ONLINE = '' + @online + N'')'';
        EXEC (@sql);
    END');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020235015_ConteoDelEquipoDeInscripcion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020235015_ConteoDelEquipoDeInscripcion', N'10.0.11');
END;
GO

