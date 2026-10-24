using FriendShip.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Infrastructure.Persistence;

public sealed class AppDbContextFactory(
    IDbContextFactory<AppDbContext> factory)
    : IAppDbContextFactory
{
    public IAppDbContext Create() => factory.CreateDbContext();
}
