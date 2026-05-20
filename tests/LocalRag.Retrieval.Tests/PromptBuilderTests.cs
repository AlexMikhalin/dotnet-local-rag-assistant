using LocalRag.Retrieval;

namespace LocalRag.Retrieval.Tests;

public sealed class PromptBuilderTests
{
    [Fact]
    public void Build_IncludesQuestionAndSourceMetadata()
    {
        var chunks = new[]
        {
            new ScoredChunk(0.72f, "sample-docs/rag-notes.md", 2, "Qdrant stores document vectors.")
        };

        var prompt = PromptBuilder.Build("What stores vectors?", chunks);

        Assert.Contains("What stores vectors?", prompt);
        Assert.Contains("sample-docs/rag-notes.md #chunk-2", prompt);
        Assert.Contains("Qdrant stores document vectors.", prompt);
    }
}
