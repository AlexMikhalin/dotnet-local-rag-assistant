using System.Net.Http.Json;
using System.Text.Json;

namespace LocalRag.Infrastructure.Ollama;

public sealed class OllamaClient(Uri baseUrl)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = new() { BaseAddress = baseUrl };

    public async Task<bool> IsReadyAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync()
    {
        var response = await _http.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", JsonOptions);
        return response?.Models.Select(model => model.Name).Order().ToArray() ?? [];
    }

    public async Task<float[]> EmbedAsync(string model, string input)
    {
        using var response = await _http.PostAsJsonAsync("/api/embed", new { model, input }, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var embeddings = document.RootElement.GetProperty("embeddings");
        var first = embeddings[0];

        var vector = new float[first.GetArrayLength()];
        var index = 0;
        foreach (var value in first.EnumerateArray())
        {
            vector[index++] = value.GetSingle();
        }

        return vector;
    }

    public async Task<string> ChatAsync(string model, string prompt)
    {
        var request = new
        {
            model,
            stream = false,
            messages = new[]
            {
                new { role = "system", content = "You are a precise RAG assistant. Answer only from the provided context. If the context is insufficient, say what is missing." },
                new { role = "user", content = prompt }
            }
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }
}
