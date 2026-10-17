BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([OrderNo]) / 2 > 40)
        THROW 51020, N'Orders.OrderNo tiene valores de mas de 40 caracteres (Plan M, Tarea 5).', 1;
    IF EXISTS (SELECT 1 FROM [Orders] WHERE [OrderNo] IS NOT NULL GROUP BY [OrderNo] HAVING COUNT(*) > 1)
        THROW 51021, N'Orders.OrderNo tiene duplicados: ver Scripts/sql/plan-m/01-orderno-duplicados.sql y R15 del Plan M.', 1;
    IF EXISTS (SELECT 1 FROM [Orders] WHERE DATALENGTH([MemberId]) / 2 > 20 OR DATALENGTH([MembershipSubscriptionId]) / 2 > 450)
        THROW 51022, N'Orders.MemberId o MembershipSubscriptionId no caben (Plan M, Tarea 5).', 1;
    IF EXISTS (SELECT 1 FROM [OrderDetails] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([ProductId]) / 2 > 450)
        THROW 51023, N'OrderDetails.OrderId o ProductId no caben (Plan M, Tarea 5).', 1;
    IF EXISTS (SELECT 1 FROM [OrderDetails] d WHERE NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = d.[OrderId]))
        THROW 51024, N'OrderDetails tiene lineas sin pedido: no se puede crear FK_OrderDetails_Orders_OrderId.', 1;
    IF EXISTS (SELECT 1 FROM [PaymentHistories] WHERE DATALENGTH([OrderId]) / 2 > 450 OR DATALENGTH([MemberId]) / 2 > 100 OR DATALENGTH([GatewayTransactionId]) / 2 > 200)
        THROW 51025, N'PaymentHistories.OrderId, MemberId o GatewayTransactionId no caben (Plan M, Tarea 5).', 1;
    IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] WHERE DATALENGTH([LastOrderId]) / 2 > 450)
        THROW 51026, N'MembershipSubscriptions.LastOrderId no cabe (Plan M, Tarea 5).', 1;
    IF EXISTS (SELECT 1 FROM [MembershipSubscriptions] s WHERE s.[LastOrderId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [Orders] o WHERE o.[Id] = s.[LastOrderId]))
        THROW 51027, N'MembershipSubscriptions.LastOrderId apunta a pedidos que no existen: no se puede crear la FK.', 1;
    IF EXISTS (SELECT 1 FROM [MemberStatistics] WHERE DATALENGTH([MemberId]) / 2 > 100)
        THROW 51028, N'MemberStatistics.MemberId no cabe (Plan M, Tarea 5).', 1;
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
    DROP INDEX IF EXISTS [IX_DualTeamTree_ParentMemberId] ON [DualTeamTree];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'OrdersId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [PaymentHistories] DROP COLUMN [OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'OrdersId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [OrderDetails] DROP COLUMN [OrdersId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MembershipSubscriptions]') AND [c].[name] = N'LastOrderId1');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [MembershipSubscriptions] DROP COLUMN [LastOrderId1];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'OrderId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [OrderId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'MemberId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [MemberId] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PaymentHistories]') AND [c].[name] = N'GatewayTransactionId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [PaymentHistories] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [PaymentHistories] ALTER COLUMN [GatewayTransactionId] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'OrderNo');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [OrderNo] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'MembershipSubscriptionId');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [MembershipSubscriptionId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Orders]') AND [c].[name] = N'MemberId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Orders] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [Orders] ALTER COLUMN [MemberId] nvarchar(20) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'ProductId');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [OrderDetails] ALTER COLUMN [ProductId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrderDetails]') AND [c].[name] = N'OrderId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [OrderDetails] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [OrderDetails] ALTER COLUMN [OrderId] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MemberStatistics]') AND [c].[name] = N'MemberId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [MemberStatistics] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [MemberStatistics] ALTER COLUMN [MemberId] nvarchar(100) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017171000_PedidosAcotadosEIndices'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MembershipSubscriptions]') AND [c].[name] = N'LastOrderId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [MembershipSubscriptions] DROP CONSTRAINT ' + @var12 + ';');
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
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN

    IF EXISTS (SELECT 1 FROM [GenealogyTree] WHERE DATALENGTH([Id]) / 2 > 100)
        THROW 51033, N'GenealogyTree.Id tiene valores de mas de 100 caracteres: no caben en NodeNullKey (Plan M, Tarea 9).', 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    DROP INDEX [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    ALTER TABLE [GenealogyTree] ADD [Node] hierarchyid NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD [NodeLevel] AS [Node].GetLevel() PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD [NodeNullKey] AS CASE WHEN [Node] IS NULL THEN CAST([Id] AS nvarchar(100)) END PERSISTED');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GenealogyTree_Node] ON [GenealogyTree] ([Node], [NodeNullKey]) INCLUDE ([MemberId], [Level], [CreationDate], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_NodeLevel_Node] ON [GenealogyTree] ([NodeLevel], [Node]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    CREATE INDEX [IX_GenealogyTree_ParentMemberId] ON [GenealogyTree] ([ParentMemberId]) INCLUDE ([Node]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    EXEC(N'ALTER TABLE [GenealogyTree] ADD CONSTRAINT [CK_GenealogyTree_Node_OrdinalesEnteros] CHECK ([Node] IS NULL OR CHARINDEX(''.'', [Node].ToString()) = 0)');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261017215312_NodoDeGenealogia'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261017215312_NodoDeGenealogia', N'10.0.11');
END;

COMMIT;
GO

