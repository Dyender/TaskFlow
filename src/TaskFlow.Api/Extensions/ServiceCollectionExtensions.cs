using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Background;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Realtime;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.Services;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskFlow(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TaskFlow")
            ?? throw new InvalidOperationException("Connection string 'TaskFlow' was not configured.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<DeadlineReminderSettings>(configuration.GetSection(DeadlineReminderSettings.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtSettings>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DeadlineReminderSettings>>().Value);

        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<TaskFlowDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure();
            });
        });

        services.AddScoped<ITaskFlowRepository, EfTaskFlowRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IBoardRealtimeNotifier, SignalRBoardRealtimeNotifier>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IActivityService, ActivityService>();

        services.AddAuthentication("TaskFlowBearer")
            .AddScheme<AuthenticationSchemeOptions, TaskFlowAuthenticationHandler>("TaskFlowBearer", _ => { });
        services.AddAuthorization();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddHostedService<DeadlineReminderBackgroundService>();
        return services;
    }
}
