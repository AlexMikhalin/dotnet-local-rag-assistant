using LocalRag.Ingestion;

namespace LocalRag.Ingestion.Tests;

public sealed class DocumentFinderTests
{
    [Fact]
    public void FindTextFiles_ReturnsMarkdownAndTextFilesOnly()
    {
        var directory = CreateTempDirectory();
        var markdown = Path.Combine(directory, "notes.md");
        var text = Path.Combine(directory, "facts.txt");
        var json = Path.Combine(directory, "ignored.json");

        File.WriteAllText(markdown, "markdown");
        File.WriteAllText(text, "text");
        File.WriteAllText(json, "{}");

        try
        {
            var files = DocumentFinder.FindTextFiles(directory)
                .Select(file => Path.GetFileName(file)!)
                .Order()
                .ToArray();

            Assert.Equal(["facts.txt", "notes.md"], files);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"local-rag-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
