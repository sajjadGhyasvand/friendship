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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(40) NOT NULL,
        [Icon] nvarchar(8) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [ErrorLogs] (
        [Id] bigint NOT NULL IDENTITY,
        [At] datetime2 NOT NULL,
        [Level] nvarchar(20) NOT NULL,
        [Category] nvarchar(200) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [Exception] nvarchar(max) NULL,
        CONSTRAINT [PK_ErrorLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [PublicId] varchar(10) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(30) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [Role] int NOT NULL,
        [AvatarKey] varchar(32) NOT NULL,
        [Bio] nvarchar(160) NULL,
        [IsProfilePublic] bit NOT NULL,
        [IsBanned] bit NOT NULL,
        [BanReason] nvarchar(200) NULL,
        [SecurityStamp] varchar(32) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Questions] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [Text] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Questions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Questions_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Achievements] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Key] varchar(50) NOT NULL,
        [UnlockedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Achievements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Achievements_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Rooms] (
        [Id] uniqueidentifier NOT NULL,
        [Code] varchar(6) NOT NULL,
        [OwnerUserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(60) NOT NULL,
        [Mode] int NOT NULL,
        [QuestionCount] int NOT NULL,
        [TimeLimitSeconds] int NULL,
        [MaxParticipants] int NOT NULL,
        [RevealMode] int NOT NULL,
        [ShowLeaderboard] bit NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [OpenedAt] datetime2 NULL,
        [EndedAt] datetime2 NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [Version] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Rooms] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Rooms_Users_OwnerUserId] FOREIGN KEY ([OwnerUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [QuestionOptions] (
        [Id] int NOT NULL IDENTITY,
        [QuestionId] int NOT NULL,
        [Text] nvarchar(80) NOT NULL,
        [Order] int NOT NULL,
        CONSTRAINT [PK_QuestionOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QuestionOptions_Questions_QuestionId] FOREIGN KEY ([QuestionId]) REFERENCES [Questions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Participants] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [VisitorId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [DisplayName] nvarchar(30) NOT NULL,
        [AvatarKey] varchar(32) NOT NULL,
        [ShareCode] varchar(10) NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [TotalQuestions] int NOT NULL,
        [CorrectCount] int NOT NULL,
        [Score] int NOT NULL,
        [KnowledgePercent] int NOT NULL,
        CONSTRAINT [PK_Participants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Participants_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]),
        CONSTRAINT [FK_Participants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [Reports] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [ReporterUserId] uniqueidentifier NULL,
        [ReporterVisitorId] uniqueidentifier NOT NULL,
        [Reason] nvarchar(300) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [HandledAt] datetime2 NULL,
        [HandledByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Reports_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]),
        CONSTRAINT [FK_Reports_Users_HandledByUserId] FOREIGN KEY ([HandledByUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Reports_Users_ReporterUserId] FOREIGN KEY ([ReporterUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [RoomQuestions] (
        [Id] uniqueidentifier NOT NULL,
        [RoomId] uniqueidentifier NOT NULL,
        [SourceQuestionId] int NOT NULL,
        [Order] int NOT NULL,
        [Text] nvarchar(200) NOT NULL,
        [CategoryName] nvarchar(40) NOT NULL,
        [CategoryIcon] nvarchar(8) NOT NULL,
        [OwnerAnswerOptionId] uniqueidentifier NULL,
        CONSTRAINT [PK_RoomQuestions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomQuestions_Questions_SourceQuestionId] FOREIGN KEY ([SourceQuestionId]) REFERENCES [Questions] ([Id]),
        CONSTRAINT [FK_RoomQuestions_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [RoomStatusChanges] (
        [Id] bigint NOT NULL IDENTITY,
        [RoomId] uniqueidentifier NOT NULL,
        [From] int NOT NULL,
        [To] int NOT NULL,
        [At] datetime2 NOT NULL,
        [Reason] nvarchar(200) NULL,
        CONSTRAINT [PK_RoomStatusChanges] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomStatusChanges_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [RoomQuestionOptions] (
        [Id] uniqueidentifier NOT NULL,
        [RoomQuestionId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [Text] nvarchar(80) NOT NULL,
        CONSTRAINT [PK_RoomQuestionOptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RoomQuestionOptions_RoomQuestions_RoomQuestionId] FOREIGN KEY ([RoomQuestionId]) REFERENCES [RoomQuestions] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE TABLE [ParticipantAnswers] (
        [Id] uniqueidentifier NOT NULL,
        [ParticipantId] uniqueidentifier NOT NULL,
        [RoomQuestionId] uniqueidentifier NOT NULL,
        [Order] int NOT NULL,
        [SelectedOptionId] uniqueidentifier NULL,
        [IsCorrect] bit NOT NULL,
        [TimedOut] bit NOT NULL,
        [Points] int NOT NULL,
        [ServedAt] datetime2 NOT NULL,
        [AnsweredAt] datetime2 NULL,
        CONSTRAINT [PK_ParticipantAnswers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ParticipantAnswers_Participants_ParticipantId] FOREIGN KEY ([ParticipantId]) REFERENCES [Participants] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ParticipantAnswers_RoomQuestionOptions_SelectedOptionId] FOREIGN KEY ([SelectedOptionId]) REFERENCES [RoomQuestionOptions] ([Id]),
        CONSTRAINT [FK_ParticipantAnswers_RoomQuestions_RoomQuestionId] FOREIGN KEY ([RoomQuestionId]) REFERENCES [RoomQuestions] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Achievements_UserId_Key] ON [Achievements] ([UserId], [Key]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Categories_IsActive_SortOrder] ON [Categories] ([IsActive], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_ErrorLogs_At] ON [ErrorLogs] ([At]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_ErrorLogs_Level_At] ON [ErrorLogs] ([Level], [At]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParticipantAnswers_ParticipantId_Order] ON [ParticipantAnswers] ([ParticipantId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParticipantAnswers_ParticipantId_RoomQuestionId] ON [ParticipantAnswers] ([ParticipantId], [RoomQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_ParticipantAnswers_RoomQuestionId] ON [ParticipantAnswers] ([RoomQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_ParticipantAnswers_SelectedOptionId] ON [ParticipantAnswers] ([SelectedOptionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Participants_RoomId_CompletedAt] ON [Participants] ([RoomId], [CompletedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Participants_RoomId_VisitorId] ON [Participants] ([RoomId], [VisitorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Participants_ShareCode] ON [Participants] ([ShareCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Participants_UserId_CompletedAt] ON [Participants] ([UserId], [CompletedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_QuestionOptions_QuestionId_Order] ON [QuestionOptions] ([QuestionId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Questions_CategoryId_IsActive] ON [Questions] ([CategoryId], [IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Reports_HandledByUserId] ON [Reports] ([HandledByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Reports_ReporterUserId] ON [Reports] ([ReporterUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Reports_RoomId_ReporterVisitorId] ON [Reports] ([RoomId], [ReporterVisitorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Reports_Status_CreatedAt] ON [Reports] ([Status], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomQuestionOptions_RoomQuestionId_Order] ON [RoomQuestionOptions] ([RoomQuestionId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomQuestions_RoomId_Order] ON [RoomQuestions] ([RoomId], [Order]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RoomQuestions_RoomId_SourceQuestionId] ON [RoomQuestions] ([RoomId], [SourceQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_RoomQuestions_SourceQuestionId] ON [RoomQuestions] ([SourceQuestionId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rooms_Code] ON [Rooms] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Rooms_OwnerUserId_CreatedAt] ON [Rooms] ([OwnerUserId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Rooms_Status_ExpiresAt] ON [Rooms] ([Status], [ExpiresAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_RoomStatusChanges_RoomId_At] ON [RoomStatusChanges] ([RoomId], [At]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE INDEX [IX_Users_CreatedAt] ON [Users] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_PublicId] ON [Users] ([PublicId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261012072050_mig-init'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261012072050_mig-init', N'8.0.31');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Users] ADD [CurrentGameId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Users] ADD [GameLinkCode] varchar(24) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Users] ADD [RecoveryCodeHash] nvarchar(512) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Users] ADD [UserName] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    DROP INDEX [IX_Rooms_Code] ON [Rooms];
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Rooms]') AND [c].[name] = N'Code');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Rooms] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Rooms] ALTER COLUMN [Code] varchar(24) NOT NULL;
    CREATE UNIQUE INDEX [IX_Rooms_Code] ON [Rooms] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Rooms] ADD [IsPersonalGame] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [RoomQuestions] ADD [GeneratorVersion] varchar(32) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [RoomQuestions] ADD [OwnerPrompt] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    CREATE INDEX [IX_Users_CurrentGameId] ON [Users] ([CurrentGameId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_GameLinkCode] ON [Users] ([GameLinkCode]) WHERE [GameLinkCode] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_UserName] ON [Users] ([UserName]) WHERE [UserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    ALTER TABLE [Users] ADD CONSTRAINT [FK_Users_Rooms_CurrentGameId] FOREIGN KEY ([CurrentGameId]) REFERENCES [Rooms] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261013090831_PersonalGames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261013090831_PersonalGames', N'8.0.31');
END;
GO

COMMIT;
GO

