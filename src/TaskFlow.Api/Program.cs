using TaskFlow.Api.Extensions;
using TaskFlow.Api.Hubs;
using TaskFlow.Api.Middleware;
using TaskFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddTaskFlow(builder.Configuration);

var app = builder.Build();

await TaskFlowDbInitializer.InitializeAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<BoardHub>("/hubs/boards");

app.Run();
