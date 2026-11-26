using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ManageU.Application.Common.Exceptions;

namespace ManageU.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during request execution: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var statusCode = exception switch
        {
            ValidationException => (int)HttpStatusCode.BadRequest,
            UnauthorizedException => (int)HttpStatusCode.Unauthorized,
            ConflictException => (int)HttpStatusCode.Conflict,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var title = exception switch
        {
            ValidationException => "Validation failure",
            UnauthorizedException => "Unauthorized",
            ConflictException => "Conflict",
            KeyNotFoundException => "Resource not found",
            ArgumentException => "Invalid request argument",
            _ => "An unexpected error occurred"
        };

        if (exception is ValidationException validationException)
        {
            var validationProblemDetails = new ValidationProblemDetails(validationException.Errors)
            {
                Status = statusCode,
                Title = title,
                Detail = validationException.Message,
                Instance = context.Request.Path
            };

            var validationJson = JsonSerializer.Serialize(validationProblemDetails);
            return context.Response.WriteAsync(validationJson);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == (int)HttpStatusCode.InternalServerError
                ? "An unexpected error occurred while processing your request."
                : exception.Message,
            Instance = context.Request.Path
        };

        var json = JsonSerializer.Serialize(problemDetails);
        return context.Response.WriteAsync(json);
    }
}
