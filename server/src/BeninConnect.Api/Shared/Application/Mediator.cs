using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace BeninConnect.Shared.Application;

/// <summary>Requête CQRS : une commande modifie l'état, une requête le lit.</summary>
public interface IRequest<TResult>;

public interface ICommand<TResult> : IRequest<TResult>;

public interface IQuery<TResult> : IRequest<TResult>;

public interface IRequestHandler<in TRequest, TResult>
    where TRequest : IRequest<TResult>
{
    Task<TResult> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>Requête dotée de règles de validation, vérifiées avant le handler.</summary>
public interface IValidatable
{
    IEnumerable<(string Field, string Error)> Validate();
}

public interface ISender
{
    Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default);
}

/// <summary>Dispatcher minimal (sans dépendance externe) : validation puis handler résolu par DI.</summary>
public sealed class Sender(IServiceProvider services) : ISender
{
    private static readonly ConcurrentDictionary<(Type, Type), (Type HandlerType, MethodInfo Handle)> Cache = new();

    public async Task<TResult> Send<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        if (request is IValidatable validatable)
        {
            var errors = validatable.Validate()
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Error).ToArray());

            if (errors.Count > 0)
            {
                throw new ValidationException(errors);
            }
        }

        var (handlerType, handle) = Cache.GetOrAdd((request.GetType(), typeof(TResult)), static key =>
        {
            var type = typeof(IRequestHandler<,>).MakeGenericType(key.Item1, key.Item2);
            return (type, type.GetMethod("Handle")!);
        });

        var handler = services.GetRequiredService(handlerType);
        try
        {
            return await (Task<TResult>)handle.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex.InnerException);
            throw;
        }
    }
}

/// <summary>Erreurs de validation par champ (HTTP 400, ValidationProblemDetails).</summary>
public sealed class ValidationException(IDictionary<string, string[]> errors)
    : Exception("Les données envoyées sont invalides.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
