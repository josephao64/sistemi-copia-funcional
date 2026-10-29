using System.Net;
using Microsoft.EntityFrameworkCore;
using SistemiPos.Application.Common.Exceptions;
using SistemiPos.Domain.Common.Exceptions;

namespace SistemiPos.Web.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing request {Path}", context.Request.Path);

            context.Response.ContentType = "application/json; charset=utf-8";

            var (statusCode, title) = exception switch
            {
                DomainRuleException => (HttpStatusCode.BadRequest, "Regla de negocio no cumplida"),
                NotFoundException => (HttpStatusCode.NotFound, "Recurso no encontrado"),
                DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "La información cambió durante la operación"),
                _ => (HttpStatusCode.InternalServerError, "Error interno del servidor")
            };

            context.Response.StatusCode = (int)statusCode;

            var detail = exception switch
            {
                DomainRuleException or NotFoundException => exception.Message,
                DbUpdateConcurrencyException concurrencyException when environment.IsDevelopment() =>
                    $"{concurrencyException.Message} Entidades: {string.Join(", ", concurrencyException.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct())}.",
                DbUpdateConcurrencyException => "Otro proceso actualizó la información antes de finalizar. Recarga la pantalla y vuelve a intentarlo.",
                _ when environment.IsDevelopment() => exception.GetBaseException().Message,
                _ => "No fue posible completar la operación. Inténtalo nuevamente o contacta al administrador."
            };

            await context.Response.WriteAsJsonAsync(new
            {
                title,
                detail,
                status = context.Response.StatusCode,
                traceId = context.TraceIdentifier
            });
        }
    }
}
