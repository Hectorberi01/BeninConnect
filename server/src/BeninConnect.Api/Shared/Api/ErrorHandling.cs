using BeninConnect.Shared.Application;
using BeninConnect.Shared.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BeninConnect.Shared.Api;

/// <summary>Convertit les exceptions métier en ProblemDetails (detail = message lisible affiché par l'app).</summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problems, ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException v => new ValidationProblemDetails(v.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Données invalides",
                Detail = v.Errors.Values.SelectMany(e => e).FirstOrDefault() ?? v.Message
            },
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Introuvable", exception.Message),
            ForbiddenException => Problem(StatusCodes.Status403Forbidden, "Accès refusé", exception.Message),
            ConflictException => Problem(StatusCodes.Status409Conflict, "Conflit", exception.Message),
            DomainException => Problem(StatusCodes.Status400BadRequest, "Requête refusée", exception.Message),
            UnauthorizedAccessException => Problem(StatusCodes.Status401Unauthorized, "Non authentifié", exception.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Erreur serveur", "Une erreur inattendue est survenue.")
        };

        if (problem.Status >= 500)
        {
            logger.LogError(exception, "Erreur non gérée");
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}
