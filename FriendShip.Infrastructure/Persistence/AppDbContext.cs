using FriendShip.Application.Abstractions;
using FriendShip.Domain.Achievements;
using FriendShip.Domain.Moderation;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Questions;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FriendShip.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ErrorLogEntry> ErrorLogs => Set<ErrorLogEntry>();
    public DbSet<FriendShip.Domain.Discovery.DiscoveryProfile> DiscoveryProfiles => Set<FriendShip.Domain.Discovery.DiscoveryProfile>();
    public DbSet<FriendShip.Domain.Discovery.DiscoveryAnswer> DiscoveryAnswers => Set<FriendShip.Domain.Discovery.DiscoveryAnswer>();
    public DbSet<FriendShip.Domain.Discovery.AnalysisDecision> AnalysisDecisions => Set<FriendShip.Domain.Discovery.AnalysisDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUsers(modelBuilder);
        ConfigureQuestions(modelBuilder);
        ConfigureRooms(modelBuilder);
        ConfigureParticipants(modelBuilder);
        ConfigureAchievements(modelBuilder);
        ConfigureModeration(modelBuilder);
        ConfigureDiscovery(modelBuilder);
        ConfigureUtcDates(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();

        user.ToTable("Users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).ValueGeneratedNever();

        user.Property(x => x.PublicId)
            .HasMaxLength(AppUser.PublicIdLength)
            .IsUnicode(false)
            .IsRequired();

        user.Property(x => x.Email)
            .HasMaxLength(AppUser.EmailMax)
            .IsRequired();

        user.Property(x => x.UserName).HasMaxLength(30);
        user.Property(x => x.GameLinkCode).HasMaxLength(24).IsUnicode(false);
        user.Property(x => x.RecoveryCodeHash).HasMaxLength(512);
        user.Property(x => x.CurrentGameId).IsConcurrencyToken();
        user.HasIndex(x => x.UserName).IsUnique().HasFilter("[UserName] IS NOT NULL");
        user.HasIndex(x => x.GameLinkCode).IsUnique().HasFilter("[GameLinkCode] IS NOT NULL");
        user.HasOne<Room>().WithMany().HasForeignKey(x => x.CurrentGameId).OnDelete(DeleteBehavior.NoAction);

        user.Property(x => x.DisplayName)
            .HasMaxLength(AppUser.DisplayNameMax)
            .IsRequired();

        user.Property(x => x.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        user.Property(x => x.AvatarKey)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();

        user.Property(x => x.Bio)
            .HasMaxLength(AppUser.BioMax);

        user.Property(x => x.BanReason)
            .HasMaxLength(200);

        user.Property(x => x.SecurityStamp)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired()
            .IsConcurrencyToken();

        user.Property(x => x.Role).HasConversion<int>();

        user.Ignore(x => x.IsAdmin);

        user.HasIndex(x => x.Email).IsUnique();
        user.HasIndex(x => x.PublicId).IsUnique();
        user.HasIndex(x => x.CreatedAt);
    }

    private static void ConfigureDiscovery(ModelBuilder builder)
    {
        var profile = builder.Entity<FriendShip.Domain.Discovery.DiscoveryProfile>();
        profile.ToTable("DiscoveryProfiles"); profile.HasKey(p => p.UserId);
        profile.Property(p => p.UserId).ValueGeneratedNever();
        profile.Property(p => p.Version).IsConcurrencyToken();
        profile.Property(p => p.ComparisonCode).HasMaxLength(24).IsUnicode(false);
        profile.HasIndex(p => p.ComparisonCode).IsUnique().HasFilter("[ComparisonCode] IS NOT NULL");
        profile.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        var answer = builder.Entity<FriendShip.Domain.Discovery.DiscoveryAnswer>();
        answer.ToTable("DiscoveryAnswers"); answer.HasKey(a => new { a.UserId, a.QuestionKey });
        answer.Property(a => a.QuestionKey).HasMaxLength(48).IsUnicode(false);
        answer.Property(a => a.QuestionVersion).HasMaxLength(32).IsUnicode(false);
        answer.Property(a => a.Prompt).HasMaxLength(200);
        answer.Property(a => a.Value).HasMaxLength(100);
        answer.Property(a => a.Revision).IsConcurrencyToken();
        answer.HasOne<FriendShip.Domain.Discovery.DiscoveryProfile>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        var decision = builder.Entity<FriendShip.Domain.Discovery.AnalysisDecision>();
        decision.ToTable("AnalysisDecisions"); decision.HasKey(d => new { d.UserId, d.ResultKey });
        decision.Property(d => d.ResultKey).HasMaxLength(80).IsUnicode(false);
        decision.Property(d => d.Correction).HasMaxLength(4).IsUnicode(false);
        decision.Property(d => d.RuleKey).HasMaxLength(64).IsUnicode(false);
        decision.Property(d => d.CopyVersion).HasMaxLength(32).IsUnicode(false);
        decision.Property(d => d.ShareCode).HasMaxLength(24).IsUnicode(false);
        decision.Property(d => d.Revision).IsConcurrencyToken();
        decision.HasIndex(d => d.ShareCode).IsUnique().HasFilter("[ShareCode] IS NOT NULL");
        decision.HasOne<FriendShip.Domain.Discovery.DiscoveryProfile>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureQuestions(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();

        category.ToTable("Categories");
        category.HasKey(x => x.Id);

        category.Property(x => x.Name)
            .HasMaxLength(Category.NameMax)
            .IsRequired();

        category.Property(x => x.Icon)
            .HasMaxLength(Category.IconMax)
            .IsRequired();

        category.HasIndex(x => new { x.IsActive, x.SortOrder });

        var question = modelBuilder.Entity<Question>();

        question.ToTable("Questions");
        question.HasKey(x => x.Id);

        question.Property(x => x.Text)
            .HasMaxLength(Question.TextMax)
            .IsRequired();

        question.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        question.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        question.Navigation(x => x.Options)
            .HasField("_options")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        question.HasIndex(x => new { x.CategoryId, x.IsActive });

        var option = modelBuilder.Entity<QuestionOption>();

        option.ToTable("QuestionOptions");
        option.HasKey(x => x.Id);

        option.Property(x => x.Text)
            .HasMaxLength(Question.OptionTextMax)
            .IsRequired();

        option.HasIndex(x => new { x.QuestionId, x.Order })
            .IsUnique();
    }

    private static void ConfigureRooms(ModelBuilder modelBuilder)
    {
        var room = modelBuilder.Entity<Room>();

        room.ToTable("Rooms");
        room.HasKey(x => x.Id);
        room.Property(x => x.Id).ValueGeneratedNever();
        room.Property(x => x.ScoringVersion).HasMaxLength(32).IsUnicode(false);

        room.Property(x => x.Code)
            .HasMaxLength(Room.CodeLength)
            .IsUnicode(false)
            .IsRequired();

        room.Property(x => x.Title)
            .HasMaxLength(Room.TitleMax)
            .IsRequired();

        room.Property(x => x.Status).HasConversion<int>();

        room.Property(x => x.Version)
            .IsConcurrencyToken();

        room.Ignore(x => x.IsEnded);
        room.Ignore(x => x.AreAnswersRevealable);

        room.HasIndex(x => x.Code).IsUnique();
        room.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        room.HasIndex(x => new { x.Status, x.ExpiresAt });

        room.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.NoAction);

        room.OwnsOne(x => x.Settings, settings =>
        {
            settings.Property(x => x.Mode)
                .HasColumnName("Mode")
                .HasConversion<int>();

            settings.Property(x => x.QuestionCount)
                .HasColumnName("QuestionCount");

            settings.Property(x => x.TimeLimitSeconds)
                .HasColumnName("TimeLimitSeconds");

            settings.Property(x => x.MaxParticipants)
                .HasColumnName("MaxParticipants");

            settings.Property(x => x.RevealMode)
                .HasColumnName("RevealMode")
                .HasConversion<int>();

            settings.Property(x => x.ShowLeaderboard)
                .HasColumnName("ShowLeaderboard");
        });

        room.Navigation(x => x.Settings).IsRequired();

        room.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        room.Navigation(x => x.Questions)
            .HasField("_questions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        room.HasMany(x => x.History)
            .WithOne()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        room.Navigation(x => x.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var question = modelBuilder.Entity<RoomQuestion>();

        question.ToTable("RoomQuestions");
        question.HasKey(x => x.Id);
        question.Property(x => x.Id).ValueGeneratedNever();

        question.Property(x => x.Text)
            .HasMaxLength(Question.TextMax)
            .IsRequired();

        question.Property(x => x.CategoryName)
            .HasMaxLength(Category.NameMax)
            .IsRequired();

        question.Property(x => x.OwnerPrompt).HasMaxLength(Question.TextMax);
        question.Property(x => x.GeneratorVersion).HasMaxLength(32).IsUnicode(false);
        question.Property(x => x.TemplateKey).HasMaxLength(48).IsUnicode(false);
        question.Property(x => x.TemplateVersion).HasMaxLength(32).IsUnicode(false);

        question.Property(x => x.CategoryIcon)
            .HasMaxLength(Category.IconMax)
            .IsRequired();

        question.Ignore(x => x.IsAnsweredByOwner);

        question.HasIndex(x => new { x.RoomId, x.Order })
            .IsUnique();

        question.HasIndex(x => new { x.RoomId, x.SourceQuestionId })
            .IsUnique();

        question.HasOne<Question>()
            .WithMany()
            .HasForeignKey(x => x.SourceQuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        question.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.RoomQuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        question.Navigation(x => x.Options)
            .HasField("_options")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var option = modelBuilder.Entity<RoomQuestionOption>();

        option.ToTable("RoomQuestionOptions");
        option.HasKey(x => x.Id);
        option.Property(x => x.Id).ValueGeneratedNever();

        option.Property(x => x.Text)
            .HasMaxLength(Question.OptionTextMax)
            .IsRequired();

        option.HasIndex(x => new { x.RoomQuestionId, x.Order })
            .IsUnique();

        var history = modelBuilder.Entity<RoomStatusChange>();

        history.ToTable("RoomStatusChanges");
        history.HasKey(x => x.Id);

        history.Property(x => x.From).HasConversion<int>();
        history.Property(x => x.To).HasConversion<int>();
        history.Property(x => x.Reason).HasMaxLength(200);

        history.HasIndex(x => new { x.RoomId, x.At });
    }

    private static void ConfigureParticipants(ModelBuilder modelBuilder)
    {
        var participant = modelBuilder.Entity<Participant>();

        participant.ToTable("Participants");
        participant.HasKey(x => x.Id);
        participant.Property(x => x.Id).ValueGeneratedNever();

        participant.Property(x => x.DisplayName)
            .HasMaxLength(Participant.NameMax)
            .IsRequired();

        participant.Property(x => x.AvatarKey)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();

        participant.Property(x => x.ShareCode)
            .HasMaxLength(Participant.ShareCodeLength)
            .IsUnicode(false)
            .IsRequired();

        participant.Property(x => x.CompletedAt)
            .IsConcurrencyToken();

        participant.Ignore(x => x.IsCompleted);
        participant.Ignore(x => x.AnsweredCount);

        participant.HasIndex(x => new { x.RoomId, x.VisitorId })
            .IsUnique();

        participant.HasIndex(x => x.ShareCode)
            .IsUnique();

        participant.HasIndex(x => new { x.UserId, x.CompletedAt });
        participant.HasIndex(x => new { x.RoomId, x.CompletedAt });

        participant.HasOne<Room>()
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.NoAction);

        participant.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        participant.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        participant.Navigation(x => x.Answers)
            .HasField("_answers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var answer = modelBuilder.Entity<ParticipantAnswer>();

        answer.ToTable("ParticipantAnswers");
        answer.HasKey(x => x.Id);
        answer.Property(x => x.Id).ValueGeneratedNever();

        answer.Property(x => x.AnsweredAt)
            .IsConcurrencyToken();

        answer.Ignore(x => x.IsFinal);

        answer.HasIndex(x => new
        {
            x.ParticipantId,
            x.RoomQuestionId
        }).IsUnique();

        answer.HasIndex(x => new
        {
            x.ParticipantId,
            x.Order
        }).IsUnique();

        answer.HasOne<RoomQuestion>()
            .WithMany()
            .HasForeignKey(x => x.RoomQuestionId)
            .OnDelete(DeleteBehavior.NoAction);

        answer.HasOne<RoomQuestionOption>()
            .WithMany()
            .HasForeignKey(x => x.SelectedOptionId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureAchievements(ModelBuilder modelBuilder)
    {
        var achievement = modelBuilder.Entity<Achievement>();

        achievement.ToTable("Achievements");
        achievement.HasKey(x => x.Id);
        achievement.Property(x => x.Id).ValueGeneratedNever();

        achievement.Property(x => x.Key)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        achievement.HasIndex(x => new { x.UserId, x.Key })
            .IsUnique();

        achievement.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureModeration(ModelBuilder modelBuilder)
    {
        var report = modelBuilder.Entity<Report>();

        report.ToTable("Reports");
        report.HasKey(x => x.Id);
        report.Property(x => x.Id).ValueGeneratedNever();

        report.Property(x => x.Reason)
            .HasMaxLength(Report.ReasonMax)
            .IsRequired();

        report.Property(x => x.Status)
            .HasConversion<int>()
            .IsConcurrencyToken();

        report.HasIndex(x => new { x.Status, x.CreatedAt });
        report.HasIndex(x => new { x.RoomId, x.ReporterVisitorId });

        report.HasOne<Room>()
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.NoAction);

        report.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ReporterUserId)
            .OnDelete(DeleteBehavior.NoAction);

        report.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.HandledByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        var error = modelBuilder.Entity<ErrorLogEntry>();

        error.ToTable("ErrorLogs");
        error.HasKey(x => x.Id);

        error.Property(x => x.Level)
            .HasMaxLength(20)
            .IsRequired();

        error.Property(x => x.Category)
            .HasMaxLength(200)
            .IsRequired();

        error.Property(x => x.Message)
            .HasMaxLength(2000)
            .IsRequired();

        error.Property(x => x.Exception)
            .HasMaxLength(8000);

        error.HasIndex(x => x.At);
        error.HasIndex(x => new { x.Level, x.At });
    }

    private static void ConfigureUtcDates(ModelBuilder modelBuilder)
    {
        var converter = new ValueConverter<DateTime, DateTime>(
            value => value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        // EF does not pass null values to value converters.
        // This converter also applies to nullable DateTime properties.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) ||
                    property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(converter);
                }
            }
        }
    }
}
