using LocalRag.Application;
using LocalRag.Retrieval;

namespace LocalRag.Application.Tests;

public sealed class RagApplicationTests
{
    [Fact]
    public async Task AskAsync_WhenNoContextFound_DoesNotCallChatModel()
    {
        var ai = new FakeLocalAiClient();
        var vectorStore = new FakeVectorStore();
        var app = new RagApplication(CreateSettings(), ai, vectorStore);

        var result = await app.AskAsync("What is the meaning of life?");

        Assert.False(result.HasContext);
        Assert.Equal("None", result.Confidence.Label);
        Assert.Equal(1, ai.EmbedCalls);
        Assert.Equal(0, ai.ChatCalls);
        Assert.Equal(1, vectorStore.SearchCalls);
    }

    [Fact]
    public async Task AskAsync_WhenContextFound_CallsChatModelWithRetrievedContext()
    {
        var ai = new FakeLocalAiClient();
        var vectorStore = new FakeVectorStore
        {
            Chunks =
            [
                new ScoredChunk(0.7f, "sample-docs/rag-notes.md", 0, "Ollama creates local embeddings.")
            ]
        };
        var app = new RagApplication(CreateSettings(), ai, vectorStore);

        var result = await app.AskAsync("What does Ollama do?");

        Assert.True(result.HasContext);
        Assert.Equal("fake answer", result.Answer);
        Assert.Equal("High", result.Confidence.Label);
        Assert.Equal(1, ai.ChatCalls);
        Assert.Contains("Ollama creates local embeddings.", ai.LastPrompt);
    }

    private static RagSettings CreateSettings() => new(
        OllamaUrl: new Uri("http://localhost:11434"),
        QdrantUrl: new Uri("http://localhost:6333"),
        CollectionName: "test_collection",
        EmbeddingModel: "test-embed",
        ChatModel: "test-chat",
        ChunkSize: 900,
        ChunkOverlap: 120,
        TopK: 5);

    private sealed class FakeLocalAiClient : ILocalAiClient
    {
        public int EmbedCalls { get; private set; }

        public int ChatCalls { get; private set; }

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<bool> IsReadyAsync() => Task.FromResult(true);

        public Task<IReadOnlyList<string>> ListModelsAsync() =>
            Task.FromResult<IReadOnlyList<string>>(["test-embed", "test-chat"]);

        public Task<float[]> EmbedAsync(string model, string input)
        {
            EmbedCalls++;
            return Task.FromResult(new[] { 0.1f, 0.2f, 0.3f });
        }

        public Task<string> ChatAsync(string model, string prompt)
        {
            ChatCalls++;
            LastPrompt = prompt;
            return Task.FromResult("fake answer");
        }
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public IReadOnlyList<ScoredChunk> Chunks { get; init; } = [];

        public int SearchCalls { get; private set; }

        public Task<bool> IsReadyAsync() => Task.FromResult(true);

        public Task EnsureCollectionAsync(int vectorSize) => Task.CompletedTask;

        public Task UpsertAsync(string source, int chunkIndex, string text, float[] vector) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ScoredChunk>> SearchAsync(float[] vector, int limit)
        {
            SearchCalls++;
            return Task.FromResult(Chunks);
        }
    }
}
