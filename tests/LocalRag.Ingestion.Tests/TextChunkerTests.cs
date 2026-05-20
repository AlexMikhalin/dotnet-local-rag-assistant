using LocalRag.Ingestion;

namespace LocalRag.Ingestion.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Split_CreatesOverlappingChunks()
    {
        var chunks = TextChunker.Split("abcdefghijklmnopqrstuvwxyz", chunkSize: 10, overlap: 3)
            .ToArray();

        Assert.Equal(["abcdefghij", "hijklmnopq", "opqrstuvwx", "vwxyz"], chunks);
    }

    [Fact]
    public void Split_IgnoresEmptyText()
    {
        var chunks = TextChunker.Split("   ", chunkSize: 10, overlap: 3);

        Assert.Empty(chunks);
    }
}
