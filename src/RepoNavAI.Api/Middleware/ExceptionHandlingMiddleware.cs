using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using RepoNavAI.Application.Common.Exceptions;

namespace RepoNavAI.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var (status, title, errors) = exception switch
            {
                ValidationException validation => (StatusCodes.Status400BadRequest, "Validation failed", validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray()) as object),
                ConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, exception.Message, null),
                ForbiddenException => (StatusCodes.Status403Forbidden, exception.Message, null),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
                ExternalServiceException => (StatusCodes.Status502BadGateway, exception.Message, null),
                RateLimitException => (StatusCodes.Status429TooManyRequests, exception.Message, null),
                InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message, null),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
            };
            if (status == 500) logger.LogError(exception, "Unhandled request exception");
            else logger.LogWarning(exception, "Request rejected with status {StatusCode}", status);
            var problem = new ProblemDetails { Status = status, Title = title, Instance = context.Request.Path };
            if (errors is not null) problem.Extensions["errors"] = errors;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
