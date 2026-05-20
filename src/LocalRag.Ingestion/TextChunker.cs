namespace LocalRag.Ingestion;

public static class TextChunker
{
    public static IEnumerable<string> Split(string text, int chunkSize, int overlap)
    {
        var normalized = text.ReplaceLineEndings("\n").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            yield break;
        }

        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(chunkSize, normalized.Length - start);
            var end = start + length;

            if (end < normalized.Length)
            {
                var lastBreak = normalized.LastIndexOf('\n', end - 1, length);
                if (lastBreak > start + chunkSize / 2)
                {
                    end = lastBreak;
                }
            }

            var chunk = normalized[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                yield return chunk;
            }

            if (end >= normalized.Length)
            {
                break;
            }

            start = Math.Max(0, end - overlap);
        }
    }
}
