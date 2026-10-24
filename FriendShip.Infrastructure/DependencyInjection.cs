using FriendShip.Application.Abstractions;
using FriendShip.Infrastructure.Persistence;
using FriendShip.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FriendShip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseSqlServer(connectionString);

            // Do not enable sensitive-data logging:
            // queries may contain personal data or answer information.
        });

        services.AddSingleton<
            IAppDbContextFactory,
            AppDbContextFactory>();

        services.AddSingleton<
            IPasswordService,
            PasswordService>();

        return services;
    }
}
