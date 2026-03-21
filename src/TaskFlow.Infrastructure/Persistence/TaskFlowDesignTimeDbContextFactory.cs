using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class TaskFlowDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TaskFlowDbContext>
{
    public TaskFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("TASKFLOW_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=taskflow;Username=taskflow;Password=taskflow";

        var optionsBuilder = new DbContextOptionsBuilder<TaskFlowDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new TaskFlowDbContext(optionsBuilder.Options);
    }
}
