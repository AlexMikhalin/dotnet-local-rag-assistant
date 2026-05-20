namespace LocalRag.Ingestion;

public static class DocumentFinder
{
    public static IEnumerable<string> FindTextFiles(string path)
    {
        if (File.Exists(path))
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".txt" or ".md")
            {
                yield return path;
            }

            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension is ".txt" or ".md")
            {
                yield return file;
            }
        }
    }
}
