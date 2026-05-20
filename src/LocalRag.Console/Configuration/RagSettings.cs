namespace LocalRag;

internal sealed record RagSettings(
    Uri OllamaUrl,
    Uri QdrantUrl,
    string CollectionName,
    string EmbeddingModel,
    string ChatModel,
    int ChunkSize,
    int ChunkOverlap,
    int TopK)
{
    public static RagSettings FromEnvironment() => new(
        OllamaUrl: ReadUri("OLLAMA_URL", "http://localhost:11434"),
        QdrantUrl: ReadUri("QDRANT_URL", "http://localhost:6333"),
        CollectionName: Read("RAG_COLLECTION", "local_rag_documents"),
        EmbeddingModel: Read("RAG_EMBED_MODEL", "nomic-embed-text"),
        ChatModel: Read("RAG_CHAT_MODEL", "llama3.2:3b"),
        ChunkSize: ReadInt("RAG_CHUNK_SIZE", 900),
        ChunkOverlap: ReadInt("RAG_CHUNK_OVERLAP", 120),
        TopK: ReadInt("RAG_TOP_K", 5));

    private static string Read(string key, string fallback) =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))
            ? fallback
            : Environment.GetEnvironmentVariable(key)!;

    private static Uri ReadUri(string key, string fallback) => new(Read(key, fallback));

    private static int ReadInt(string key, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(key), out var value) ? value : fallback;
}
