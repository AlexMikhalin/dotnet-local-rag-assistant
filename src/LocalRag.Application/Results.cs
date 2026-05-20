using LocalRag.Retrieval;

namespace LocalRag.Application;

public sealed record RagStatus(
    bool OllamaReady,
    bool QdrantReady,
    IReadOnlyList<string> OllamaModels);

public sealed record IndexedFile(string Source, int ChunkCount);

public sealed record IngestResult(
    bool Success,
    string Message,
    int FilesIndexed,
    int ChunksIndexed,
    IReadOnlyList<IndexedFile> IndexedFiles)
{
    public static IngestResult Completed(int filesIndexed, int chunksIndexed, IReadOnlyList<IndexedFile> indexedFiles) =>
        new(true, string.Empty, filesIndexed, chunksIndexed, indexedFiles);

    public static IngestResult Failed(string message) =>
        new(false, message, 0, 0, []);
}

public sealed record SearchResult(
    IReadOnlyList<ScoredChunk> Chunks,
    RetrievalConfidence Confidence);

public sealed record AskResult(
    bool HasContext,
    string Answer,
    IReadOnlyList<ScoredChunk> Chunks,
    RetrievalConfidence Confidence)
{
    public static AskResult WithAnswer(
        string answer,
        IReadOnlyList<ScoredChunk> chunks,
        RetrievalConfidence confidence) =>
        new(true, answer, chunks, confidence);

    public static AskResult WithoutContext(RetrievalConfidence confidence) =>
        new(false, string.Empty, [], confidence);
}
