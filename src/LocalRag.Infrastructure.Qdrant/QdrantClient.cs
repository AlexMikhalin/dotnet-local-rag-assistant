using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalRag.Application;
using LocalRag.Retrieval;

namespace LocalRag.Infrastructure.Qdrant;

public sealed class QdrantClient(Uri baseUrl, string collectionName) : IVectorStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http = new() { BaseAddress = baseUrl };

    public async Task<bool> IsReadyAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/collections");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureCollectionAsync(int vectorSize)
    {
        using var existing = await _http.GetAsync($"/collections/{collectionName}");
        if (existing.IsSuccessStatusCode)
        {
            return;
        }

        if (existing.StatusCode != HttpStatusCode.NotFound)
        {
            existing.EnsureSuccessStatusCode();
        }

        var createRequest = new
        {
            vectors = new
            {
                size = vectorSize,
                distance = "Cosine"
            }
        };

        using var created = await _http.PutAsJsonAsync($"/collections/{collectionName}", createRequest, JsonOptions);
        created.EnsureSuccessStatusCode();
    }

    public async Task UpsertAsync(QdrantPoint point)
    {
        var request = new { points = new[] { point } };
        using var response = await _http.PutAsJsonAsync($"/collections/{collectionName}/points?wait=true", request, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public Task UpsertAsync(string source, int chunkIndex, string text, float[] vector)
    {
        var point = QdrantPoint.FromChunk(source, chunkIndex, text, vector);
        return UpsertAsync(point);
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, int limit)
    {
        var request = new
        {
            vector,
            limit,
            with_payload = true
        };

        using var response = await _http.PostAsJsonAsync($"/collections/{collectionName}/points/search", request, JsonOptions);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var result = document.RootElement.GetProperty("result");
        var chunks = new List<ScoredChunk>();

        foreach (var item in result.EnumerateArray())
        {
            var payload = item.GetProperty("payload");
            chunks.Add(new ScoredChunk(
                Score: item.GetProperty("score").GetSingle(),
                Source: payload.GetProperty("source").GetString() ?? "(unknown)",
                ChunkIndex: payload.GetProperty("chunk_index").GetInt32(),
                Text: payload.GetProperty("text").GetString() ?? string.Empty));
        }

        return chunks;
    }
}
