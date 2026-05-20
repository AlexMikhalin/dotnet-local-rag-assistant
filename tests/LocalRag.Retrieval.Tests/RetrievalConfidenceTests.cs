using LocalRag.Retrieval;

namespace LocalRag.Retrieval.Tests;

public sealed class RetrievalConfidenceTests
{
    [Theory]
    [InlineData(0.60f, "High")]
    [InlineData(0.45f, "Medium")]
    [InlineData(0.30f, "Low")]
    [InlineData(0.10f, "Very low")]
    public void From_LabelsConfidenceFromTopScore(float score, string expectedLabel)
    {
        var confidence = RetrievalConfidence.From([
            new ScoredChunk(score, "source.md", 0, "text")
        ]);

        Assert.Equal(expectedLabel, confidence.Label);
    }

    [Fact]
    public void From_ReturnsNoneWhenNoChunksFound()
    {
        var confidence = RetrievalConfidence.From([]);

        Assert.Equal("None", confidence.Label);
    }
}
