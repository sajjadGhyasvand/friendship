using FriendShip.Domain.Achievements;
using FriendShip.Domain.Moderation;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Questions;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Abstractions
{
    public interface IAppDbContext : IAsyncDisposable
    {
        DbSet<FriendShip.Domain.Discovery.DiscoveryProfile> DiscoveryProfiles { get; }
        DbSet<FriendShip.Domain.Discovery.DiscoveryAnswer> DiscoveryAnswers { get; }
        DbSet<FriendShip.Domain.Discovery.AnalysisDecision> AnalysisDecisions { get; }
        DbSet<AppUser> Users { get; }
        DbSet<Category> Categories { get; }
        DbSet<Question> Questions { get; }
        DbSet<Room> Rooms { get; }
        DbSet<Participant> Participants { get; }
        DbSet<Achievement> Achievements { get; }
        DbSet<Report> Reports { get; }
        DbSet<ErrorLogEntry> ErrorLogs { get; }
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }

}
