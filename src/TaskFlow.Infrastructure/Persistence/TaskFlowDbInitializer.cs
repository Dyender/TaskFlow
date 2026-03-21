using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Contracts;
using TaskFlow.Infrastructure.Seeding;

namespace TaskFlow.Infrastructure.Persistence;

public static class TaskFlowDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var repository = scope.ServiceProvider.GetRequiredService<ITaskFlowRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        TaskFlowSeeder.Seed(repository, passwordHasher, timeProvider);
    }
}
