using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BeninConnect.Modules.Editorial.Application;

namespace BeninConnect.Modules.Editorial.Infrastructure;

/// <summary>API Messages d'Anthropic (Claude). Sans clé : non configuré, rien n'est appelé.</summary>
public sealed class ClaudeLlmClient(IHttpClientFactory http, EditorialSettings settings) : ILlmClient
{
    public const string ClientName = "editorial-llm";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.Llm.ApiKey);

    public string Model => settings.Llm.Model;

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Rédaction automatique non configurée.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{settings.Llm.BaseUrl.TrimEnd('/')}/v1/messages")
        {
            Content = JsonContent.Create(new
            {
                model = settings.Llm.Model,
                max_tokens = settings.Llm.MaxTokens,
                system,
                messages = new[] { new { role = "user", content = user } }
            })
        };
        request.Headers.Add("x-api-key", settings.Llm.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await http.CreateClient(ClientName).SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Corps d'erreur seulement (jamais la clé)
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Claude {(int)response.StatusCode} : {(body.Length > 300 ? body[..300] : body)}");
        }

        var result = await response.Content.ReadFromJsonAsync<MessageResponse>(ct);
        var text = string.Concat(result?.Content?.Where(c => c.Type == "text").Select(c => c.Text) ?? []);
        return text.Length > 0 ? text : throw new InvalidOperationException("Réponse vide de Claude.");
    }

    private sealed record MessageResponse([property: JsonPropertyName("content")] List<ContentBlock>? Content);

    private sealed record ContentBlock([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string? Text);
}
