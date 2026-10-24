using FriendShip.Application.Abstractions;
using FriendShip.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
            services.AddScoped<AuthService>();
            services.AddScoped<RoomService>();
            services.AddSingleton<FriendShip.Application.Questions.QuestionFactory>();
            services.AddScoped<PersonalGameService>();
            services.AddSingleton<FriendShip.Application.Discovery.DiscoveryEngine>();
            services.AddSingleton<FriendShip.Application.Discovery.AdaptiveQuestionSelector>();
            services.AddSingleton<FriendShip.Application.Discovery.KnowledgeEngine>();
            services.AddScoped<DiscoveryService>();
            services.AddScoped<FriendKnowledgeService>();
            services.AddScoped<PlayService>();
            services.AddScoped<StatsService>();
            services.AddScoped<AchievementService>();
            services.AddScoped<ResultsService>();
            services.AddScoped<AdminService>(); 
            services.AddScoped<ProfileService>();
            return services;
        }
    }
}
