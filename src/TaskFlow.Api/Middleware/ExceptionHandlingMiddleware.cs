using System.Net;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled request error.");

        var statusCode = exception switch
        {
            ValidationAppException => HttpStatusCode.BadRequest,
            ConflictException => HttpStatusCode.Conflict,
            ForbiddenException => HttpStatusCode.Forbidden,
            NotFoundException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = exception.Message,
            status = context.Response.StatusCode
        });
    }
}
