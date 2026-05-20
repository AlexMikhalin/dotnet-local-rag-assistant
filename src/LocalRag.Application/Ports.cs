using LocalRag.Retrieval;

namespace LocalRag.Application;

public interface ILocalAiClient
{
    Task<bool> IsReadyAsync();

    Task<IReadOnlyList<string>> ListModelsAsync();

    Task<float[]> EmbedAsync(string model, string input);

    Task<string> ChatAsync(string model, string prompt);
}

public interface IVectorStore
{
    Task<bool> IsReadyAsync();

    Task EnsureCollectionAsync(int vectorSize);

    Task UpsertAsync(string source, int chunkIndex, string text, float[] vector);

    Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, int limit);
}
