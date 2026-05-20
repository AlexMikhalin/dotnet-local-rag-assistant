using LocalRag.Infrastructure.Ollama;
using LocalRag.Infrastructure.Qdrant;
using LocalRag.Ingestion;
using LocalRag.Retrieval;

namespace LocalRag.Application;

public sealed class RagApplication
{
    private readonly RagSettings _settings;
    private readonly OllamaClient _ollama;
    private readonly QdrantClient _qdrant;

    public RagApplication(RagSettings settings)
    {
        _settings = settings;
        _ollama = new OllamaClient(settings.OllamaUrl);
        _qdrant = new QdrantClient(settings.QdrantUrl, settings.CollectionName);
    }

    public async Task<RagStatus> GetStatusAsync()
    {
        var ollamaReady = await _ollama.IsReadyAsync();
        var qdrantReady = await _qdrant.IsReadyAsync();
        var models = ollamaReady ? await _ollama.ListModelsAsync() : [];

        return new RagStatus(ollamaReady, qdrantReady, models);
    }

    public async Task<IngestResult> IngestAsync(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return IngestResult.Failed($"Path not found: {path}");
        }

        var files = DocumentFinder.FindTextFiles(path).ToArray();
        if (files.Length == 0)
        {
            return IngestResult.Failed("No .txt or .md files found.");
        }

        var firstEmbedding = await _ollama.EmbedAsync(_settings.EmbeddingModel, "dimension probe");
        await _qdrant.EnsureCollectionAsync(firstEmbedding.Length);

        var indexedFiles = new List<IndexedFile>();
        var totalChunks = 0;

        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file);
            var chunks = TextChunker.Split(text, _settings.ChunkSize, _settings.ChunkOverlap).ToArray();
            var source = Path.GetRelativePath(Environment.CurrentDirectory, file);

            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var vector = await _ollama.EmbedAsync(_settings.EmbeddingModel, chunk);
                var point = QdrantPoint.FromChunk(file, i, chunk, vector);
                await _qdrant.UpsertAsync(point);
                totalChunks++;
            }

            indexedFiles.Add(new IndexedFile(source, chunks.Length));
        }

        return IngestResult.Completed(files.Length, totalChunks, indexedFiles);
    }

    public async Task<SearchResult> SearchAsync(string question)
    {
        var chunks = await RetrieveAsync(question);
        return new SearchResult(chunks, RetrievalConfidence.From(chunks));
    }

    public async Task<AskResult> AskAsync(string question)
    {
        var chunks = await RetrieveAsync(question);
        if (chunks.Count == 0)
        {
            return AskResult.WithoutContext(RetrievalConfidence.From(chunks));
        }

        var prompt = PromptBuilder.Build(question, chunks);
        var answer = await _ollama.ChatAsync(_settings.ChatModel, prompt);

        return AskResult.WithAnswer(answer, chunks, RetrievalConfidence.From(chunks));
    }

    private async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string question)
    {
        var vector = await _ollama.EmbedAsync(_settings.EmbeddingModel, question);
        return await _qdrant.SearchAsync(vector, _settings.TopK);
    }
}
