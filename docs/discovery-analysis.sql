BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    ALTER TABLE [Rooms] ADD [ScoringVersion] varchar(32) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    ALTER TABLE [RoomQuestions] ADD [TemplateKey] varchar(48) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    ALTER TABLE [RoomQuestions] ADD [TemplateVersion] varchar(32) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    CREATE TABLE [DiscoveryProfiles] (
        [UserId] uniqueidentifier NOT NULL,
        [Enabled] bit NOT NULL,
        [AllowComparison] bit NOT NULL,
        [AllowRelationships] bit NOT NULL,
        [AllowMbti] bit NOT NULL,
        [ComparisonCode] varchar(24) NULL,
        [Version] uniqueidentifier NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DiscoveryProfiles] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_DiscoveryProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    CREATE TABLE [AnalysisDecisions] (
        [UserId] uniqueidentifier NOT NULL,
        [ResultKey] varchar(80) NOT NULL,
        [State] int NULL,
        [Correction] varchar(4) NULL,
        [ShareCode] varchar(24) NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [Revision] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AnalysisDecisions] PRIMARY KEY ([UserId], [ResultKey]),
        CONSTRAINT [FK_AnalysisDecisions_DiscoveryProfiles_UserId] FOREIGN KEY ([UserId]) REFERENCES [DiscoveryProfiles] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    CREATE TABLE [DiscoveryAnswers] (
        [UserId] uniqueidentifier NOT NULL,
        [QuestionKey] varchar(48) NOT NULL,
        [QuestionVersion] varchar(32) NOT NULL,
        [Prompt] nvarchar(200) NOT NULL,
        [OptionIndex] int NULL,
        [Value] nvarchar(100) NULL,
        [IsSkipped] bit NOT NULL,
        [IsSensitive] bit NOT NULL,
        [AnsweredAt] datetime2 NOT NULL,
        [Revision] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DiscoveryAnswers] PRIMARY KEY ([UserId], [QuestionKey]),
        CONSTRAINT [FK_DiscoveryAnswers_DiscoveryProfiles_UserId] FOREIGN KEY ([UserId]) REFERENCES [DiscoveryProfiles] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AnalysisDecisions_ShareCode] ON [AnalysisDecisions] ([ShareCode]) WHERE [ShareCode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_DiscoveryProfiles_ComparisonCode] ON [DiscoveryProfiles] ([ComparisonCode]) WHERE [ComparisonCode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261021133911_DiscoveryAnalysis'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261021133911_DiscoveryAnalysis', N'8.0.31');
END;
GO

COMMIT;
GO

