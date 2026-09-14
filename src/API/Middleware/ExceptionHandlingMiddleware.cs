using System.Text.Json;
using FlowDesk.Application.Common.Exceptions;
using FlowDesk.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FlowDesk.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request execution.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, problemDetails) = exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Detail = valEx.Message,
                    Extensions = { ["errors"] = valEx.Errors }
                }),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Resource Not Found",
                    Detail = notFoundEx.Message
                }),

            UnauthorizedException unauthEx => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = unauthEx.Message
                }),

            ForbiddenException forbEx => (
                StatusCodes.Status403Forbidden,
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = forbEx.Message
                }),

            DomainException domainEx => (
                StatusCodes.Status422UnprocessableEntity,
                new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Domain Rule Violation",
                    Detail = domainEx.Message
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = _environment.IsDevelopment() ? exception.Message : "An unexpected internal server error occurred."
                })
        };

        context.Response.StatusCode = statusCode;
        var json = JsonSerializer.Serialize(problemDetails);

        return context.Response.WriteAsync(json);
    }
}
