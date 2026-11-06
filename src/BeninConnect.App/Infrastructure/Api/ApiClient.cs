using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BeninConnect.App.Core.Services;
using BeninConnect.App.Core.Utilities;
using CommunityToolkit.Mvvm.Messaging;

namespace BeninConnect.App.Infrastructure.Api;

/// <summary>Session expirée ou jeton refusé (HTTP 401).</summary>
public sealed class ApiUnauthorizedException() : DomainException("Votre session a expiré. Reconnectez-vous.");

/// <summary>
/// Client HTTP de l'API BéninConnect : jeton JWT, sérialisation, conversion des erreurs ProblemDetails
/// en exceptions lisibles (DomainException / NotFoundException) affichées par les écrans.
/// </summary>
public sealed class ApiClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly IPreferencesService _preferences;

    public ApiClient(HttpClient http, IPreferencesService preferences, IMessenger messenger)
    {
        _http = http;
        _preferences = preferences;
        Token = preferences.Get(PreferenceKeys.ApiToken);

        // Déconnexion : le jeton est oublié.
        messenger.Register<ApiClient, SessionChangedMessage>(this, static (r, m) =>
        {
            if (m.User is null)
            {
                r.SetToken(null);
            }
        });
    }

    public string? Token { get; private set; }

    /// <summary>Le jeton est conservé localement pour l'option « Rester connecté » (à déplacer en stockage sécurisé).</summary>
    public void SetToken(string? token)
    {
        Token = token;
        _preferences.Set(PreferenceKeys.ApiToken, token);
    }

    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string path, object? body = null, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);

    public Task<T> PutAsync<T>(string path, object? body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Put, path, body, ct);

    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, null, ct);

    public async Task SendAsync(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await ExecuteAsync(method, path, body, ct);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await ExecuteAsync(method, path, body, ct);
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
               ?? throw new DomainException("Réponse vide du serveur.");
    }

    private async Task<HttpResponseMessage> ExecuteAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (Token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new DomainException("Impossible de joindre le serveur. Vérifiez votre connexion et l'adresse de l'API.");
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            var detail = await ReadDetailAsync(response, ct);
            throw response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new ApiUnauthorizedException(),
                HttpStatusCode.NotFound => new DomainException(detail ?? "Élément introuvable."),
                HttpStatusCode.Forbidden => new DomainException(detail ?? "Action non autorisée."),
                >= HttpStatusCode.InternalServerError => new DomainException("Le serveur a rencontré une erreur. Réessayez."),
                _ => new DomainException(detail ?? "La requête a été refusée.")
            };
        }
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json, ct);
            return string.IsNullOrWhiteSpace(problem?.Detail) ? problem?.Title : problem.Detail;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed record ProblemResponse(string? Title, string? Detail);
}
